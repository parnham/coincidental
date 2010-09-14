using System;
using System.Collections.Generic;

using Coincidental;


namespace CoincidentalTest
{
	public class Entity
	{
		[Indexed]
		public virtual long Id 													{ get; set; }
		public virtual string Name 												{ get; set; }
		public virtual DateTime Time											{ get; set; }
		public virtual Entity Reference											{ get; set; }
		public virtual IList<long> LongList										{ get; set; }
		public virtual IList<Entity> ReferenceList								{ get; set; }
		public virtual IDictionary<string, long> StringLongDictionary			{ get; set; }
		public virtual IDictionary<long, Entity> LongReferenceDictionary		{ get; set; }
		public virtual IDictionary<Entity, string> ReferenceStringDictionary	{ get; set; }
		public virtual IDictionary<Entity, Entity> ReferenceReferenceDictionary	{ get; set; }
		//public string NonVirtualTest { get; set; }
		
		
		public Entity()
		{
			this.LongList 						= new List<long>();
			this.ReferenceList					= new List<Entity>();
			this.StringLongDictionary			= new Dictionary<string, long>();
			this.LongReferenceDictionary		= new Dictionary<long, Entity>();
			this.ReferenceStringDictionary		= new Dictionary<Entity, string>();
			this.ReferenceReferenceDictionary	= new Dictionary<Entity, Entity>();
		}
	}
	
	
	public class Location
	{
		[Indexed]
		public virtual ulong Index	{ get; set; }
		[Indexed]
		public virtual int X 		{ get; set; }
		[Indexed]
		public virtual int Y 		{ get; set; }
		public virtual string Name	{ get; set; }
	}
	
	
	public class UnTracked
	{
		public virtual string Name 				{ get; set; }
		public virtual Tracked Item 			{ get; set; }
		public virtual IList<Tracked> ItemList	{ get; set; }
		
		
		public UnTracked()
		{
			this.ItemList = new List<Tracked>();
		}
	}
	
	
	public class Tracked : IOrphanTracked
	{
		public virtual long ReferenceCount { get; set; }
		public virtual string Name { get; set; }
	}
	
	
	public class TestBase
	{
		public virtual string Name { get; set; }
	}
	 
	
	public class TestDescendant : TestBase
	{
		public virtual long Value { get; set; }
	}
}
