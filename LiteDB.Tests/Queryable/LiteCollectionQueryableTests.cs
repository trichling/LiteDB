using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LiteDB.Tests.Queryable
{
    [TestClass]
    public class LiteCollectionQueryableTests
    {

        [TestMethod]
        public void BasicWhereGreaterLess()
        {
            using (var file = new TempFile())
            {
                using (var db = new LiteDatabase(file.Filename))
                {
                    var data = db.GetCollection<TestData>("data");
                    var docs = GetDocs();

                    data.InsertBulk(docs);

                    var above500 = data.AsQueryable().Where(d => d.Id > 500).ToList();
                    Assert.AreEqual(499, above500.Count);

                    var below500 = data.AsQueryable().Where(d => d.Id < 500).ToList();
                    Assert.AreEqual(499, below500.Count);
                                     
                }
            }
        }

        [TestMethod]
        public void StackedQuery()
        {
            //var testDocs = GetDocs();
            //var stackedQueryList = testDocs.AsQueryable();
            //stackedQueryList = stackedQueryList.Where(d => d.Id > 500);
            //stackedQueryList = stackedQueryList.Where(d => d.Text.Contains("1"));
            //var stackedQueryListResult = stackedQueryList.ToList();


            using (var file = new TempFile())
            {
                using (var db = new LiteDatabase(file.Filename))
                {
                    var data = db.GetCollection<TestData>("data");
                    var docs = GetDocs();

                    data.InsertBulk(docs);

                    var stackedQuery = data.AsQueryable();
                    stackedQuery = stackedQuery.Where(d => d.Id < 500);
                    stackedQuery = stackedQuery.Where(d => d.Text.Contains("1"));

                    var stackedResult = stackedQuery.ToList();
                }
            }
        }
   

        private IEnumerable<TestData> GetDocs()
        {
            var result = new List<TestData>();
            for (int i = 1; i < 1000; i++)
            {
                result.Add(new TestData(i));
            }

            return result;
        }
    }

    public class TestData
    {
        public TestData()
        {

        }

        public TestData(int id)
        {
            Id = id;
        }

        [BsonId]
        public int Id { get; set; }

        public string Text => $"I am {Id}";

    }
}
