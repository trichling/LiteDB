using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LiteDB;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace apetito.FreshAndFrozen.Tests
{
    [TestClass]
    public class LiteDbQueryableTests
    {
        [ClassInitialize]
        public static void TestClassInitialize(TestContext context)
        {
            if (Directory.Exists(@"Data\ArticleQuery"))
                Directory.Delete(@"Data\ArticleQuery", true);

            Directory.CreateDirectory(@"Data\ArticleQuery");

            using (var db = new LiteDatabase(@"Filename=Data\ArticleQuery\queryable.db"))
            {
                db.GetCollection<Article>().Insert(new Article() { ArticleNumber = 1, Category = "Test", Allergeens = new List<Allergeen> { new Allergeen() { Code = "A" } }, Supplier = new SupplierInformation() { Name = "Klaus" } });
                db.GetCollection<Article>().Insert(new Article() { ArticleNumber = 2, Category = "Demo", Allergeens = new List<Allergeen> { new Allergeen() { Code = "B" } }, Supplier = new SupplierInformation() { Name = "Peter" } });
                db.GetCollection<Article>().Insert(new Article() { ArticleNumber = 3, Category = "Test", Allergeens = new List<Allergeen> { new Allergeen() { Code = "A" }, new Allergeen() { Code = "B" } }, Supplier = new SupplierInformation() { Name = "Igor" } });
                db.GetCollection<Article>().Insert(new Article() { ArticleNumber = 4, Category = "AllergeneFree", Allergeens = new List<Allergeen>(), Supplier = new SupplierInformation() { Name = "Heinz" } });
            }
        }

        [ClassCleanup]
        public static void TestClassCleanup()
        {
            Directory.Delete(@"Data\ArticleQuery", true);
        }

        [TestMethod]
        [TestCategory("Unit Test")]
        public void LiteDbQueryable_StackQueries()
        {
            IQueryable<Article> query;
            using (var db = new LiteDatabase(@"Filename=Data\ArticleQuery\queryable.db"))
            {
                query = db.GetCollection<Article>().AsQueryable();
                query = query.Where(a => a.Category.Contains("e"));
                query = query.Where(a => a.Category.Contains("s"));

                var results = query.ToList();

                Assert.IsNotNull(results);
                Assert.AreEqual(2, results.Count);
            }


        }

        [TestMethod]
        [TestCategory("Unit Test")]
        public void LiteDbQueryable_ContainsQuery()
        {
            using (var db = new LiteDatabase(@"Filename=Data\ArticleQuery\queryable.db"))
            {
                var query = db.GetCollection<Article>().AsQueryable().Where(a => a.Category.Contains("es"));
                var results = query.ToList();

                Assert.IsNotNull(results);
                Assert.AreEqual(2, results.Count);
                Assert.AreEqual(1, results[0].ArticleNumber);
                Assert.AreEqual(3, results[1].ArticleNumber);
            }

        }

        [TestMethod]
        [TestCategory("Unit Test")]
        public void LiteDbQueryable_AndQuery()
        {
            using (var db = new LiteDatabase(@"Filename=Data\ArticleQuery\queryable.db"))
            {
                var query = db.GetCollection<Article>()
                    .AsQueryable()
                    .Where(a => a.ArticleNumber == 1 && a.Category == "Test");

                var results = query.ToList();

                Assert.IsNotNull(results);
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(1, results[0].ArticleNumber);
                Assert.AreEqual("Test", results[0].Category);
            }

        }

        [TestMethod]
        [TestCategory("Unit Test")]
        public void LiteDbQueryable_OrQuery()
        {
            var db = new LiteDatabase(@"Filename=Data\ArticleQuery\queryable.db");
            var query = db.GetCollection<Article>().AsQueryable()
                .Where(a => a.ArticleNumber == 1 || a.ArticleNumber == 3);

            var results = query.ToList();

            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(1, results[0].ArticleNumber);
            Assert.AreEqual(3, results[1].ArticleNumber);
        }

        [TestMethod]
        [TestCategory("Unit Test")]
        public void LiteDbQueryable_NotQuery()
        {
            using (var db = new LiteDatabase(@"Filename=Data\ArticleQuery\queryable.db"))
            {
                var query = db.GetCollection<Article>()
                    .AsQueryable()
                    .Where(a => a.ArticleNumber != 1);

                var results = query.ToList();

                Assert.IsNotNull(results);
                Assert.AreEqual(2, results.Count);
                Assert.AreEqual(2, results[0].ArticleNumber);
                Assert.AreEqual(3, results[1].ArticleNumber);
            }

        }

        [TestMethod]
        [TestCategory("Unit Test")]
        public void LiteDbQueryable_InQuery()
        {
            using (var db = new LiteDatabase(@"Filename=Data\ArticleQuery\queryable.db"))
            {
                var searchValues = new[] { 1, 2 };
                var query = db.GetCollection<Article>()
                    .AsQueryable()
                    .Where(a => searchValues.Contains(a.ArticleNumber));

                var results = query.ToList();

                Assert.IsNotNull(results);
                Assert.AreEqual(2, results.Count);
                Assert.AreEqual(1, results[0].ArticleNumber);
                Assert.AreEqual(2, results[1].ArticleNumber);
            }

        }

        [TestMethod]
        [TestCategory("Unit Test")]
        public void LiteDbQueryable_InQueryWithImplicitArray()
        {
            using (var db = new LiteDatabase(@"Filename=Data\ArticleQuery\queryable.db"))
            {
                var query = db.GetCollection<Article>()
                    .AsQueryable()
                    .Where(a => new[] { 1, 2 }.Contains(a.ArticleNumber));

                var results = query.ToList();

                Assert.IsNotNull(results);
                Assert.AreEqual(2, results.Count);
                Assert.AreEqual(1, results[0].ArticleNumber);
                Assert.AreEqual(2, results[1].ArticleNumber);
            }

        }

        [TestMethod]
        [TestCategory("Unit Test")]
        public void LiteDbQueryable_NotInQuery()
        {
            using (var db = new LiteDatabase(@"Filename=Data\ArticleQuery\queryable.db"))
            {
                var searchValues = new[] { 1, 3 };

                var query = db.GetCollection<Article>()
                    .AsQueryable()
                    .Where(a => !searchValues.Contains(a.ArticleNumber));

                var results = query.ToList();

                Assert.IsNotNull(results);
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(2, results[0].ArticleNumber);
            }

        }

        [TestMethod]
        [TestCategory("Unit Test")]
        public void LiteDbQueryable_AnyWithConditionQuery()
        {
            var db = new LiteDatabase(@"Filename=Data\ArticleQuery\queryable.db");
            var searchValues = new[] { 1, 3 };
            var query = db.GetCollection<Article>().AsQueryable()
                .Where(a => a.Allergeens.Any(x => x.Code == "A"));

            var results = query.ToList();

            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Count);
        }

        [TestMethod]
        [TestCategory("Unit Test")]
        public void LiteDbQueryable_AnyEnumerableQuery()
        {
            using (var db = new LiteDatabase(@"Filename=Data\ArticleQuery\queryable.db"))
            {
                var searchValues = new[] { 1, 3 };
                var query = db.GetCollection<Article>()
                    .AsQueryable()
                    .Where(a => searchValues.Any(x => a.ArticleNumber == x));

                var results = query.ToList();

                Assert.IsNotNull(results);
                Assert.AreEqual(2, results.Count);
                Assert.AreEqual(1, results[0].ArticleNumber);
                Assert.AreEqual(3, results[1].ArticleNumber);
            }
        }

        // Not supported by liteDb
        //[TestMethod]
        //[TestCategory("Unit Test")]
        //public void LiteDbQueryable_AnyQuery()
        //{
        //    var db = new LiteDatabase(@"Filename=Data\ArticleQuery\queryable.db");
        //    var searchValues = new[] { 1, 3 };
        //    var query = db.GetCollection<Article>().AsQueryable()
        //        .Where(a => a.Allergeens.Any());

        //    var results = query.ToList();

        //    Assert.IsNotNull(results);
        //    Assert.AreEqual(3, results.Count);
        //}

        [TestMethod]
        [TestCategory("Unit Test")]
        public void LiteDbQueryable_InQueryWithNestedPropertyName()
        {
            using (var db = new LiteDatabase(@"Filename=Data\ArticleQuery\queryable.db"))
            {
                var searchValues = new[] { "Klaus", "Peter" };
                var query = db.GetCollection<Article>()
                    .AsQueryable()
                    .Where(a => searchValues.Contains(a.Supplier.Name));

                var results = query.ToList();

                Assert.AreEqual(2, results.Count);
                Assert.AreEqual(1, results[0].ArticleNumber);
                Assert.AreEqual(2, results[1].ArticleNumber);
            }
        }

        [TestMethod]
        [TestCategory("Unit Test")]
        public void LiteDbQueryable_ListContainsValuesFromList()
        {
            IQueryable<Article> query = null;
            var searchAllergens = new[] { "A", "B" };

            using (var db0 = new LiteDatabase(@"Filename=Data\ArticleQuery\queryable.db"))
            {
                query = db0.GetCollection<Article>()
                    .AsQueryable()
                    .Where(a => searchAllergens.Any(y => a.Allergeens.Select(x => x.Code).Contains(y)));

                //var query = db0.GetCollection<Article>("articles").AsQueryable(db0.Mapper)
                //    .Where(a => a.Allergeens.Select(x => x.Code).Contains("A") ||
                //                a.Allergeens.Select(x => x.Code).Contains("B"));
                //.Or(l => l.Equal(a => a.Allergeens.Select(x => x.Code), "A"), r => r.Equal(a => a.Allergeens.Select(x => x.Code), "B"));
            }

            var results = query.ToList();



            using (var db1 = new LiteDatabase(@"Filename=Data\ArticleQuery\query.db"))
            {
                var articles = db1.GetCollection<Article>().Find(a => searchAllergens.Any(y => a.Allergeens.Select(x => x.Code).Contains(y))).ToList();

                Assert.AreEqual(results.Count, articles.Count);
            }
        }

        //[TestMethod]
        //[TestCategory("Unit Test")]
        //public void LiteDbQueryable_AnyQuery()
        //{
        //    var db0 = new LiteDbHawaDatabase(@"Filename=Data\ArticleQuery\queryable.db");
        //    var searchAllergens = new[] { "A", "B" };
        //    var query = db0.GetCollection<Article>("articles").AsQueryable(db0.Mapper)
        //        .Where(a => )
        //        //.Any( searchAllergens, (c, v) => c.Equal(a => a.Allergeens.Select(x => x.Code), v));
        //    var results = query.Execute(out var overallResults);

        //    Assert.AreEqual(3, results.Count);
        //}

        //[TestMethod]
        //[TestCategory("Unit Test")]
        //public void LiteDbQueryable_AllQuery()
        //{
        //    var db = new LiteDbHaWaRepository(@"Filename=Data\ArticleQuery\query.db") as IArticleRepository;
        //    var searchAllergens = new[] { "A", "B" };
        //    var query = db.Instance.Query().All(searchAllergens, (c, v) => c.Equal(a => a.Allergeens.Select(x => x.Code), v));
        //    var results = query.Execute(out var overallResults);

        //    Assert.AreEqual(1, results.Count);
        //    Assert.AreEqual(3, results[0].ArticleNumber);
        //}

    }

    public class SupplierInformation
    {
        public string Name { get; set; }
    }

    public class Allergeen
    {
        public string Code { get; set; }
    }

    public class Article
    {
        [BsonId]
        public int ArticleNumber { get; set; }
        public string Category { get; set; }
        public List<Allergeen> Allergeens { get; set; }
        public SupplierInformation Supplier { get; set; }
    }
}
