// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.Query.ExpressionVisitors;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.Logging;

namespace Microsoft.Data.Entity.Query
{
    public class InMemoryQueryCompilationContext : QueryCompilationContext
    {
        public InMemoryQueryCompilationContext(
            [NotNull] ILoggerFactory loggerFactory,
            [NotNull] IEntityQueryModelVisitorFactory entityQueryModelVisitorFactory,
            [NotNull] IRequiresMaterializationExpressionVisitor requiresMaterializationExpressionVisitor)
            : base(
                Check.NotNull(loggerFactory, nameof(loggerFactory)),
                Check.NotNull(entityQueryModelVisitorFactory, nameof(entityQueryModelVisitorFactory)),
                Check.NotNull(requiresMaterializationExpressionVisitor, nameof(requiresMaterializationExpressionVisitor)))
        {
        }

        public override EntityQueryModelVisitor CreateQueryModelVisitor(EntityQueryModelVisitor parentEntityQueryModelVisitor)
        {
            var inMemoryQueryModelVisitor =
                (InMemoryQueryModelVisitor)base.CreateQueryModelVisitor(parentEntityQueryModelVisitor);

            inMemoryQueryModelVisitor.QueryCompilationContext = this;

            return inMemoryQueryModelVisitor;
        }
    }
}
