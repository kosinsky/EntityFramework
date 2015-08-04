// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Query.ExpressionVisitors;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq.Parsing.ExpressionVisitors.TreeEvaluation;

namespace Microsoft.Data.Entity.Query
{
    public class QueryPreprocessor : IQueryPreprocessor
    {
        public virtual Expression Preprocess([NotNull] Expression query, [NotNull] QueryContext queryContext)
        {
            Check.NotNull(query, nameof(query));
            Check.NotNull(queryContext, nameof(queryContext));

            var annotatedQuery = new QueryAnnotatingExpressionVisitor()
                .Visit(query);

            var functionEvaluationDisabledExpression = new FunctionEvaluationDisablingVisitor()
                .Visit(annotatedQuery);

            var partialEvaluationInfo = EvaluatableTreeFindingExpressionVisitor
                .Analyze(functionEvaluationDisabledExpression, new NullEvaluatableExpressionFilter());

            var parameterExtractedExpression = new ParameterExtractingExpressionVisitor(partialEvaluationInfo, queryContext)
                .Visit(functionEvaluationDisabledExpression);

            return parameterExtractedExpression;
        }

        private class NullEvaluatableExpressionFilter : EvaluatableExpressionFilterBase
        {
        }
    }
}
