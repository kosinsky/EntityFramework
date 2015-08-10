// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.DependencyInjection;
using Remotion.Linq.Clauses;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public class MemberAccessBindingExpressionVisitorFactory : IMemberAccessBindingExpressionVisitorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public MemberAccessBindingExpressionVisitorFactory([NotNull] IServiceProvider serviceProvider)
        {
            Check.NotNull(serviceProvider, nameof(serviceProvider));

            _serviceProvider = serviceProvider;
        }

        public virtual ExpressionVisitor Create(
            [NotNull] QuerySourceMapping querySourceMapping,
            [NotNull] EntityQueryModelVisitor queryModelVisitor,
            bool inProjection)
        {
            Check.NotNull(querySourceMapping, nameof(querySourceMapping));
            Check.NotNull(queryModelVisitor, nameof(queryModelVisitor));

            var visitor = _serviceProvider.GetService<MemberAccessBindingExpressionVisitor>();

            visitor.Initialize(querySourceMapping, queryModelVisitor, inProjection);

            return visitor;
        }
    }
}
