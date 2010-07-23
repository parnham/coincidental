using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using Db4objects.Db4o;
using Db4objects.Db4o.Linq;
using Db4objects.Db4o.Query;
using Db4objects.Db4o.Config;


namespace Coincidental
{
	public class Provider : IDisposable
	{
		private IObjectContainer container	= null;
		private PersistenceCache cache		= null;
		

		public bool Initialise(string connectionString, int activationDepth)
		{
			if (this.container == null)
			{
				IEmbeddedConfiguration config = Db4oEmbedded.NewConfiguration(); // Db4oFactory.Configure();
				//config.UpdateDepth(1);
				config.Common.UpdateDepth		= 1;
				//config.ActivationDepth(activationDepth);
				config.Common.ActivationDepth	= activationDepth;
				
				this.container	= Db4oEmbedded.OpenFile(config, connectionString); //Db4oFactory.OpenFile(config, connectionString);
				this.cache		= new PersistenceCache(this.container);
			
				return true;
			}
			
			return false;
		}
		
		
		public void Dispose()
		{
			if (this.cache != null)		this.cache.Dispose();
			if (this.container != null) this.container.Close();	
			
			this.container	= null;
			this.cache		= null;
		}

		
		public T Store<T>(T entity) where T : class
		{
			return this.cache.GetPersistent(typeof(T), entity) as T;
		}
		
		
		public bool Delete(object entity)
		{
			return this.cache.Delete(entity as IPersistence);
		}
		
		
		public bool Delete(IEnumerable<object> entities)
		{
			return this.cache.Delete(entities.Cast<IPersistence>());
		}
		
		
		public IQueryable<T> Query<T>()
		{
			return InterceptingProvider.Intercept<T>(this.container.AsQueryable<T>(), this.cache);
		}
		
		
		public T Get<T>(Func<T, bool> expression) where T : class
		{
			return this.cache.GetPersistent(typeof(T), this.container.AsQueryable<T>().SingleOrDefault(expression)) as T;
		}
		
		
		public bool Lock(params object [] entities)
		{
			bool result = true;
			
			lock(this)
			{
				foreach (IPersistence entity in entities.Cast<IPersistence>())
				{
					if (entity != null)
					{
						if (!entity.GetBase().Lock(false))
						{
							result = false;
							break;
						}
					}
					else throw new Exception("Attempted to lock a transient object");
				}
			}
			
			// Couldn't lock all objects so unlock any that have been
			if (!result) this.Unlock(entities);
			
			return result;
		}
		
		
		public void Unlock(params object [] entities)
		{
			foreach (IPersistence entity in entities.Cast<IPersistence>())
			{
				if (entity != null) entity.GetBase().Unlock();
				else 				throw new Exception("Attempted to unlock a transient object");
			}
		}
		

		public void Flush()
		{
			lock(this)
			{
				this.cache.Flush();
			}
		}
	}

}

