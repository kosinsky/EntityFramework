// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Query.Expressions;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public interface ISqlTranslatingExpressionVisitor
    {
        Expression TranslateSql(
            [NotNull] RelationalQueryModelVisitor queryModelVisitor,
            [NotNull] Expression expression,
            [CanBeNull] SelectExpression targetSelectExpression = null,
            [CanBeNull] Expression topLevelPredicate = null,
            bool bindParentQueries = false,
            bool inProjection = false);

        Expression ClientEvalPredicate { get; }
    }
}
