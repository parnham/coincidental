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
		