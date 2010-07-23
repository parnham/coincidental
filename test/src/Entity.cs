using System;
using System.Collections.Generic;


namespace CoincidentalTest
{
	public class Entity
	{
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
}
