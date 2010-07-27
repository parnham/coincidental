//	Coincidental
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
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Collections;


namespace Coincidental
{
	internal class InterceptingQuery<T> : IOrderedQueryable<T>
	{
		private Expression expression;
		private InterceptingProvider provider;
		
		
		public InterceptingQuery(InterceptingProvider provider, Expression expression)
		{
			this.provider	= provider;
			this.expression = expression;
		}
		
		
		public IQueryProvider Provider
		{
			get { return this.provider; }
		}
		
		
		public Expression Expression
		{
			get { return this.expression; }
		}
		
		
		public Type ElementType
		{
			get { return typeof(T); }
		}
		
		
		public IEnumerator<T> GetEnumerator()
		{
			return this.provider.ExecuteQuery<T>(this.expression);
		}
		
		
		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.provider.ExecuteQuery<T>(this.expression);
		}
	}
	
	
	internal class InterceptingProvider : IQueryProvider
	{
		private IQueryProvider source;
		private PersistenceCache cache;
		
		private InterceptingProvider(IQueryProvider source, PersistenceCache cache)
		{
			this.source = source;
			this.cache	= cache;
		}
		
		
		public IQueryable<T> CreateQuery<T>(Expression expression)
		{
			return new InterceptingQuery<T>(this, expression);
		}
		
		
		public IQueryable CreateQuery(Expression expression)
		{
			Type type = typeof(InterceptingQuery<>).MakeGenericType(ReflectHelper.FindIEnumerable(expression.Type));
			
			return Activator.CreateInstance(type, this, expression) as IQueryable;
		}
		
		
		public IEnumerator<T> ExecuteQuery<T>(Expression expression)
		{
			Type type = typeof(T);
			
			if ( (type.IsClass || type.IsGenericType) && type != typeof(string))
			{
				List<T> result	= new List<T>();
				
				foreach(T item in this.source.CreateQuery<T>(expression))
				{
					result.Add((T)this.cache.GetPersistent(type, item));
				}
			
				return result.GetEnumerator();
			}
			
			return this.source.CreateQuery<T>(expression).GetEnumerator();
		}  
		
		
		public T Execute<T>(Expression expression)
		{
			Type type = typeof(T);
			return type.IsClass ? (T)this.cache.GetPersistent(type, this.source.Execute<T>(expression)) : this.source.Execute<T>(expression);
		}
		
		
		public object Execute(Expression expression)
		{
			Type type = expression.GetType();
			return type.IsClass ? this.cache.GetPersistent(type, this.source.Execute(expression)) : this.source.Execute(expression);
		}
		
		
		internal static IQueryable<T> Intercept<T>(IQueryable<T> source, PersistenceCache cache)
		{
			return new InterceptingProvider(source.Provider, cache).CreateQuery<T>(source.Expression);
		}	
	}	
}
