using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using Db4objects.Db4o;
using Db4objects.Db4o.Config;


namespace Coincidental
{
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class IndexedAttribute : Attribute
    {
    }
	
	
	public class CoincidentalConfiguration
	{
		private string connection 			= string.Empty;
		private int activationDepth 		= 2;
		private bool debugging				= false;
		private IndexConfiguration indexing	= new IndexConfiguration();
		
		
		internal CoincidentalConfiguration()
		{
		}
		
		
		public CoincidentalConfiguration Connection(string path)
		{
			this.connection = path;
			return this;
		}
		
		
		public CoincidentalConfiguration ActivationDepth(int depth)
		{
			this.activationDepth = depth;
			return this;
		}
		
		
		public CoincidentalConfiguration Debugging(bool enable)
		{
			this.debugging = enable;
			return this;
		}
		
		
		public CoincidentalConfiguration Indexing(Action<IndexConfiguration> indexingAction)
		{
			indexingAction(this.indexing);	
			return this;
		}
		
		
		internal IEmbeddedConfiguration Configuration
		{
			get
			{
				IEmbeddedConfiguration result		= Db4oEmbedded.NewConfiguration();
				result.Common.UpdateDepth			= 1;
				result.Common.ActivationDepth		= this.activationDepth;
				result.Common.OptimizeNativeQueries	= true;
				result.Common.MessageLevel			= this.debugging ? 1 : 0;

				foreach (Type clazz in this.indexing.Classes) result.Common.IndexClass(clazz);
				
				return result;
			}
		}
		
		
		internal bool DebugEnabled
		{
			get { return this.debugging; }
		}
		
		
		internal string ConnectionPath
		{
			get { return this.connection; }
		}
	}
	
	
	public class IndexConfiguration
	{
		private List<Assembly> assemblies			= new List<Assembly>();
		private List<Type> additional				= new List<Type>();
		private List<Func<Type, bool>> expressions 	= new List<Func<Type, bool>>();
		
		internal IndexConfiguration()
		{
		}
		
		
		public IndexConfiguration Add<T>()
		{
			this.additional.Add(typeof(T));
			return this;
		}
		
		
		public IndexConfiguration AssemblyOf<T>()
		{
			this.assemblies.Add(typeof(T).Assembly);
			return this;
		}
		
		
		public IndexConfiguration Where(Func<Type, bool> expression)
		{
			this.expressions.Add(expression);
			return this;
		}
		
		
		internal IEnumerable<Type> Classes
		{
			get
			{
				List<Type> result = new List<Type>(this.additional);
				
				foreach (Assembly assembly in this.assemblies.Distinct())
				{
					IEnumerable<Type> candidates = assembly.GetTypes();
					
					foreach (Func<Type, bool> expression in this.expressions) candidates = candidates.Where(expression);
					
					if (candidates.Any()) result.AddRange(candidates);
				}
				
				return result.Distinct();
			}
		}
	}
}