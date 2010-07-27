using System;
using System.Linq;
using System.Threading;


namespace Coincidental
{
	public interface IPersistentBase
	{
		long Id 				{ get; }
		object Object			{ get; }	
		object PersistentObject	{ get; }
		
		bool Lock(bool wait);
		void Unlock();
	}
	
	
	internal abstract class PersistentBase : IPersistentBase
	{
		private static int LOCK_TIMEOUT = 2; 	// Milliseconds
		private static int CACHE_LIFE	= 600;	// Seconds

		protected PersistenceCache cache;
		protected ReaderWriterLockSlim objectLock = new ReaderWriterLockSlim();
		
		public long Id					{ get; set; }
		public bool Dirty				{ get; set; }
		public DateTime Access			{ get; set; }
		public object Object			{ get; set; }	
		public object PersistentObject	{ get; set; }
		
		
		public PersistentBase(long id, object source, PersistenceCache cache)
		{
			this.Id					= id;
			this.Dirty				= false;
			this.Object				= source;
			this.PersistentObject	= this;
			this.Access				= DateTime.Now;
			this.cache				= cache;
		}
		
		
		public bool Expired
		{
			get { return (DateTime.Now - this.Access).Seconds > CACHE_LIFE; }
		}
		
		
		public bool Lock(bool wait)
		{
			if (Provider.Debugging) Console.WriteLine("Coincidental: Locking {0} ({1})", this.Object, this.Id);
			
			return this.objectLock.TryEnterWriteLock(wait ? -1 : LOCK_TIMEOUT);	
		}
		
		
		public void Unlock()
		{
			if (Provider.Debugging) Console.WriteLine("Coincidental: Unlocking {0} ({1})", this.Object, this.Id);
			
			if (this.objectLock.IsWriteLockHeld) this.objectLock.ExitWriteLock();
		}
		
		
		protected void AssertWrite()
		{
			if (!this.objectLock.IsWriteLockHeld) throw new Exception("Attempted to modify an unlocked persistent object");
			
			this.Dirty = true;
		}
		
		
		protected T Read<T>(Func<T> operation)
		{
			T result = default(T);
			
			if (!this.objectLock.IsWriteLockHeld)
			{
				this.objectLock.EnterReadLock();
				
				try 	{ result = operation();				}
				finally	{ this.objectLock.ExitReadLock();	}
			}
			else result = operation();
			
			return result;
		}
		
		
		protected IPersistentBase GetBase(Type type, object item)
		{
			return (item is IPersistence) ? (item as IPersistence).GetBase() : this.cache.GetBase(type, item);
		}
	}
}
