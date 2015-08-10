// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.DependencyInjection;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public class EntityResultFindingExpressionVisitorFactory : IEntityResultFindingExpressionVisitorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public EntityResultFindingExpressionVisitorFactory([NotNull] IServiceProvider serviceProvider)
        {
            Check.NotNull(serviceProvider, nameof(serviceProvider));

            _serviceProvider = serviceProvider;
        }

        public virtual EntityResultFindingExpressionVisitor Create([NotNull] QueryCompilationContext queryCompilationContext)
        {
            Check.NotNull(queryCompilationContext, nameof(queryCompilationContext));

            var visitor = _serviceProvider.GetService<EntityResultFindingExpressionVisitor>();

            visitor.Initialize(queryCompilationContext);

            return visitor;
        }
    }
}
