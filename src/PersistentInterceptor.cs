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
