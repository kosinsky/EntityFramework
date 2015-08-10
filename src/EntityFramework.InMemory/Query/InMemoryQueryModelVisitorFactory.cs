// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.DependencyInjection;

namespace Microsoft.Data.Entity.Query
{
    public class InMemoryQueryModelVisitorFactory : IEntityQueryModelVisitorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public InMemoryQueryModelVisitorFactory([NotNull] IServiceProvider serviceProvider)
        {
            Check.NotNull(serviceProvider, nameof(serviceProvider));

            _serviceProvider = serviceProvider;
        }

        public virtual EntityQueryModelVisitor Create([NotNull] QueryCompilationContext queryCompilationContext)
        {
            Check.NotNull(queryCompilationContext, nameof(queryCompilationContext));

            var visitor = _serviceProvider.GetService<InMemoryQueryModelVisitor>();

            visitor.QueryCompilationContext = queryCompilationContext;

            return visitor;
        }

        public virtual EntityQueryModelVisitor Create(
            [NotNull] QueryCompilationContext queryCompilationContext,
            [NotNull] EntityQueryModelVisitor parentEntityQueryModelVisitor)
        {
            Check.NotNull(queryCompilationContext, nameof(queryCompilationContext));
            Check.NotNull(parentEntityQueryModelVisitor, nameof(parentEntityQueryModelVisitor));

            return Create(queryCompilationContext);
        }
    }
}
