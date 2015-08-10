// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.DependencyInjection;
using Remotion.Linq.Clauses;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public class QueryFlatteningExpressionVisitorFactory : IQueryFlatteningExpressionVisitorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public QueryFlatteningExpressionVisitorFactory([NotNull] IServiceProvider serviceProvider)
        {
            Check.NotNull(serviceProvider, nameof(serviceProvider));

            _serviceProvider = serviceProvider;
        }

        public virtual ExpressionVisitor Create(
            [NotNull] IQuerySource outerQuerySource,
            [NotNull] IQuerySource innerQuerySource,
            [NotNull] RelationalQueryCompilationContext relationalQueryCompilationContext,
            int readerOffset,
            [NotNull] MethodInfo operatorToFlatten)
        {
            var visitor = _serviceProvider.GetService<QueryFlatteningExpressionVisitor>();

            visitor.Initialize(
                outerQuerySource,
                innerQuerySource,
                relationalQueryCompilationContext,
                readerOffset,
                operatorToFlatten);

            return visitor;
        }
    }
}
