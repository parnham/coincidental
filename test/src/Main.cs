using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

using Coincidental;
using System.Diagnostics;


namespace CoincidentalTest
{
	class MainClass
	{
		public static int WORKER_NUMBER = 100;
		
		
		public static void Main(string [] args)
		{
			if (args.Length == 1)
			{
				switch (args[0])
				{
					case "indexing":	MainClass.IndexingTest();	break;
					case "stress":		MainClass.StressTest();		break;
					case "query":		MainClass.QueryTest();		break;
					case "orphan":		MainClass.OrphanTest();		break;
					case "basequery":	MainClass.BaseQueryTest();	break;
				}
			}
			else Console.WriteLine("Invalid arguments (choose from 'indexing', 'stress', 'query', 'orphan' or 'basequery')");
		}
		
		
		private static void StressTest()
		{
			CoincidentalConfiguration config = Provider.Configure
				.Connection("test.yap")
				.ActivationDepth(1)
				.Indexing(i => i.Add<Entity>());
					          
			using (Provider db = new Provider())
			{
				// Create new DB
				if (File.Exists("test.yap")) File.Delete("test.yap");
				db.Initialise(config);
				
				// Initialise DB
				Entity entity = MainClass.GetInitialData();
				db.Store(entity);
				
				MainClass.RunTests("First pass", 0, db);
			}
			
			using (Provider db = new Provider())
			{
				// Open DB
				db.Initialise(config);
				
				MainClass.RunTests("Second pass", 1, db);
			}
			
			
			using (Provider db = new Provider())
			{
				// Open DB
				db.Initialise(config);
			
				// Read results
				Entity entity = db.Get<Entity>(e => e.Name == "Test");
				
				Console.WriteLine("\nEntity: ");
				MainClass.PrintEntity(entity, 2);
			}
		}
		
		
		private static Entity GetInitialData()
		{
			Entity entity = new Entity {
				Id 			= 0,
				Name		= "Test",
				Time 		= DateTime.Now,
				Reference	= new Entity { Id = 1, Name = "Reference", Time = DateTime.Today }
			};
			
			entity.ReferenceList.Add(new Entity { Id = 1, Name = "ListReference" });
			entity.StringLongDictionary.Add("String", 0);
			entity.LongReferenceDictionary.Add(0, entity.Reference);
			entity.ReferenceStringDictionary.Add(entity.Reference, "ReferenceString");
			entity.ReferenceReferenceDictionary.Add(new Entity { Id = 0, Name = "Key" }, new Entity { Id = 0, Name = "Value" });
			
			return entity;
		}
		
		
		private static void RunTests(string name, int pass, Provider db)
		{
			// Create worker set
			List<Worker> workers = new List<Worker>();
			List<Thread> threads = new List<Thread>();
			for (int i=0; i<WORKER_NUMBER; i++) 
			{
				Worker worker = new Worker(i, pass, db);
				workers.Add(worker);
				threads.Add(new Thread(worker.Work));
			}
			
			// Run workers
			threads.ForEach(t => t.Start());
			
			bool finished = false;
			while (!finished) 
			{
				finished = threads.TrueForAll(t => t.ThreadState == System.Threading.ThreadState.Stopped);
				Thread.Sleep(1);
			}
			
			// Output results
			Console.WriteLine("\n{0} statistics:", name);
			MainClass.PrintStats(workers);
		}
		
		
		private static void PrintStats(List<Worker> workers)
		{
			Console.WriteLine("  Min:  {0} ms", workers.Min(w => w.TimeTaken));
			Console.WriteLine("  Max:  {0} ms", workers.Max(w => w.TimeTaken));
			Console.WriteLine("  Mean: {0} ms", workers.Average(w => w.TimeTaken));
		}
		
		
		private static void PrintEntity(Entity entity, int indent)
		{
			string space = new string(' ', indent);
			Console.WriteLine("{0}Id:   {1}", space, entity.Id);
			Console.WriteLine("{0}Name: {1}", space, entity.Name);
			Console.WriteLine("{0}Time: {1}", space, entity.Time);
			if (entity.Reference != null)
			{
				Console.WriteLine("{0}Reference:", space);
				MainClass.PrintEntity(entity.Reference, indent + 2);
			}
			if (entity.LongList.Any()) Console.WriteLine(
				"{0}LongList: {1}", space, 
				entity.LongList.Aggregate<long, string>("", (s, l) => string.Format("{0}{1}, ", s, l))
			);
			if (entity.ReferenceList.Any())
			{
				int i=0;
				Console.WriteLine("{0}ReferenceList:", space);
				foreach (Entity item in entity.ReferenceList) 
				{
					Console.WriteLine("{0}  {1}:", space, i++);
					MainClass.PrintEntity(item, indent + 4);
				}
			}
			if (entity.StringLongDictionary.Any()) Console.WriteLine(
				"{0}StringLongDictionary: {1}", space, 
				entity.StringLongDictionary.Aggregate<KeyValuePair<string, long>, string>("", (s, k) => string.Format("{0}({1},{2}), ", s, k.Key, k.Value))
			);
			if (entity.LongReferenceDictionary.Any())
			{
				Console.WriteLine("{0}LongReferenceDictionary:", space);
				foreach (KeyValuePair<long, Entity> item in entity.LongReferenceDictionary) 
				{
					Console.WriteLine("{0}  {1}:", space, item.Key);
					MainClass.PrintEntity(item.Value, indent + 4);
				}
			}
			if (entity.ReferenceStringDictionary.Any())	Console.WriteLine(
				"{0}ReferenceStringDictionary: {1}", space, 
				entity.ReferenceStringDictionary.Aggregate<KeyValuePair<Entity, string>, string>("", (s, k) => string.Format("{0}({1},{2}), ", s, k.Key.Name, k.Value))
			);
			if (entity.ReferenceReferenceDictionary.Any()) Console.WriteLine(
				"{0}ReferenceReferenceDictionary: {1}", space, 
				entity.ReferenceReferenceDictionary.Aggregate<KeyValuePair<Entity, Entity>, string>("", (s, k) => string.Format("{0}({1}={2},{3}={4}), ", s, k.Key.Name, k.Key.Id, k.Value.Name, k.Value.Id))
			);
		}


		private static void IndexingTest()
		{
			CoincidentalConfiguration config = Provider.Configure
				.Connection("test.yap")
				.ActivationDepth(1)
				.Debugging
				.Indexing(i => i.AssemblyOf<Location>());
			
			using (Provider db = new Provider())
			{
				if (File.Exists("test.yap")) File.Delete("test.yap");
				db.Initialise(config);
				
				Console.WriteLine("Preparing data...");
				
				for (int y = 0; y<200; y++)
				{
					for (int x = 0; x<200; x++)
					{
						db.Store(new Location { X = x, Y = y, Index = ((ulong)x << 32) | (ulong)y,  Name = string.Format("{0}, {1}", x, y) });
					}
				}
			}
			
			using (Provider db = new Provider())
			{
				Random random = new Random();
				db.Initialise(config);
				
				Stopwatch stopwatch = new Stopwatch();
				Console.WriteLine("Beginning query...");

				for (int i=0; i<10; i++)
				{
					stopwatch.Reset();
					stopwatch.Start();

					Location loc = db.Get<Location>(l => l.X == random.Next(199) && l.Y == random.Next(199));
					
					stopwatch.Stop();
					Console.WriteLine("Query found: {0} ({1} ms) {2}", loc.Name, stopwatch.ElapsedMilliseconds, loc is IPersistence);
				}
				
				Console.WriteLine("Beginning query with index...");

				for (int i=0; i<10; i++)
				{
					stopwatch.Reset();
					stopwatch.Start();
					
					ulong index = ((ulong)random.Next(199) << 32) | (ulong)random.Next(199);

					Location loc = db.Get<Location>(l => l.Index == index);
					
					stopwatch.Stop();
					Console.WriteLine("Query found: {0} ({1} ms) {2}", loc.Name, stopwatch.ElapsedMilliseconds, loc is IPersistence);
				}
			}
		}
		
		
		private static void QueryTest()
		{
			CoincidentalConfiguration config = Provider.Configure
				.Connection("test.yap")
				.ActivationDepth(1)
				.Debugging
				.Indexing(i => i.AssemblyOf<Location>());
			
			
			using (Provider db = new Provider())
			{
				if (File.Exists("test.yap")) File.Delete("test.yap");
				db.Initialise(config);
				
				Console.WriteLine("Preparing data...");
				
				for (int y = 0; y<100; y++)
				{
					for (int x = 0; x<100; x++)
					{
						db.Store(new Location { X = x, Y = y, Name = string.Format("{0}, {1}", x, y) });
					}
				}
			}
			
			using (Provider db = new Provider())
			{
				db.Initialise(config);
				
				List<QueryWorker> workers	= new List<QueryWorker>();
				List<Thread> threads		= new List<Thread>();
				
				for (int i=0; i<WORKER_NUMBER; i++)
				{
					QueryWorker worker = new QueryWorker(i, db);
					workers.Add(worker);
					threads.Add(new Thread(worker.Work));
				}
				
				Console.WriteLine("Running query test...");
				
				// Run workers
				threads.ForEach(t => t.Start());
				
				bool finished = false;
				while (!finished) 
				{
					finished = threads.TrueForAll(t => t.ThreadState == System.Threading.ThreadState.Stopped);
					Thread.Sleep(1);
				}
				
				Console.WriteLine("  Min:  {0} ms", workers.Min(w => w.TimeTaken));
				Console.WriteLine("  Max:  {0} ms", workers.Max(w => w.TimeTaken));
				Console.WriteLine("  Mean: {0} ms", workers.Average(w => w.TimeTaken));
			}
		}
		
		
		private static void OrphanTest()
		{
			CoincidentalConfiguration config = Provider.Configure
				.Connection("test.yap")
				.ActivationDepth(1)
				.Debugging;
			
			
			using (Provider db = new Provider())
			{
				if (File.Exists("test.yap")) File.Delete("test.yap");
				db.Initialise(config);
				
				UnTracked untracked = db.Store(new UnTracked { Name = "UnTracked" });
				UnTracked other		= db.Store(new UnTracked { Name = "Other" });
				Tracked tracked		= db.Store(new Tracked { Name = "Tracked", ReferenceCount = 10 });
				
				Console.WriteLine("Initial reference count = {0}", tracked.ReferenceCount);
				
				try
				{
					using (db.Lock(tracked)) tracked.ReferenceCount = 10;
				}
				catch(Exception e)
				{
					Console.WriteLine("Exception caught: {0}", e.Message);
				}
				
				using (db.Lock(untracked)) untracked.Item = tracked;
				Console.WriteLine("Single reference added, count = {0}", tracked.ReferenceCount);
				
				using (db.Lock(other)) other.Item = tracked;
				Console.WriteLine("Single reference added to other, count = {0}", tracked.ReferenceCount);
				
				using (db.Lock(untracked.ItemList)) untracked.ItemList.Add(tracked);
				Console.WriteLine("Added to list, count = {0}", tracked.ReferenceCount);
				
				using (db.Lock(untracked)) untracked.Item = null;
				Console.WriteLine("Single reference removed, count = {0}", tracked.ReferenceCount);
			}
			
			using (Provider db = new Provider())
			{
				db.Initialise(config);
				
				UnTracked untracked = db.Get<UnTracked>(u => u.Name == "UnTracked");
				Tracked tracked		= db.Get<Tracked>(t => t.Name == "Tracked");
				
				Console.WriteLine("Closed and opened new provider, count = {0}", tracked.ReferenceCount);
				
				using (db.Lock(untracked.ItemList)) untracked.ItemList.Remove(tracked);
				Console.WriteLine("Removed from list, count = {0}", tracked.ReferenceCount);
				
				using (db.Lock(untracked)) untracked.Item = tracked;
				Console.WriteLine("Single reference added, count = {0}", tracked.ReferenceCount);
			}
			
			config = config.AutomaticOrphanPurge;
			
			using (Provider db = new Provider())
			{
				db.Initialise(config);
				
				UnTracked untracked = db.Get<UnTracked>(u => u.Name == "UnTracked");
				UnTracked other		= db.Get<UnTracked>(u => u.Name == "Other");
				Tracked tracked		= db.Get<Tracked>(t => t.Name == "Tracked");
				
				Console.WriteLine("Closed and opened new provider with automatic orphan purge enabled, count = {0}", tracked.ReferenceCount);
				
				db.Delete(other);
				Console.WriteLine("Deleted other, count = {0}", tracked.ReferenceCount);
				
				using (db.Lock(untracked)) untracked.Item = null;
				Console.WriteLine("Single reference removed, count = {0}", tracked.ReferenceCount);
				
				db.Flush();
				
				tracked = db.Get<Tracked>(t => t.Name == "Tracked");
				
				if (tracked == null) 	Console.WriteLine("Attempted to re-acquire tracked item, but it was successfully purged");
				else 					Console.WriteLine("Managed to re-acquire tracked item, automatic purging failed!");
			}
		}
		
		
		private static void BaseQueryTest()
		{
			CoincidentalConfiguration config = Provider.Configure
				.Connection("test.yap")
				.ActivationDepth(1)
				.Debugging;
			
			using (Provider db = new Provider())
			{
				if (File.Exists("test.yap")) File.Delete("test.yap");
				db.Initialise(config);
				
				db.Store(new TestBase { Name = "FirstBase" });
				db.Store(new TestBase { Name = "SecondBase" });
				db.Store(new TestDescendant { Name = "FirstDescendant", Value = 42 });
				db.Store(new TestDescendant { Name = "SecondDescendant", Value = 1 });
				
				Console.WriteLine("Added test data");
			}
			
			using (Provider db = new Provider())
			{
				db.Initialise(config);
				
				Console.WriteLine("Querying for TestDescendant");
				
				foreach (TestDescendant d in db.Query<TestDescendant>())
				{
					Console.WriteLine("  Found: {0}", d.Name);
				}
			}
			
			using (Provider db = new Provider())
			{
				db.Initialise(config);
				
				Console.WriteLine("Querying for TestBase");
				
				foreach (TestBase d in db.Query<TestBase>())
				{
					Console.WriteLine("  Found: {0} {1} descendant", d.Name, d is TestDescendant ? "is" : "is not");
				}
			}
		}
	}
}
