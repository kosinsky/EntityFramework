// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Query.Expressions;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public class RelationalProjectionExpressionVisitor : ProjectionExpressionVisitor
    {
        private readonly ISqlTranslatingExpressionVisitor _sqlTranslatingExpressionVisitor;

        private RelationalQueryModelVisitor _relationalQueryModelVisitor;
        private IQuerySource _querySource;

        public RelationalProjectionExpressionVisitor(
            [NotNull] ISqlTranslatingExpressionVisitor sqlTranslatingExpressionVisitor)
        {
            Check.NotNull(sqlTranslatingExpressionVisitor, nameof(sqlTranslatingExpressionVisitor));

            _sqlTranslatingExpressionVisitor = sqlTranslatingExpressionVisitor;
        }

        public override Expression VisitProjection(
            [NotNull] EntityQueryModelVisitor queryModelVisitor,
            [NotNull] IQuerySource querySource,
            [NotNull] Expression expression)
        {
            Check.NotNull(queryModelVisitor, nameof(queryModelVisitor));
            Check.NotNull(querySource, nameof(querySource));
            Check.NotNull(expression, nameof(expression));

            _relationalQueryModelVisitor = (RelationalQueryModelVisitor)queryModelVisitor;
            _querySource = querySource;

            return VisitQueryExpression(queryModelVisitor, expression);
        }

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            Check.NotNull(methodCallExpression, nameof(methodCallExpression));

            if (methodCallExpression.Method.IsGenericMethod)
            {
                var methodInfo = methodCallExpression.Method.GetGenericMethodDefinition();

                if (ReferenceEquals(methodInfo, EntityQueryModelVisitor.PropertyMethodInfo))
                {
                    var newArg0 = Visit(methodCallExpression.Arguments[0]);

                    if (newArg0 != methodCallExpression.Arguments[0])
                    {
                        return Expression.Call(
                            methodCallExpression.Method,
                            newArg0,
                            methodCallExpression.Arguments[1]);
                    }

                    return methodCallExpression;
                }
            }

            return base.VisitMethodCall(methodCallExpression);
        }

        protected override Expression VisitNew(NewExpression newExpression)
        {
            Check.NotNull(newExpression, nameof(newExpression));

            var newNewExpression = base.VisitNew(newExpression);

            var selectExpression = _relationalQueryModelVisitor.TryGetQuery(_querySource);

            if (selectExpression != null)
            {
                for (var i = 0; i < newExpression.Arguments.Count; i++)
                {
                    var aliasExpression
                        = selectExpression.Projection
                            .OfType<AliasExpression>()
                            .SingleOrDefault(ae => ae.SourceExpression == newExpression.Arguments[i]);

                    if (aliasExpression != null)
                    {
                        aliasExpression.SourceMember
                            = newExpression.Members?[i]
                                ?? (newExpression.Arguments[i] as MemberExpression)?.Member;
                    }
                }
            }

            return newNewExpression;
        }

        public override Expression Visit(Expression expression)
        {
            var selectExpression = _relationalQueryModelVisitor.TryGetQuery(_querySource);

            if (expression != null
                && !(expression is ConstantExpression)
                && selectExpression != null)
            {
                var sqlExpression
                    = _sqlTranslatingExpressionVisitor
                        .TranslateSql(
                            _relationalQueryModelVisitor,
                            expression,
                            selectExpression,
                            inProjection: true);

                if (sqlExpression == null)
                {
                    if (!(expression is QuerySourceReferenceExpression))
                    {
                        _relationalQueryModelVisitor.RequiresClientProjection = true;
                    }
                }
                else
                {
                    if (!(expression is NewExpression))
                    {
                        AliasExpression aliasExpression;

                        int index;

                        if (!(expression is QuerySourceReferenceExpression))
                        {
                            var columnExpression = sqlExpression.TryGetColumnExpression();

                            if (columnExpression != null)
                            {
                                index = selectExpression.AddToProjection(sqlExpression);

                                aliasExpression = selectExpression.Projection[index] as AliasExpression;

                                if (aliasExpression != null)
                                {
                                    aliasExpression.SourceExpression = expression;
                                }

                                return expression;
                            }
                        }

                        if (!(sqlExpression is ConstantExpression))
                        {
                            index = selectExpression.AddToProjection(sqlExpression);

                            aliasExpression = selectExpression.Projection[index] as AliasExpression;

                            if (aliasExpression != null)
                            {
                                aliasExpression.SourceExpression = expression;
                            }

                            return
                                QueryModelVisitor.BindReadValueMethod(
                                    expression.Type,
                                    QueryResultScope.GetResult(
                                        EntityQueryModelVisitor.QueryResultScopeParameter,
                                        _querySource,
                                        typeof(ValueBuffer)),
                                    index);
                        }
                    }
                }
            }

            return base.Visit(expression);
        }
    }
}
