// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq.Clauses;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public class RelationalProjectionExpressionVisitorFactory : IProjectionExpressionVisitorFactory
    {
        private readonly ISqlTranslatingExpressionVisitorFactory _sqlTranslatingExpressionVisitorFactory;

        public RelationalProjectionExpressionVisitorFactory(
            [NotNull] ISqlTranslatingExpressionVisitorFactory sqlTranslatingExpressionVisitorFactory)
        {
            Check.NotNull(sqlTranslatingExpressionVisitorFactory, nameof(sqlTranslatingExpressionVisitorFactory));

            _sqlTranslatingExpressionVisitorFactory = sqlTranslatingExpressionVisitorFactory;
        }

        public virtual ExpressionVisitor Create(
            [NotNull] EntityQueryModelVisitor queryModelVisitor,
            [NotNull] IQuerySource querySource)
            => new RelationalProjectionExpressionVisitor(
                _sqlTranslatingExpressionVisitorFactory,
                (RelationalQueryModelVisitor)Check.NotNull(queryModelVisitor, nameof(queryModelVisitor)),
                Check.NotNull(querySource, nameof(querySource)));
    }
}
