// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.DependencyInjection;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public class CompositePredicateExpressionVisitorFactory : ICompositePredicateExpressionVisitorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public CompositePredicateExpressionVisitorFactory([NotNull] IServiceProvider serviceProvider)
        {
            Check.NotNull(serviceProvider, nameof(serviceProvider));

            _serviceProvider = serviceProvider;
        }

        public virtual ExpressionVisitor Create(bool useRelationalNullSemantics)
        {
            var visitor = _serviceProvider.GetService<CompositePredicateExpressionVisitor>();

            visitor.Initialize(useRelationalNullSemantics);

            return visitor;
        }
    }
}
