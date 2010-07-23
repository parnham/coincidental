using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

using Coincidental;


namespace CoincidentalTest
{
	class MainClass
	{
		public static int WORKER_NUMBER = 100;
		
		
		public static void Main(string [] args)
		{
			Provider db = new Provider();
			db.Initialise("test.yap", 1);
			db.Store<Entity>(new Entity { 
				Name = "Test", 
				Time = DateTime.Now, 
				Reference = new Entity { 
					Name = "Reference" 
				} 
			});
			
			Entity entity = db.Get<Entity>(e => e.Name == "Test");
			Console.WriteLine(entity.Name);
			
			try
			{
				// Will throw an exception since the object is not locked
				entity.Name = "NewName";		
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}
			
			while (!db.Lock(entity)) Thread.Sleep(1);
			
			// This time it works because the object has been locked
			entity.Name = "NewName";		
			
			db.Unlock(entity);
			
			Console.WriteLine(entity.Name);
			
			db.Dispose();
			
			/*using (Provider db = new Provider())
			{
				// Create new DB
				if (File.Exists("test.yap")) File.Delete("test.yap");
				db.Initialise("test.yap", 1);
				
				// Initialise DB
				Entity entity = MainClass.GetInitialData();
				db.Store<Entity>(entity);
				
				MainClass.RunTests("First pass", 0, db);
			}
			
			using (Provider db = new Provider())
			{
				// Open DB
				db.Initialise("test.yap", 1);
				
				MainClass.RunTests("Second pass", 1, db);
			}
			
			
			using (Provider db = new Provider())
			{
				// Open DB
				db.Initialise("test.yap", 1);
			
				// Read results
				Entity entity = db.Get<Entity>(e => e.Name == "Test");
				
				Console.WriteLine("\nEntity: ");
				MainClass.PrintEntity(entity, 2);
				MainClass.CheckEntity(entity);
			}*/
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
				finished = threads.TrueForAll(t => t.ThreadState == ThreadState.Stopped);
				Thread.Sleep(1);
			}
			
			// Output results
			Console.WriteLine("\n{0} statistics:", name);
			MainClass.PrintStats(workers);
		}
		
		
		private static void PrintStats(List<Worker> workers)
		{
			Console.WriteLine("  Min:                      {0} ms", workers.Min(w => w.TimeTaken));
			Console.WriteLine("  Max:                      {0} ms", workers.Max(w => w.TimeTaken));
			Console.WriteLine("  Mean:                     {0} ms", workers.Average(w => w.TimeTaken));
			Console.WriteLine("  Total writelock failures: {0}", workers.Sum(w => w.WriteLockFails));
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
		
		
		private static void CheckEntity(Entity entity)
		{
		}
	}
}

