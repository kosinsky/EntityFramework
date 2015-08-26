// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using JetBrains.Annotations;
using Remotion.Linq;
using Remotion.Linq.Clauses;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public interface IRequiresMaterializationExpressionVisitor
    {
        ISet<IQuerySource> FindQuerySourcesRequiringMaterialization(
            [NotNull] EntityQueryModelVisitor queryModelVisitor,
            [NotNull] QueryModel queryModel);
    }
}
