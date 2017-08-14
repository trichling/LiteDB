using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace LiteDB
{
    public sealed partial class LiteCollection<T>
    {
        private string _name;
        private LazyLoad<LiteEngine> _engine;
        private BsonMapper _mapper;
        private Logger _log;
        private List<Action<BsonDocument>> _includes;
        private QueryVisitor<T> _visitor;

        /// <summary>
        /// Get collection name
        /// </summary>
        public string Name { get { return _name; } }

        /// <summary>
        /// Returns visitor resolver query only for internals implementations
        /// </summary>
        internal QueryVisitor<T> Visitor { get { return _visitor; } }

        public LiteCollection(string name, LazyLoad<LiteEngine> engine, BsonMapper mapper, Logger log)
        {
            _name = name ?? mapper.ResolveCollectionName(typeof(T));
            _engine = engine;
            _mapper = mapper;
            _log = log;
            _visitor = new QueryVisitor<T>(mapper);
            _includes = new List<Action<BsonDocument>>();
        }

        public IQueryable<T> AsQueryable()
        {
            return new LiteCollectionAsQueryable<T>(new LiteDbCollectionQueryProvider<T>(this, _mapper));
        }
    }
}