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
using System.Threading;
using System.Reflection;
using System.Collections.Generic;


namespace Coincidental
{
	internal class PersistentContainer : PersistentBase
	{
		private static Type orphanType = typeof(IOrphanTracked);
		
		public PersistentContainer(Type type, long id, object source, PersistenceCache cache) : base(id, source, cache)
		{
			this.PersistentObject = Persistence.Create(type, this);
		}
		
		
		public object GetProperty(PropertyInfo property)
		{
			object result	= null;
			Type type		= property.PropertyType;

			if (Persistence.Required(type))
			{
				object obj	= this.Read(() => property.GetValue(this.Object, null));
				result		= this.cache.GetPersistent(type, obj);
			}
			else result = this.Read(() => property.GetValue(this.Object, null));
				
			return result;
		}
		
		
		public void SetProperty(PropertyInfo property, object value)
		{	
			this.AssertWrite();
			
			if (this.orphanTracked && property.Name == "ReferenceCount")
			{
				throw new Exception("Attempt to modify the reference count of an orphan tracked persistent object");
			}

			Type type		= property.PropertyType;
			object actual 	= value;
			
			if (Persistence.Required(type))
			{
				bool orphanTracked = orphanType.IsAssignableFrom(type);
				
				if (orphanTracked)
				{
					object current = property.GetValue(this.Object, null);
					if (current != null) this.cache.GetBase(type, current).UnReference();
				}
			
				if (value != null)
				{
					// If target is not persistent, create a new persistent wrapper and actually store the object to the database. If the target is persistent simply retrieve its source.
					//actual = (value is IPersistence) ? (value as IPersistence).GetSource() : this.cache.GetSource(type, value);
					IPersistentBase persistent	= (value is IPersistence) ? (value as IPersistence).GetBase() : this.cache.GetBase(type, value);
					actual						= persistent.Object;
					
					if (orphanTracked) persistent.Reference();
				}
			}
			
			property.SetValue(this.Object, actual, null);
		}
	}
}
