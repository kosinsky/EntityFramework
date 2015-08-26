// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public class OrderingExpressionVisitor : DefaultQueryExpressionVisitor, IOrderingExpressionVisitor
    {
        public virtual Expression VisitOrder(
            [NotNull] EntityQueryModelVisitor queryModelVisitor,
            [NotNull] Expression expression)
        {
            Check.NotNull(queryModelVisitor, nameof(queryModelVisitor));
            Check.NotNull(expression, nameof(expression));

            return VisitQueryExpression(queryModelVisitor, expression);
        }
    }
}
