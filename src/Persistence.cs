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

using Castle.DynamicProxy;


namespace Coincidental
{
	public interface IPersistence
	{
		object GetSource();
		IPersistentBase GetBase();
	}
	
	
	internal class PersistentProxyGeneration : IProxyGenerationHook
	{
		public bool ShouldInterceptMethod(Type type, MethodInfo method)
		{
			return ReflectHelper.IsGetter(method) || ReflectHelper.IsSetter(method) || method.Name == "GetSource" || method.Name == "GetBase";	
		}
		
		
		public void NonVirtualMemberNotification(Type type, MemberInfo memberInfo) {}
		public void MethodsInspected() {}
	}
	
	
	internal class Persistence
	{
		private static readonly ProxyGenerator generator		= new ProxyGenerator();
		private static readonly ProxyGenerationOptions options	= new ProxyGenerationOptions(new PersistentProxyGeneration());
		private static readonly Type [] interfaces				= new Type [] { typeof(IPersistence) };
		
		
		public static T Create<T>(PersistentContainer container) where T : class
		{
			return Create(typeof(T), container) as T;
		}
		
		
		public static object Create(Type type, PersistentContainer container)
		{
			PersistentInterceptor interceptor 	= new PersistentInterceptor(container);
			object result 						= generator.CreateClassProxy(type, interfaces, options, interceptor);
			interceptor.Active					= true;
			
			return result;
		}
		
		
		public static bool Required(Type type)
		{
			// DateTime/TimeStamp will also get ignored since it is a struct not a class
			return (type.IsClass || type.IsGenericType) && type != typeof(string);
		}
	}
}
		