// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Entity.Storage;
using Remotion.Linq.Parsing;

namespace Microsoft.Data.Entity.Query
{
    public class RelationalQueryProvider : EntityQueryProvider
    {
        public RelationalQueryProvider(DbContext context, IDatabase database, ICompiledQueryCache compiledQueryCache, IQueryContextFactory queryContextFactory)
            : base(context, database, compiledQueryCache, queryContextFactory)
        {
        }

        public override TResult Execute<TResult>(Expression expression)
        {
            var newExpression = ConfigureNullSemantics(expression);

            return base.Execute<TResult>(newExpression);
        }

        public override IAsyncEnumerable<TResult> ExecuteAsync<TResult>(Expression expression)
        {
            var newExpression = ConfigureNullSemantics(expression);

            return base.ExecuteAsync<TResult>(expression);
        }

        public override Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            var newExpression = ConfigureNullSemantics(expression);

            return base.ExecuteAsync<TResult>(expression, cancellationToken);
        }

        private Expression ConfigureNullSemantics(Expression expression)
        {
            var newExpression = expression;
            //if (Context.Database.AsRelational().UseDatabaseNullSemantics)
            {
                var myVisitor = new MyVisitor();
                newExpression = myVisitor.Visit(expression);
            }

            return newExpression;
        }

        private class MyVisitor : RelinqExpressionVisitor
        {
            protected override Expression VisitConstant(ConstantExpression node)
            {
                return base.VisitConstant(node);
            }
        }
    }
}
