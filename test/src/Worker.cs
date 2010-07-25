using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

using Coincidental;


namespace CoincidentalTest
{
	public class Worker
	{
		public long Id				{ get; set; }
		public long TimeTaken 		{ get; set; }
		private Provider db; 
		private int pass;
		
		private List<Action<Entity>> jobs = new List<Action<Entity>>();
		
		
		public Worker(long id, int pass, Provider db)
		{
			this.Id				= id;
			this.db				= db;
			this.pass			= pass;
			this.TimeTaken		= -1;
			
			this.jobs.Add(this.Read);
			this.jobs.Add(this.Write);
			this.jobs.Add(this.WriteReference);
			this.jobs.Add(this.UpdateLists);
			this.jobs.Add(this.UpdateDictionaries);
			
			Random rnd	= new Random();
			this.jobs	= this.jobs.OrderBy(j => rnd.Next()).ToList();	
		}
		
		
		public void Work()
		{
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			
			Thread.Sleep(1);
			
			Entity entity = db.Get<Entity>(e => e.Name == "Test");
			
			foreach (Action<Entity> job in this.jobs) 
			{
				job(entity);
				Thread.Sleep(1);
			}
			
			stopwatch.Stop();
			this.TimeTaken = stopwatch.ElapsedMilliseconds;
		}
		
		
		private void Read(Entity entity)
		{
			long id 	= entity.Id;
			string name = entity.Name;
			
			foreach(Entity item in entity.ReferenceList) name = item.Name;
			id 		= entity.LongReferenceDictionary[0].Id;
			name	= entity.Reference.Name;
		}
		
		
		private void Write(Entity entity)
		{
			using (this.db.Lock(entity))
			{
				entity.Id++;
				entity.Time = DateTime.Now;
			}
		}
		
		
		private void WriteReference(Entity entity)
		{
			using (this.db.Lock(entity, entity.Reference))
			{
				entity.Reference.Name = string.Format("Owned by {0}", this.Id);
				entity.Reference.Id += 2;
			}
		}
		
		
		private void UpdateLists(Entity entity)
		{
			using (this.db.Lock(entity, entity.LongList))
			{
				entity.LongList.Add(MainClass.WORKER_NUMBER * this.pass + this.Id);
			}
			
			Entity first = entity.ReferenceList.First();
			
			using (this.db.Lock(first))
			{
				first.Name += string.Format(", {0}", this.Id);
			}
		}
		
		
		private void UpdateDictionaries(Entity entity)
		{
			using (this.db.Lock(entity, entity.StringLongDictionary))
			{
				entity.StringLongDictionary.Add((MainClass.WORKER_NUMBER * this.pass + this.Id).ToString(), this.Id);
			}
		
			
			KeyValuePair<Entity, Entity> item = entity.ReferenceReferenceDictionary.First();
			
			using (this.db.Lock(item.Key, item.Value, item.Value.LongList))
			{
				item.Key.Id++;
				item.Value.Id--;
				item.Value.Time = DateTime.Now;
				item.Value.LongList.Add(this.Id);
			}
		}
	}
}
