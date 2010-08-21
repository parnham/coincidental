using System;


namespace Coincidental
{
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class IndexedAttribute : Attribute
    {
    }

	
	public interface IOrphanTracked
	{
		long ReferenceCount { get; set; }
	}
	
	
	public interface IPersistentBase
	{
		long Id 				{ get; }
		object Object			{ get; }	
		object PersistentObject	{ get; }
		
		bool Lock(bool wait);
		void Unlock();
		
		void Reference();
		void UnReference();
	}
	
	
	public interface IPersistence
	{
		object GetSource();
		IPersistentBase GetBase();
		
		void Reference();
		void UnReference();
	}
}
