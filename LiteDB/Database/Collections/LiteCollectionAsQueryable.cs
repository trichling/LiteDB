using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace LiteDB
{
    public class LiteCollectionAsQueryable<T> : IQueryable<T>
    {
        public LiteCollectionAsQueryable(LiteCollectionQueryProvider provider)
        {
            Expression = Expression.Constant(this);
            Provider = provider;
        }

        public LiteCollectionAsQueryable(LiteCollectionQueryProvider provider, Expression expression)
        {
            Provider = provider;
            Expression = expression;
        }

        public Expression Expression { get; set; }

        public Type ElementType => typeof(T);

        public IQueryProvider Provider { get; set; }

        public IEnumerator<T> GetEnumerator()
        {
            return Provider.Execute<IEnumerable<T>>(Expression).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Provider.Execute<IEnumerable>(Expression).GetEnumerator();
        }
    }
}