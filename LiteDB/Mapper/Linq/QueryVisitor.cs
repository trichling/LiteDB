
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using ParameterDictionary = System.Collections.Generic.Dictionary<System.Linq.Expressions.ParameterExpression, LiteDB.BsonValue>;

namespace LiteDB
{
    internal class QueryVisitor<T>
    {
        private readonly BsonMapper _mapper;
        private readonly Type _type;
        private ParameterDictionary _parameters = new ParameterDictionary();
        private Stack<string> _fieldPrefixes = new Stack<string>();

        public QueryVisitor(BsonMapper mapper)
        {
            _mapper = mapper;
            _type = typeof(T);
        }


        public Query Visit(Expression expression)
        {
            if (expression.NodeType == ExpressionType.Quote)
                expression = StripQuotes(expression);

            var @switch = new Dictionary<Type, Func<Expression, Query>>
            {
                { typeof(MethodCallExpression), expr => Visit(expr as MethodCallExpression) },
                { typeof(ConstantExpression), expr => Visit(expr as ConstantExpression) },
                { typeof(LambdaExpression), expr => Visit(expr as LambdaExpression) },
                { typeof(MemberExpression), expr => Visit(expr as MemberExpression) },
                { typeof(BinaryExpression), expr => Visit(expr as BinaryExpression) },
                { typeof(UnaryExpression), expr => Visit(expr as UnaryExpression) },
                { typeof(InvocationExpression), expr => Visit(expr as InvocationExpression) },
            };

            var key = @switch.Keys.SingleOrDefault(t => t.IsAssignableFrom(expression.GetType()));

            if (key != null)
                return @switch[key](expression);

            throw new Exception();
        }

        public Expression StripQuotes(Expression e)
        {
            while (e.NodeType == ExpressionType.Quote)
            {
                e = ((UnaryExpression)e).Operand;
            }

            return e;
        }

        public Query Visit(MethodCallExpression expr)
        {

            var method = expr.Method.Name;
#if NET35
            var type = expr.Method.ReflectedType;
#else
            var type = expr.Method.DeclaringType;
#endif

            switch (method)
            {
                case "Where":
                    return WhereMethodCallExpression(expr);

                case "StartsWith":
                    return StartsWithMethodCallExpression(expr);

                case "Equals":
                    return EqualsMethodCallExpression(expr);

                case "Contains":
                    return ContainsMethodCallExpression(expr);

                case "Any":
                    return AnyMethodCallExpression(expr);

                case "All":
                    return AllMethodCallExpression(expr);

                default:
                    return EnumerableMethodCallExpression(expr);
            }

            throw new Exception();
        }

        private Query WhereMethodCallExpression(MethodCallExpression expr)
        {
            var left = Visit(expr.Arguments[0]);
            var right = Visit(expr.Arguments[1]);
            return Query.And(left, right);
        }

        private Query StartsWithMethodCallExpression(MethodCallExpression expr)
        {
            var value = this.GetValue(expr.Arguments[0]);
            return Query.StartsWith(this.GetField(expr.Object), value);
        }

        private Query EqualsMethodCallExpression(MethodCallExpression expr)
        {
            var value = this.GetValue(expr.Arguments[0]);
            return Query.EQ(this.GetField(expr.Object), value);
        }

        private Query ContainsMethodCallExpression(MethodCallExpression expr)
        {
#if NET35
            var type = expr.Method.ReflectedType;
#else
            var type = expr.Method.DeclaringType;
#endif
            if (type == typeof(string))
            {
                var field = this.GetField(expr.Object);
                var value = this.GetValue(expr.Arguments[0]);

                return Query.Contains(field, value);
            }

            if (type == typeof(Enumerable))
            {
                var paramType = GetArgument0Type(expr);

                if (paramType == ExpressionType.Parameter)
                {
                    // Contains (Enumerable): x.ListNumber.Contains(2)
                    var field = this.GetField(expr.Arguments[0]);
                    var value = this.GetValue(expr.Arguments[1]);
                    return Query.EQ(field, value);
                }
                else
                {
                    // Contains (enumerable) searchValues.Contains(x.Id)
                    var containsValues = this.GetValue(expr.Arguments[0]).AsArray;
                    var field = this.GetField(expr.Arguments[1]);
                    return Query.In(field, containsValues);
                }
            }

            throw new Exception();
            //return EnumerableMethodCallExpression(expr);
        }

        private Query AnyMethodCallExpression(MethodCallExpression expr)
        {
#if NET35
            var type = expr.Method.ReflectedType;
#else
            var type = expr.Method.DeclaringType;
#endif

            var paramType = GetArgument0Type(expr);

            if (type == typeof(Enumerable))
            {
                if (paramType == ExpressionType.Parameter)
                {
                    var field = this.GetField(expr.Arguments[0]);
                    var lambda = expr.Arguments[1] as LambdaExpression;

                    _fieldPrefixes.Push(field);
                    var query = this.Visit(lambda.Body);
                    _fieldPrefixes.Pop();
                    return query;
                }
                else
                {
                    return EnumerableMethodCallExpression(expr);
                }
            }


            throw new Exception();
        }

        private Query AllMethodCallExpression(MethodCallExpression expr)
        {
            throw new NotImplementedException();
        }

        private Query EnumerableMethodCallExpression(MethodCallExpression expr)
        {
            if (expr.Method.DeclaringType != typeof(Enumerable))
                throw new NotImplementedException("Cannot parse methods outside the System.Linq.Enumerable class.");

            var values = this.GetValue(expr.Arguments[0]).AsArray;
            var lambda = expr.Arguments[1] as LambdaExpression;
            var queries = new Query[values.Count];

            for (var i = 0; i < queries.Length; i++)
            {
                _parameters[lambda.Parameters[0]] = values[i];
                queries[i] = Visit(lambda.Body);
            }

            _parameters.Remove(lambda.Parameters[0]);

            if (expr.Method.Name == "Any")
            {
                return CreateOrQuery(ref queries);
            }
            else if (expr.Method.Name == "All")
            {
                return CreateAndQuery(ref queries);
            }

            throw new NotImplementedException("Not implemented System.Linq.Enumerable method");
        }

        public Query Visit(MemberExpression expr)
        {
            if (expr.Type == typeof(bool))
            {
                return Query.EQ(this.GetField(expr), new BsonValue(true));
            }

            throw new Exception();
        }

        public Query Visit(ConstantExpression expr)
        {
            if (typeof(IQueryable).IsAssignableFrom(expr.Type))
                return Query.All();

            if (typeof(bool).IsAssignableFrom(expr.Type))
                return (bool)expr.Value ? Query.All() : new QueryEmpty();

            throw new Exception();
        }

        public Query Visit(UnaryExpression expr)
        {
            if (expr.NodeType == ExpressionType.Not)
            {
                var query = Visit(expr.Operand);
                return Query.Not(query);
            }

            throw new Exception();
        }

        public Query Visit(LambdaExpression expr)
        {
            return Visit(expr.Body);
        }

        public Query Visit(BinaryExpression expr)
        {
            switch (expr.NodeType)
            {
                case ExpressionType.GreaterThanOrEqual:
                    return Query.GTE(GetField(expr.Left), GetValue(expr.Right, expr.Left));

                case ExpressionType.GreaterThan:
                    return Query.GT(GetField(expr.Left), GetValue(expr.Right, expr.Left));

                case ExpressionType.LessThan:
                    return Query.LT(GetField(expr.Left), GetValue(expr.Right, expr.Left));

                case ExpressionType.LessThanOrEqual:
                    return Query.LTE(GetField(expr.Left), GetValue(expr.Right, expr.Left));

                case ExpressionType.Equal:
                    return Query.EQ(GetField(expr.Left), GetValue(expr.Right, expr.Left));

                case ExpressionType.NotEqual:
                    return Query.Not(GetField(expr.Left), GetValue(expr.Right, expr.Left));

                case ExpressionType.AndAlso:
                    var andLeft = Visit(expr.Left);
                    var andRight = Visit(expr.Right);
                    return Query.And(andLeft, andRight);

                case ExpressionType.OrElse:
                    var orLeft = Visit(expr.Left);
                    var orRight = Visit(expr.Right);
                    return Query.Or(orLeft, orRight);
            }

            throw new Exception();
        }

        public Query Visit(InvocationExpression expr)
        {
            var lambda = expr.Expression as LambdaExpression;
            return this.Visit(lambda.Body);
        }

        public string GetField(Expression expr)
        {
            var prefix = BuildPrefix();
            var property = prefix + expr.GetPath();
            var parts = property.Split('.');
            var fields = new string[parts.Length];
            var type = _type;
            var isdbref = false;

            // loop "first.second.last"
            for (var i = 0; i < parts.Length; i++)
            {
                var entity = _mapper.GetEntityMapper(type);
                var part = parts[i];
                var prop = entity.Members.Find(x => x.MemberName == part);

                if (prop == null) throw LiteException.PropertyNotMapped(property);

                // if property is an IEnumerable, gets underlying type (otherwise, gets PropertyType)
                type = prop.UnderlyingType;

                fields[i] = prop.FieldName;

                if (prop.FieldName == "_id" && isdbref)
                {
                    isdbref = false;
                    fields[i] = "$id";
                }

                // if this property is DbRef, so if next property is _id, change to $id
                if (prop.IsDbRef) isdbref = true;
            }

            return string.Join(".", fields);
        }

        private string BuildPrefix()
        {
            var prefix = string.Empty;
            if (_fieldPrefixes.Any())
                prefix = string.Join(".", _fieldPrefixes.ToArray()) + ".";

            return prefix;
        }

        private BsonValue GetValue(Expression expr, Expression left = null)
        {
            // check if left side is an enum and convert to string before return
            Func<Type, object, BsonValue> convert = (type, value) =>
            {
                var enumType = (left as UnaryExpression) == null ? null : (left as UnaryExpression).Operand.Type;

                if (enumType != null && enumType.GetTypeInfo().IsEnum)
                {
                    var str = Enum.GetName(enumType, value);
                    return _mapper.Serialize(typeof(string), str, 0);
                }

                return _mapper.Serialize(type, value, 0);
            };

            // its a constant; Eg: "fixed string"
            if (expr is ConstantExpression)
            {
                var value = (expr as ConstantExpression);

                return convert(value.Type, value.Value);
            }
            else if (expr is MemberExpression && _parameters.Count > 0)
            {
                var mExpr = (MemberExpression)expr;
                var mValue = this.GetValue(mExpr.Expression, left);
                var value = mValue.AsDocument[mExpr.Member.Name];

                return convert(typeof(object), value);
            }
            else if (expr is ParameterExpression)
            {
                BsonValue result;
                if (_parameters.TryGetValue((ParameterExpression)expr, out result))
                {
                    return result;
                }
            }
            else if (expr is NewArrayExpression)
            {
                var values = (expr as NewArrayExpression).Expressions.Select(e => GetValue(e));
                return new BsonArray(values);
            }


            // execute expression
            var objectMember = Expression.Convert(expr, typeof(object));
            var getterLambda = Expression.Lambda<Func<object>>(objectMember);
            var getter = getterLambda.Compile();

            return convert(typeof(object), getter());
        }

        private Query CreateAndQuery(ref Query[] queries, int startIndex = 0)
        {
            var length = queries.Length - startIndex;

            if (length == 0) return new QueryEmpty();

            if (length == 1)
            {
                return queries[startIndex];
            }
            else
            {
                return Query.And(queries[startIndex], CreateOrQuery(ref queries, startIndex += 1));
            }
        }

        private Query CreateOrQuery(ref Query[] queries, int startIndex = 0)
        {
            var length = queries.Length - startIndex;

            if (length == 0) return new QueryEmpty();

            if (length == 1)
            {
                return queries[startIndex];
            }
            else
            {
                return Query.Or(queries[startIndex], CreateOrQuery(ref queries, startIndex += 1));
            }
        }

        private ExpressionType GetArgument0Type(MethodCallExpression expr)
        {
            if (expr.Arguments[0] is MemberExpression)
                return (expr.Arguments[0] as MemberExpression).Expression.NodeType;

            return expr.Arguments[0].NodeType;

        }

    }

}