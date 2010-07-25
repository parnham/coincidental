using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using Db4objects.Db4o;


namespace Coincidental
{
	internal class PersistenceCache : IDisposable
	{
		private IObjectContainer container;
		private IDictionary<long, PersistentBase> cache = new SortedDictionary<long, PersistentBase>();
		
		
		public PersistenceCache(IObjectContainer container)
		{
			this.container	= container;
		}
		
		
		public void Dispose()
		{
			this.Flush();
			this.cache.Clear();
			this.cache = null;
		}
		
		
		public PersistentBase GetBase(Type type, object item)
		{
			PersistentBase result = null;
			
			if (item != null)
			{
				long id = this.container.Ext().GetID(item);
				
				// If db4o is not aware of this item then store it
				if (id == 0) id = this.Store(item);
				
				lock (this)
				{
					if (this.cache.ContainsKey(id))	
					{
						result			= this.cache[id];
						result.Access	= DateTime.Now;
					}
					else 
					{
						if (type.IsGenericType)
						{
							if (this.IsList(type))				result = Activator.CreateInstance(this.MakeList(type), id, this.Activate(item), this) as PersistentBase;
							else if (this.IsDictionary(type))	result = Activator.CreateInstance(this.MakeDictionary(type), id, this.Activate(item), this)	as PersistentBase;
						}
						else result = new PersistentContainer(type, id, this.Activate(item), this);
							
						if (result != null) this.cache.Add(id, result);
					}
				}
			}
			
			return result;	
		}
		
		
		private long Store(object item)
		{
			// Traverse object tree and replace persistent objects, if found, with
			// their source counterpart before storing to db4o.
			this.UnPersist(item, new List<object>());
			this.container.Store(item);
			
			return this.container.Ext().GetID(item);
		}
		
		
		private void UnPersist(object item, List<object> seen)
		{
			// The list is to prevent infinite recursion caused by 
			// self-referential object trees.
			if (!seen.Contains(item))
			{
				seen.Add(item);
				
				Type type = item.GetType();
				
				if (type.IsGenericType)
				{
					// Ignoring lists and dictionaries?
					
					/*if (this.IsList(type))
					{
						if (Persistence.Required(type.GetGenericArguments()[0]))
						{
							IList<object> list = item as IList<object>;
							
							for (int i=0; i<list.Count; i++)
							{
								if (list[i] is IPersistence)	list[i] = (list[i] as IPersistence).GetSource();
								else 							this.UnPersist(list[i], seen);
							}
						} 
					}
					else if (this.IsDictionary(type))
					{
						if (Persistence.RequiresPersistent(type.GetGenericArguments()[0]))
						{
						} 
						if (Persistence.RequiresPersistent(type.GetGenericArguments()[1]))
						{
						} 
					}*/
				}
				else
				{
					foreach (PropertyInfo property in type.GetProperties())
					{
						if (Persistence.Required(property.PropertyType))
						{
							object value = property.GetValue(item, null);
							
							if (value != null)
							{
								if (value is IPersistence)	property.SetValue(item, (value as IPersistence).GetSource(), null);
								else 						this.UnPersist(value, seen);
							}
						}
					}
				}
			}
		}

		
		private bool IsList(Type source)
		{
			return source.GetGenericTypeDefinition() == typeof(IList<>);
		}
		
		
		private bool IsDictionary(Type source)
		{
			return source.GetGenericTypeDefinition() == typeof(IDictionary<,>);
		}
		
	
		private Type MakeList(Type source)
		{
			return typeof(PersistentList<>).MakeGenericType(source.GetGenericArguments());
		}
		
		
		private Type MakeDictionary(Type source)
		{
			return typeof(PersistentDictionary<,>).MakeGenericType(source.GetGenericArguments());
		}
		
	
		public object GetPersistent(Type type, object item)
		{
			PersistentBase pb = this.GetBase(type, item);
			
			return pb != null ? pb.PersistentObject : null;
		}
		
		
		public object GetSource(Type type, object item)
		{
			PersistentBase pb = this.GetBase(type, item);
			
			return pb != null ? pb.Object : null;
		}
		
		
		public object Activate(object item)
		{
			if (!this.container.Ext().IsActive(item))
			{
				//Console.WriteLine("  Lazy loading:" + item.ToString());
				this.container.Activate(item, 1);	
			}
			
			return item;
		}
		
		
		public bool Delete(IPersistence item)
		{
			if (item != null)
			{
				lock(this)
				{
					this.container.Delete(item.GetSource());
					this.cache.Remove(item.GetBase().Id);
				}
				
				this.container.Commit();
				
				return true;
			}
			
			return false;
		}
		
		
		public bool Delete(IEnumerable<IPersistence> items)
		{
			if (items != null)
			{
				lock(this)
				{
					foreach (IPersistence item in items)
					{
						this.container.Delete(item.GetSource());
						this.cache.Remove(item.GetBase().Id);
					}
				}
				this.container.Commit();
				
				return true;
			}
			
			return false;
		}
		
		
		public void Flush()
		{
			// Flush dirty items to db4o.
			// Check for objects that have not been accessed for some time and 
			// remove them from the cache so that they can be garbage collected.
			List<PersistentBase> flush	= new List<PersistentBase>();
			List<long> delete			= new List<long>();
			
			lock(this)
			{ 
				foreach (KeyValuePair<long, PersistentBase> item in this.cache)
				{
					if (item.Value.Dirty)	flush.Add(item.Value);
					if (item.Value.Expired)	delete.Add(item.Key);
				}
				
				foreach (long id in delete) this.cache.Remove(id);
			}
			
			foreach (PersistentBase item in flush)
			{
				item.Lock(true);
					this.container.Store(item.Object);
					item.Dirty = false;
				item.Unlock();
			}
			this.container.Commit();
		}
	}
}
