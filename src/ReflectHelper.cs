using System;
using System.Reflection;
using System.Collections.Generic;


namespace Coincidental
{
	internal class ReflectHelper
	{
		public static bool IsGetter(MethodInfo method)
		{
			return method.IsSpecialName && method.Name.StartsWith("get_");
		}
		
		
		public static bool IsSetter(MethodInfo method)
		{
			return method.IsSpecialName && method.Name.StartsWith("set_");
		}
		
		
		public static string GetPropertyName(MethodInfo method)
		{
			return method.Name.Substring(4);
		}
		
		
		// Adapted from the implementation in the IQToolkit (http://iqtoolkit.codeplex.com/)
		public static Type FindIEnumerable(Type type)
        {
            if (type == null || type == typeof(string)) return null;
			
            if (type.IsArray) return typeof(IEnumerable<>).MakeGenericType(type.GetElementType());
			
            if (type.IsGenericType)
            {
                foreach (Type arg in type.GetGenericArguments())
                {
                    Type ienum = typeof(IEnumerable<>).MakeGenericType(arg);
					
                    if (ienum.IsAssignableFrom(type)) return ienum;
                }
            }
			
            Type [] ifaces = type.GetInterfaces();
			
            if (ifaces != null && ifaces.Length > 0)
            {
                foreach (Type iface in ifaces)
                {
                    Type ienum = FindIEnumerable(iface);
                    if (ienum != null) return ienum;
                }
            }
			
            if (type.BaseType != null && type.BaseType != typeof(object)) return FindIEnumerable(type.BaseType);

            return null;
        }
	}
}
