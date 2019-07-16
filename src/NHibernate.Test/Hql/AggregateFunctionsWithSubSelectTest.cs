﻿using NHibernate.Cfg.MappingSchema;
using NHibernate.Dialect;
using NHibernate.Mapping.ByCode;
using NUnit.Framework;

namespace NHibernate.Test.Hql
{
	[TestFixture]
	public class AggregateFunctionsWithSubSelectTest : TestCaseMappingByCode
	{
		protected override HbmMapping GetMappings()
		{
			var mapper = new ModelMapper();
			mapper.Class<Person>(
				rc =>
				{
					rc.Id(x => x.Id, m => m.Generator(Generators.Native));
					rc.Property(x => x.Name);
					rc.Map(x => x.Localized, cm => cm.Cascade(Mapping.ByCode.Cascade.All), x => x.Element());
				});

			mapper.Class<Document>(
				rc =>
				{
					rc.Id(x => x.Id, m => m.Generator(Generators.Native));
					rc.Property(x => x.Name);
					rc.Map(x => x.Contacts, cm => cm.Key(k => k.Column("position")), x => x.OneToMany());
				});

			return mapper.CompileMappingForAllExplicitlyAddedEntities();
		}

		protected override void OnTearDown()
		{
			using (var session = OpenSession())
			using (var transaction = session.BeginTransaction())
			{
				session.Delete("from System.Object");

				session.Flush();
				transaction.Commit();
			}
		}

		protected override void OnSetUp()
		{
			using (var session = OpenSession())
			using (var transaction = session.BeginTransaction())
			{
				var document = new Document();
				var p1 = new Person();
				var p2 = new Person();

				p1.Localized.Add(1, "p1.1");
				p1.Localized.Add(2, "p1.2");
				p2.Localized.Add(1, "p2.1");
				p2.Localized.Add(2, "p2.2");

				document.Contacts.Add(1, p1);
				document.Contacts.Add(2, p2);

				session.Persist(p1);
				session.Persist(p2);
				session.Persist(document);

				transaction.Commit();
			}
		}

		protected override bool AppliesTo(Dialect.Dialect dialect)
		{
			return TestDialect.SupportsAggregateInSubSelect;
		}

		[TestCase("SUM", 4)]
		[TestCase("MIN", 2)]
		[TestCase("MAX", 2)]
		[TestCase("AVG", 2d)]
		public void TestAggregateFunction(string functionName, object result)
		{
			var query = "SELECT " +
			            "	d.Id, " +
						$"	{functionName}(" +
			            "		(" +
			            "			SELECT COUNT(localized) " +
			            "			FROM Person p " +
			            "			LEFT JOIN p.Localized localized " +
			            "			WHERE p.Id = c.Id" +
			            "		)" +
			            "	) AS LocalizedCount " +
			            "FROM Document d " +
			            "LEFT JOIN d.Contacts c " +
			            "GROUP BY d.Id";

			using (var session = OpenSession())
			using (var transaction = session.BeginTransaction())
			{
				var results = session.CreateQuery(query).List();

				Assert.That(results, Has.Count.EqualTo(1));
				var tuple = results[0] as object[];
				Assert.That(tuple, Is.Not.Null);
				Assert.That(tuple, Has.Length.EqualTo(2));
				Assert.That(tuple[1], Is.EqualTo(result));
				transaction.Commit();
			}
		}
	}
}