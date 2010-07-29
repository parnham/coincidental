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
