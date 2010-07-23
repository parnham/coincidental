Coincidental
============

A db4o concurrency and persistence wrapper for C#
-------------------------------------------------

During initial development of the browser-based game engine [Henge][1] it became apparent that an object
database was required since a standard SQL database (even when using a persistence layer such as NHibernate)
would not be suitable. It was decided to use [db4o][2] since it handles complex object graphs efficiently and
is extremely simple to use.

However, db4o does not employ any form of optimistic/object locking since it is primarily intended
for embedded use. Henge also had a requirement above and beyond optimistic locking that would ensure all object
modifications get applied, not simply overwritten.

For example, if you have a character in a game who is attacked by two other players simultaneously:

				Player 1							Player 2
		Can attack character?				Can attack character?
		health = character.Health			health = character.Health
		character.Health = health - 1		character.Health = health - 1

This is a very contrived example meant to represent two threads attempting to simultaneously modify the character. In this scenario
(even with some form of optimistic locking) one of the health modifications would be lost. What we actually want to do is the following:

				Player 1							Player 2
		Can attack character?				Can attack character?
		Lock character						Awaiting lock
		health = character.Health			-
		character.Health = health - 1		-
		Release lock						Lock character
											health = character.Health
											character.Health = health - 1
											Release lock

Coincidental was designed to permit this form of locking and also support concurrent reading (vital for a web-based game engine).
It was also intended to provide transparent activation and persistence thereby removing the need to perform post-build assembly
instrumentation via the Db4oTool. This is achieved through automatic object proxying making use of the [Castle DynamicProxy][3]
library.


Getting Started
---------------

Coincidental is in the alpha stage and is therefore not recommended for general use. It is not feature rich and only exposes
some of the functionality that you would otherwise get from using db4o directly. Since it is in alpha and being
developed alongside Henge please be aware that the API is likely to change.

Until it has reached a reasonably stable state there will not be any file releases so you must build the library from source.


Building Coincidental
---------------------

To build and use Coincidental you must have either .NET 3.5 or Mono 2.6+ installed. The solution can be opened using an IDE such
as MonoDevelop, SharpDevelop or VisualStudio. Alternatively it can be built using the msbuild or xbuild tools.

The project depends on certain libraries which should be placed in a "references" folder (at the same level as the solution file).

From the [Castle project][4] you will need:

  * Castle.Core.dll
  * Castle.DynamicProxy2.dll

From [db4o][2] you will need:

  * Db4objects.Db4o.dll
  * Db4objects.Db4o.Linq.dll
  * Mono.Reflection.dll

Once the references are in place the project can be built. It will output the Coincidental.dll along with the references to the
appropriate "bin" folder. These should all be copied/linked to where you intend to use them (they must all be available at run-time).


Coincidental API
----------------

**Warning: This is likely to change as Coincidental and Henge progress**

### Provider

Access to the database is through the Provider. It is intended that a single Provider be used for access to the database and
should be shared across all threads. At the time of writing the Provider was not implemented as a singleton so it is possible
to use more than one at once, however, if you attempt to use multiple Provider instances to access the same database file it
will not be permitted (since db4o locks the file).


### Provider Functions

  * **bool** Initialise(**string** *connectionString*, **int** *activationDepth*)
    * *connectionString*: Should simply be the path to a db4o database.
    * *activationDepth*: Sets the activation depth to be used by db4o (automatically honoured by the Coincidental layer).
    * Returns true if successful and false if it has already been initialised.

	This should be called before attempting to use any other functions, an exception will occur if you do not!


  * T Store<T>(T *entity*)
    * *entity*: Object to be stored. The underlying db4o container will automatically store the entire object tree.
    * Returns a persistent instance of the object.

	Stores a transient object tree to the database. If a persistent object is passed in nothing will happen. Mixing persistent
	objects with transients here is not a good idea.


  * **bool** Delete(**object** *entity*)
    * *entity*: Persistent object to be deleted.
    * Returns true if successful and false if the object was not persistent.

	Delete a persistent object from the database.


  * **bool** Delete(IEnumerable<**object**> *entities*)
    * *entities*: An enumerable list of persistent objects.
    * Returns true if successful and false if any of the objects were not persistent.

	Delete a list of persistent objects from the database. This is more efficient than deleting the entities individually
	since it is performed in a single transaction.


  * IQueryable<T> Query<T>()
    * Returns a queryable interface.

	Provides the primary interface for querying the database. The queries are passed straight through to db4o but the results
	are intercepted and proxied as persistent objects.


  * T Get<T>(Func< T, **bool**> *expression*)
    * *expression*: A LINQ expression describing how to select the required entity.
    * Returns a persisted entity or null if no matching entity was found.

	Provides a simple way of retrieving a single entity from the database.

  * Lock Lock(**params object** [] *entities*)
    * *entities*: A variable number of persistent objects to be locked.
    * Returns a Lock instance which must be disposed.

    Keeps attempting to lock all of the supplied objects until it is successful and then returns an instance
    of Lock which will automatically unlock all of the objects when it is disposed. The simplest way of ensuring
    this is with the "using" syntax (see the example at the bottom).


  * **void** Flush()

	Coincidental automatically keeps track of which objects have been modified. When this function is called any modified
	objects will be automatically flushed to the db4o database. Coincidental also takes this opportunity to clean up its
	cache, removing object instances that have not been accessed for some time so that they may be garbage collected.

	It is not advisable to call this at the end of each web request, in fact for a web-based game it is best to allow it to work
	almost as an in-memory database with a service thread forcing occasional flushes to disk. At the cost of potentially losing
	changes if the application is forcibly closed it should be far more responsive.


### Persistent Objects

When a Store, Query or Get is performed, the result is a persistent object. They support transparent activation, so when
you access a property which is an object, list or dictionary it will be automatically loaded from the database. These objects also
support transparent persistence, so if they are modified then they will be written back to the database file during the next call
to Flush.

Coincidental works with plain old CLR objects, but they must use automatic virtual properties as follows:

	public class Entity
	{
		public virtual string Name				{ get; set; }
		public virtual DateTime Time			{ get; set; }
		public virtual Entity Reference			{ get; set; }
		public virtual IList<string> Strings	{ get; set; }

		public Entity()
		{
			this.Strings = new List<string>();
		}
	}

The use of non-virtual properties will cause problems (since they cannot be intercepted by the proxy). At this time Coincidental only
supports generic collections (lists and dictionaries) and the properties within an entity must be declared using the appropriate
interfaces (IList<> or IDictionary<,>). Be aware that when a list or dictionary is accessed it will be automatically activated so
please avoid having huge lists of objects stored within other objects.

If you attempt to modify any properties of an object without first locking it an exception will be thrown.


Example
-------

This is a quick example based around the Entity defined above:

	using System;
	using System.Linq;
	using System.Threading;

	using Coincidental;


	namespace CoincidentalTest
	{
		class MainClass
		{
			public static void Main(string [] args)
			{
				using (Provider db = new Provider())
				{
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

					using (Lock l = db.Lock(entity))
					{
						// This time it works because the object has been locked
						entity.Name = "NewName";
					}

					Console.WriteLine(entity.Name);
				}
			}
		}
	}


[1]:	http://
[2]:	http://www.db4o.com/
[3]:	http://www.castleproject.org/dynamicproxy/index.html
[4]:	http://www.castleproject.org