using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace LiteDB
{
    public abstract class LiteCollectionQueryProvider : IQueryProvider
    {

        public IQueryable CreateQuery(Expression expression)
        {
            Type elementType = TypeSystem.GetElementType(expression.Type);

            try
            {
                return (IQueryable)Activator.CreateInstance(typeof(LiteCollection<>).MakeGenericType(elementType), new object[] { this, expression });
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new LiteCollectionAsQueryable<TElement>(this, expression);
        }

        public object Execute(Expression expression)
        {
            return ExecuteExpression(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return (TResult)ExecuteExpression(expression);
        }

        public abstract object ExecuteExpression(Expression expression);
    }

    public class LiteDbCollectionQueryProvider<T> : LiteCollectionQueryProvider
    {
        private readonly LiteCollection<T> _target;
        private readonly BsonMapper _mapper;

        internal LiteDbCollectionQueryProvider(LiteCollection<T> target, BsonMapper mapper)
        {
            _target = target;
            _mapper = mapper;
        }

        public override object ExecuteExpression(Expression expression)
        {
            var query = new QueryVisitor<T>(_mapper).Visit(expression);
            return _target.Find(query);
        }
    }
}