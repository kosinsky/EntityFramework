// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.Query.ExpressionVisitors;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.Logging;

namespace Microsoft.Data.Entity.Query
{
    public class RelationalQueryCompilationContextFactory : IQueryCompilationContextFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IEntityQueryModelVisitorFactory _entityQueryModelVisitorFactory;
        private readonly IRequiresMaterializationExpressionVisitor _requiresMaterializationExpressionVisitor;

        public RelationalQueryCompilationContextFactory(
            [NotNull] ILoggerFactory loggerFactory,
            [NotNull] IEntityQueryModelVisitorFactory entityQueryModelVisitorFactory,
            [NotNull] IRequiresMaterializationExpressionVisitor requiresMaterializationExpressionVisitor)
        {
            Check.NotNull(loggerFactory, nameof(loggerFactory));
            Check.NotNull(entityQueryModelVisitorFactory, nameof(entityQueryModelVisitorFactory));
            Check.NotNull(requiresMaterializationExpressionVisitor, nameof(requiresMaterializationExpressionVisitor));

            _loggerFactory = loggerFactory;
            _entityQueryModelVisitorFactory = entityQueryModelVisitorFactory;
            _requiresMaterializationExpressionVisitor = requiresMaterializationExpressionVisitor;
        }

        public virtual QueryCompilationContext Create(bool async)
            => async
                ? new RelationalQueryCompilationContext(
                    _loggerFactory,
                    _entityQueryModelVisitorFactory,
                    _requiresMaterializationExpressionVisitor,
                    new AsyncLinqOperatorProvider(),
                    new AsyncQueryMethodProvider())
                : new RelationalQueryCompilationContext(
                    _loggerFactory,
                    _entityQueryModelVisitorFactory,
                    _requiresMaterializationExpressionVisitor,
                    new LinqOperatorProvider(),
                    new QueryMethodProvider());
    }
}
