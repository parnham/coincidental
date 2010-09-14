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
using System.Linq;
using System.Threading;


namespace Coincidental
{
	internal abstract class PersistentBase : IPersistentBase
	{
		private static int LOCK_TIMEOUT = 2; 	// Milliseconds

		
		protected bool orphanTracked;
		protected PersistenceCache cache;
		protected ReaderWriterLockSlim objectLock	= new ReaderWriterLockSlim();

		public long Id					{ get; private set; }
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
			this.orphanTracked		= source is IOrphanTracked;	
			this.cache				= cache;
		}
		
		
		public void Reference()
		{
			lock(this)
			{
				(this.Object as IOrphanTracked).ReferenceCount++;
				this.Dirty = true;
			}
		}
		
		
		public void UnReference()
		{
			lock(this)
			{
				(this.Object as IOrphanTracked).ReferenceCount--;
				this.Dirty = true;
			}
		}
		
		
		public abstract void UnReferenceMembers();
		
		
		public bool Orphaned
		{
			get
			{
				lock(this)
				{
					return (this.Object as IOrphanTracked).ReferenceCount == 0;
				}
			}
		}

		
		public bool Expired
		{
			get { return (DateTime.Now - this.Access).Seconds > Provider.CacheLife; }
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
			
			lock(this) this.Dirty = true;
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
		
		
		protected IPersistentBase GetBase(object item)
		{
			return (item is IPersistence) ? (item as IPersistence).GetBase() : this.cache.GetBase(item);
		}
	}
}
