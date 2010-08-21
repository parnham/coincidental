//  Coincidental
//  Copyright (C) 2010 Dan Parnham
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>

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
		public object QueryLock { get; private set; }
		private IObjectContainer container;
		private IDictionary<long, PersistentBase> cache = new SortedDictionary<long, PersistentBase>();
		
		
		public PersistenceCache(IObjectContainer container)
		{
			this.container	= container;
			this.QueryLock	= new object();
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
				
				if (item is IOrphanTracked) (item as IOrphanTracked).ReferenceCount = 0;
				
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
				if (Provider.Debugging) Console.WriteLine("  Lazy loading:" + item.ToString());
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
			List<object> orphans		= new List<object>();
			bool purge					= Provider.OrphanPurge;
			
			lock(this)
			{ 
				foreach (KeyValuePair<long, PersistentBase> item in this.cache)
				{
					if (item.Value.Dirty)	flush.Add(item.Value);
					if (item.Value.Expired)	delete.Add(item.Key);
					
					if (purge && item.Value.Object is IOrphanTracked)
					{
						if ((item.Value.Object as IOrphanTracked).ReferenceCount == 0) 
						{
							if (item.Value.Dirty) 		flush.Remove(item.Value);
							if (!item.Value.Expired)	delete.Add(item.Key);
							orphans.Add(item.Value.Object);
						}
					}
				}
				
				if (Provider.Debugging && delete.Any()) Console.WriteLine("Coincidental: Removing {0} expired items from cache", delete.Count);
				foreach (long id in delete) this.cache.Remove(id);
			}
			
			if (Provider.Debugging && flush.Any()) Console.WriteLine("Coincidental: Flushing {0} entities to disk", flush.Count);
			foreach (PersistentBase item in flush)
			{
				lock(item)
				{
					item.Lock(true);
						this.container.Store(item.Object);
					item.Unlock();
					item.Dirty = false;
				}
			}
			
			if (Provider.Debugging && orphans.Any()) Console.WriteLine("Coincidental: Purging {0} orphan(s)", orphans.Count);
			foreach (object item in orphans) this.container.Delete(item);
			
			this.container.Commit();
		}
	}
}
