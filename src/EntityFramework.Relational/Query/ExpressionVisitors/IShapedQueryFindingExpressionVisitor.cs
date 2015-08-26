// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using JetBrains.Annotations;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public interface IShapedQueryFindingExpressionVisitor
    {
        MethodCallExpression FindShapedQuery(
            [NotNull] RelationalQueryCompilationContext relationalQueryCompilationContext,
            [NotNull] Expression expression);
    }
}
