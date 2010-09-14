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
using System.Collections;
using System.Collections.Generic;


namespace Coincidental
{
	internal class PersistentDictionary<TKey, TValue> : PersistentBase, IPersistence, IDictionary<TKey, TValue>
	{
		protected IDictionary<TKey, TValue> source		= null;
		protected IDictionary<TKey, TValue> persistent	= null;
		private bool isKeyClass;
		private bool isValueClass;
		private bool isClass;
		private bool orphanTrackedKey;
		private bool orphanTrackedValue;
		private Type keyType;
		private Type valueType;
		
		
		private struct KeyValueSet
		{
			public KeyValuePair<TKey, TValue> Source;
			public KeyValuePair<TKey, TValue> Persistent;
		}
		
		
		public PersistentDictionary(long id, IDictionary<TKey, TValue> source, PersistenceCache cache) : base(id, source, cache)
		{
			this.keyType 			= typeof(TKey);
			this.valueType			= typeof(TValue);
			this.source				= source;
			this.isKeyClass			= (this.keyType.IsClass		|| this.keyType.IsGenericType)		&& this.keyType		!= typeof(string);
			this.isValueClass		= (this.valueType.IsClass	|| this.valueType.IsGenericType)	&& this.valueType	!= typeof(string);
			this.isClass			= this.isKeyClass || this.isValueClass;
			this.orphanTrackedKey	= this.isKeyClass ? typeof(IOrphanTracked).IsAssignableFrom(this.keyType) : false;
			this.orphanTrackedValue	= this.isValueClass ? typeof(IOrphanTracked).IsAssignableFrom(this.valueType) : false;
			this.PersistentObject	= this;
			
			if (this.isClass)
			{
				this.persistent	= new Dictionary<TKey, TValue>(source.Count);
				
				foreach (KeyValuePair<TKey, TValue> item in this.source)
				{
					TKey key 		= this.isKeyClass   ? (TKey)this.cache.GetPersistent(item.Key) : item.Key;
					TValue value	= this.isValueClass ? (TValue)this.cache.GetPersistent(item.Value) : item.Value;
					
					this.persistent.Add(key, value);
				}
			}
		}
		
		
		public object GetSource()
		{
			return this.Object;
		}
		
		
		public IPersistentBase GetBase()
		{
			return this;
		}
		
		
		public override void UnReferenceMembers()
		{
			if (this.orphanTrackedKey) 		foreach(var key in this.persistent.Keys.Cast<IPersistence>())		key.UnReference();
			if (this.orphanTrackedValue)	foreach(var value in this.persistent.Values.Cast<IPersistence>()) 	value.UnReference();
		}
		
		
		private KeyValueSet GetSet(TKey key, TValue value)
		{
			TKey sourceKey		= key, 		persistentKey	= key;
			TValue sourceValue	= value, 	persistentValue = value;
			
			if (this.isKeyClass)
			{	
				IPersistentBase item	= this.GetBase(key);
				sourceKey				= (TKey)item.Object;
				persistentKey			= (TKey)item.PersistentObject;
			}
			
			if (this.isValueClass)
			{
				IPersistentBase item	= this.GetBase(value);
				sourceValue				= (TValue)item.Object;
				persistentValue			= (TValue)item.PersistentObject;
			}
			
			return new KeyValueSet { 
				Source 		= new KeyValuePair<TKey, TValue>(sourceKey, sourceValue), 
				Persistent 	= new KeyValuePair<TKey, TValue>(persistentKey, persistentValue) 
			};
		}
		
		
		public KeyValuePair<TKey, TValue> ElementAt(int index)
		{
			return this.Read(() => this.isClass ? this.persistent.ElementAt(index) : this.source.ElementAt(index));
		}
		
		
	#region IDictionary<TKey, TValue> Members
		public void Add(TKey key, TValue value)
		{
			this.AssertWrite();
			
			if (this.isClass)
			{
				KeyValueSet item = this.GetSet(key, value);
				
				this.source.Add(item.Source);
				this.persistent.Add(item.Persistent);
				
				if (this.orphanTrackedKey)		(item.Persistent.Key as IPersistence).Reference();
				if (this.orphanTrackedValue)	(item.Persistent.Value as IPersistence).Reference();
			}
			else this.source.Add(key, value);
		}
		
		
		public bool ContainsKey(TKey key)
		{
			return this.isClass ? this.Read(() => this.persistent.ContainsKey(key)) : this.Read(() => this.source.ContainsKey(key));
		}
		
		
		public bool Remove(TKey key)
		{
			this.AssertWrite();
				
			if (this.isClass)	
			{
				if (this.persistent.ContainsKey(key))
				{
					if (this.isKeyClass) 	
					{
						if (key is IPersistence)
						{
							if (this.orphanTrackedValue) 	(this.persistent[key] as IPersistence).UnReference();
							if (this.orphanTrackedKey)		(key as IPersistence).UnReference();	
							
							return this.source.Remove((TKey)(key as IPersistence).GetSource()) && this.persistent.Remove(key);
						}
						else return false;
					}
					else 					
					{
						if (this.orphanTrackedValue) (this.persistent[key] as IPersistence).UnReference();
						
						return this.source.Remove(key) && this.persistent.Remove(key);
					}
				}
				else return false;
			}
			else return this.source.Remove(key);
		}
		
		
		public bool TryGetValue(TKey key, out TValue value)
		{
			TValue item = default(TValue);
			
			bool result = this.Read(() => this.isClass ? this.persistent.TryGetValue(key, out item) : this.source.TryGetValue(key, out item));
			value		= result ? item : default(TValue);
			
			return result;
		}
		
		
		public TValue this[TKey key]
		{
			get
			{
				return this.Read(() => this.isClass ? this.persistent[key] : this.source[key]);
			}
			
			set
			{
				this.AssertWrite();
				
				if (this.isClass)
				{
					KeyValueSet item = this.GetSet(key, value);
					
					if (this.orphanTrackedValue)
					{
						(this.persistent[item.Persistent.Key] as IPersistence).UnReference();
						(item.Persistent.Value as IPersistence).Reference();
					}		
					
					this.source[item.Source.Key] 			= item.Source.Value;
					this.persistent[item.Persistent.Key]	= item.Persistent.Value;
				}
				else this.source[key] = value;
			}
		}
		
		
		public ICollection<TKey> Keys
		{
			get
			{
				return this.Read(() =>
				{
					TKey [] result = new TKey[this.source.Count];
					if (this.isClass) 	this.persistent.Keys.CopyTo(result, 0);
					else 				this.source.Keys.CopyTo(result, 0);
					
					return result;
				});
			}
		}
		
		
		public ICollection<TValue> Values
		{
			get
			{
				return this.Read(() =>
				{
					TValue [] result = new TValue[this.source.Count];
					if (this.isClass) 	this.persistent.Values.CopyTo(result, 0);
					else 				this.source.Values.CopyTo(result, 0);
					
					return result;
				});
			}
		}
	#endregion
		
	
	#region ICollection Members
		public void Add(KeyValuePair<TKey, TValue> item)
		{
			this.AssertWrite();
			
			if (this.isClass)
			{
				KeyValueSet kvs = this.GetSet(item.Key, item.Value);
				
				this.source.Add(kvs.Source);
				this.persistent.Add(kvs.Persistent);
				
				if (this.orphanTrackedKey) 		(kvs.Persistent.Key as IPersistence).Reference();
				if (this.orphanTrackedValue)	(kvs.Persistent.Value as IPersistence).Reference();
			}
			else this.source.Add(item);
		}
		
		
		public void Clear()
		{
			this.AssertWrite();
			
			this.source.Clear();
			if (this.isClass) 
			{
				if (this.orphanTrackedKey) 		foreach(var key in this.persistent.Keys.Cast<IPersistence>())		key.UnReference();
				if (this.orphanTrackedValue)	foreach(var value in this.persistent.Values.Cast<IPersistence>()) 	value.UnReference();
				this.persistent.Clear();
			}
		}
		
		
		public bool Contains(KeyValuePair<TKey, TValue> item)
		{
			return this.Read(() => this.isClass ? this.persistent.Contains(item) : this.source.Contains(item));
		}
		
		
		public void CopyTo(KeyValuePair<TKey, TValue> [] array, int index)
		{
			this.Read(() => {
				int count = this.source.Count;
				if (this.isClass) 	for (int i=0; i<count; i++) array.SetValue(this.persistent.ElementAt(i), i + index);
				else 				for (int i=0; i<count; i++) array.SetValue(this.source.ElementAt(i), i + index);
				return true;
			});
		}
		
		
		public bool Remove(KeyValuePair<TKey, TValue> item)
		{
			this.AssertWrite();
			
			if (this.isClass)
			{
				KeyValueSet kvs = this.GetSet(item.Key, item.Value);
				
				if (this.orphanTrackedKey)		(kvs.Persistent.Key as IPersistence).UnReference();
				if (this.orphanTrackedValue)	(kvs.Persistent.Value as IPersistence).UnReference();
				
				return this.source.Remove(kvs.Source) && this.persistent.Remove(kvs.Persistent);
			}
			else return this.source.Remove(item);
		}
		
		
		public int Count
		{
			get { return this.Read(() => this.source.Count); }
		}
		
		
		public bool IsReadOnly
		{
			get { return false; }
		}
	#endregion
	
		
	#region IEnumerable Members
		IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
		{
			return new Enumerator(this);
		}
		
		
		public IEnumerator GetEnumerator()
		{
			return new Enumerator(this);
		}
	#endregion
		
		
		class Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IEnumerator
		{
			private PersistentDictionary<TKey, TValue> dictionary;
			private KeyValuePair<TKey, TValue> current;
			private int index = -1;
			
			
			public Enumerator(PersistentDictionary<TKey, TValue> dictionary)
			{
				this.dictionary = dictionary;
			}
		
		
			public KeyValuePair<TKey, TValue> Current
			{
				get { return this.current; }
			}
			
			
			object IEnumerator.Current
			{
				get { return this.current; }
			}
			
			
			public bool MoveNext()
			{
				if (++this.index < this.dictionary.Count) 
				{
					this.current = this.dictionary.ElementAt(this.index);
					return true;
				}
				
				return false;
			}
			
			
			public void Reset() 
			{
				this.index		= -1;
				this.current	= default(KeyValuePair<TKey, TValue>);
			}
			
			
			void IDisposable.Dispose() { }
		}
	}
}
