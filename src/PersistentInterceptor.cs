using System;
using System.Reflection;

using Castle.Core.Interceptor;


namespace Coincidental
{
	internal class PersistentInterceptor : IInterceptor
	{
		public bool Active { get; set; }
		private PersistentContainer container;
		
		
		public PersistentInterceptor(PersistentContainer container)
		{
			this.container	= container;
			this.Active		= false;
		}
		
		
		public void Intercept(IInvocation invocation)
		{
			if (this.Active)
			{
				PropertyInfo property = invocation.TargetType.GetProperty(ReflectHelper.GetPropertyName(invocation.Method));
				
				if (property != null)
				{
					if (ReflectHelper.IsGetter(invocation.Method))	invocation.ReturnValue = this.container.GetProperty(property);
					else 											this.container.SetProperty(property, invocation.Arguments[0]);
				}
				else switch (invocation.Method.Name)
				{
					case "GetSource": 	invocation.ReturnValue = this.container.Object;	break;
					case "GetBase":		invocation.ReturnValue = this.container;		break;
				}
			}

			// We do not call invocation.Proceed() here since the proxy should never be accessing its underlying fields but should always act
			// as a thread-safe "view" of the original db4o object.
		}
	}
}
