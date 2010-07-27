using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using Db4objects.Db4o.Config;


namespace Db4objects.Db4o
{
    public static class DBSchemaUtility
    {
        private const string BackingfieldPostFix = "k__BackingField";

        public static void IndexClass(this ICommonConfiguration config, Type type)
        {
            var propertiesToIndex 	= from p in type.GetProperties()
                                      where p.GetCustomAttributes(typeof(Coincidental.IndexedAttribute), true).Any()
                                      select p;
			
            foreach (var property in propertiesToIndex)
            {
                var fieldName = string.Format("<{0}>{1}", property.Name, BackingfieldPostFix);
				
                if (type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance) == null)
	            {
	                throw new ArgumentException(String.Format(
						@"The Property '{0}' is marked with the attribute '{1}'. However it seems that this property isn't an auto-property, because the field {2} couldn't be found",
	                    property.Name, typeof(Coincidental.IndexedAttribute).Name, fieldName
					));
	            }
				
				if (Coincidental.Provider.Debugging) Console.WriteLine("Coincidental: Adding index for {0}, {1}", type, fieldName);
                config.ObjectClass(type).ObjectField(fieldName).Indexed(true);
            }
        }
    }
}