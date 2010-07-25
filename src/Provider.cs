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
	/// <summary>
	/// Utility class to help ensure that a set of locked objects are always unlocked
	/// </summary>
	internal class Lock : IDisposable
	{
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
	
	
	/// <summary>
	/// The interface to Coincidental. 
	/// </summary>
	public class Provider : IDisposable
	{
		private IObjectContainer container	= null;
		private PersistenceCache cache		= null;
		

		/// <summary>
		/// This should be called before attempting to use any other functions, an exception will occur if you do not!
		/// </summary>
		/// <param name="connectionString">Should simply be the path to a db4o database.</param>
		/// <param name="activationDepth">Sets the activation depth to be used by db4o (automatically honoured by the Coincidental layer).</param>
		/// <returns>Returns true if successful and false if it has already been initialised.</returns>
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

		
		/// <summary>
		/// Stores a transient object tree to the database. If a persistent object is passed in nothing will happen. Mixing persistent
		/// objects with transients here is not a good idea.
		/// </summary>
		/// <param name="entity">Object to be stored. The underlying db4o container will automatically store the entire object tree.</param>
		/// <returns>Returns a persistent instance of the object.</returns>
		public T Store<T>(T entity) where T : class
		{
			return this.cache.GetPersistent(entity.GetType(), entity) as T;
		}
		
		
		/// <summary>
		/// Delete a persistent object from the database.
		/// </summary>
		/// <param name="entity">Persistent object to be deleted.</param>
		/// <returns>Returns true if successful and false if the object was not persistent.</returns>
		public bool Delete(object entity)
		{
			return this.cache.Delete(entity as IPersistence);
		}
		
		
		/// <summary>
		/// Delete a list of persistent objects from the database. This is more efficient than deleting the entities individually
		/// since it is performed in a single transaction.
		/// </summary>
		/// <param name="entities">An enumerable list of persistent objects.</param>
		/// <returns>Returns true if successful and false if any of the objects were not persistent.</returns>
		public bool Delete(IEnumerable<object> entities)
		{
			return this.cache.Delete(entities.OfType<IPersistence>());
		}
		
		
		/// <summary>
		/// Provides the primary interface for querying the database. The queries are passed straight through to db4o but the results
		/// are intercepted and proxied as persistent objects.
		/// </summary>
		/// <returns>Returns a queryable interface.</returns>
		public IQueryable<T> Query<T>()
		{
			return InterceptingProvider.Intercept<T>(this.container.AsQueryable<T>(), this.cache);
		}
		
		
		/// <summary>
		/// Provides a simple way of retrieving a single entity from the database.
		/// </summary>
		/// <param name="expression">A LINQ expression describing how to select the required entity.</param>
		/// <returns>Returns a persisted entity or null if no matching entity was found.</returns>
		public T Get<T>(Func<T, bool> expression) where T : class
		{
			return this.cache.GetPersistent(typeof(T), this.container.AsQueryable<T>().SingleOrDefault(expression)) as T;
		}
		
		
		/// <summary>
		/// Keeps attempting to lock all of the supplied objects until it is successful and then returns an IDisposable instance
    	/// which will automatically unlock all of the objects when it is disposed. The simplest way of ensuring
    	/// this is with the "using" syntax (see the example at the bottom).
		/// </summary>
		/// <param name="entities">A variable number of persistent objects to be locked.</param>
		/// <returns>Returns an object which must be disposed.</returns>
		public IDisposable Lock(params object [] entities)
		{
			IPersistentBase [] items = entities.OfType<IPersistence>().Select<IPersistence, IPersistentBase>(p => p.GetBase()).ToArray();
			
			// Possibly add an eventual break out here with an exception and some form of logging
			// indicating that a dead-lock has occurred?
			while (!this.TryLock(items)) System.Threading.Thread.Sleep(1);

			return new Lock(items);
		}
		
		
		/// <summary>
		/// Coincidental automatically keeps track of which objects have been modified. When this function is called any modified
		/// objects will be automatically flushed to the db4o database. Coincidental also takes this opportunity to clean up its
		/// cache, removing object instances that have not been accessed for some time so that they may be garbage collected.
		/// It is not advisable to call this at the end of each web request, in fact for a web-based game it is best to allow it to work
		/// almost as an in-memory database with a service thread forcing occasional flushes to disk. At the cost of potentially losing
		/// changes if the application is forcibly closed it should be far more responsive.
		/// </summary>
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
