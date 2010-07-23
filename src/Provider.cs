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
	public class Lock : IDisposable
	{
		public int Failures	{ get; set; } 	// Debug property
		
		private IPersistentBase [] entities;

		
		internal Lock(IPersistentBase [] entities)
		{
			this.entities = entities;
		}
		
		
		public void Dispose()
		{
			foreach (IPersistentBase entity in entities) entity.Unlock();
		}
	}
	
	
	public class Provider : IDisposable
	{
		private IObjectContainer container	= null;
		private PersistenceCache cache		= null;
		

		public bool Initialise(string connectionString, int activationDepth)
		{
			if (this.container == null)
			{
				IEmbeddedConfiguration config	= Db4oEmbedded.NewConfiguration();
				config.Common.UpdateDepth		= 1;
				config.Common.ActivationDepth	= activationDepth;
				
				this.container	= Db4oEmbedded.OpenFile(config, connectionString);
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
		
		
		
		public Lock Lock(params object [] entities)
		{
			int failures 				= 0;
			IPersistentBase [] items	= entities.OfType<IPersistence>().Select<IPersistence, IPersistentBase>(p => p.GetBase()).ToArray();
			
			while (!this.TryLock(items)) 
			{
				// Possibly add an eventual break out here with an exception and some form of logging
				// indicating that a dead-lock has occurred?
				System.Threading.Thread.Sleep(1);
				failures++;
			}
			
			return new Lock(items) { Failures = failures };
		}
		
		
		public void Flush()
		{
			lock(this)
			{
				this.cache.Flush();
			}
		}
		
		
		private bool TryLock(IPersistentBase [] entities)
		{
			bool result = true;
			
			lock(this)
			{
				foreach (IPersistentBase entity in entities)
				{
					if (!entity.Lock(false))
					{
						result = false;
						break;
					}
				}
			}
			
			// Couldn't lock all objects so unlock any that have been to avoid a dead-lock
			if (!result) foreach (IPersistentBase entity in entities) entity.Unlock();
			
			return result;
		}
	}
}
