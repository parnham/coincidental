using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

using Coincidental;

using Db4objects.Db4o;
using Db4objects.Db4o.Linq;

namespace CoincidentalTest
{
	public class QueryWorker
	{
		public long Id				{ get; set; }
		public long TimeTaken 		{ get; set; }
		private Provider db; 
		private Random random = new Random();
		
		
		public QueryWorker(long id, Provider db)
		{
			this.Id				= id;
			this.db				= db;
			this.TimeTaken		= -1;
		}
		
		
		public void Work()
		{
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			
			for (int i=0; i<10; i++)
			{
				int x = this.random.Next(99);
				int y = this.random.Next(99);
				
				Location loc = this.db.Get<Location>(l => l.X == x && l.Y == y);
				
				if (loc == null) Console.WriteLine("Problem querying {0}, {1}!", x, y);
				
				Thread.Sleep(1);
			}
			
			stopwatch.Stop();
			this.TimeTaken = stopwatch.ElapsedMilliseconds;
		}
	}
}