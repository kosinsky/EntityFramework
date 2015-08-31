﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Query.Expressions;
using Microsoft.Data.Entity.Query.ExpressionVisitors;
using Microsoft.Data.Entity.Query.ResultOperators;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Clauses.StreamedData;
using Remotion.Linq.Parsing.ExpressionVisitors.Transformation;
using Remotion.Linq.Parsing.ExpressionVisitors.TreeEvaluation;
using Remotion.Linq.Parsing.Structure;
using Remotion.Linq.Parsing.Structure.ExpressionTreeProcessors;
using Remotion.Linq.Parsing.Structure.NodeTypeProviders;

namespace Microsoft.Data.Entity.Query
{
    public class QueryCompiler : IQueryCompiler
    {
        private readonly ICompiledQueryCache _cache;
        private readonly ICompiledQueryCacheKeyGenerator _cacheKeyGenerator;
        private readonly IDatabase _database;

        private static MethodInfo CompileQueryMethod { get; }
            = typeof(IDatabase).GetTypeInfo().GetDeclaredMethod("CompileQuery");

        public QueryCompiler(
            [NotNull] ICompiledQueryCache cache,
            [NotNull] ICompiledQueryCacheKeyGenerator cacheKeyGenerator,
            [NotNull] IDatabase database)
        {
            Check.NotNull(cache, nameof(cache));
            Check.NotNull(cacheKeyGenerator, nameof(cacheKeyGenerator));
            Check.NotNull(database, nameof(database));

            _cache = cache;
            _cacheKeyGenerator = cacheKeyGenerator;
            _database = database;
        }

        public virtual Func<QueryContext, TResult> CompileQuery<TResult>([NotNull] Expression query)
        {
            Check.NotNull(query, nameof(query));

            return _cache.GetOrAddQuery(_cacheKeyGenerator.GenerateCacheKey(query, async: false), () =>
                {
                    var queryModel = CreateQueryParser().GetParsedQuery(query);

                    var resultItemType
                        = (queryModel.GetOutputDataInfo() as StreamedSequenceInfo)?.ResultItemType ?? typeof(TResult);

                    return MapQueryExecutor<TResult>(queryModel, resultItemType);
                });
        }

        public virtual Func<QueryContext, IAsyncEnumerable<TResult>> CompileAsyncQuery<TResult>([NotNull] Expression query)
        {
            Check.NotNull(query, nameof(query));

            return _cache.GetOrAddAsyncQuery(_cacheKeyGenerator.GenerateCacheKey(query, async: true), () =>
                {
                    var queryModel = CreateQueryParser().GetParsedQuery(query);

                    return _database.CompileAsyncQuery<TResult>(queryModel);
                });
        }

        private Func<QueryContext, TResult> MapQueryExecutor<TResult>(QueryModel queryModel, Type resultItemType)
        {
            if (resultItemType == typeof(TResult))
            {
                var compiledQuery = _database.CompileQuery<TResult>(queryModel);
                return qc => compiledQuery(qc).First();
            }
            else
            {
                try
                {
                    return (Func<QueryContext, TResult>)CompileQueryMethod
                        .MakeGenericMethod(resultItemType)
                        .Invoke(_database, new object[] { queryModel });
                }
                catch (TargetInvocationException e)
                {
                    ExceptionDispatchInfo.Capture(e.InnerException).Throw();

                    throw;
                }
            }
        }

        private static QueryParser CreateQueryParser()
            => new QueryParser(
                new ExpressionTreeParser(
                    _cachedNodeTypeProvider.Value,
                        new CompoundExpressionTreeProcessor(new IExpressionTreeProcessor[]
                        {
                            new PartialEvaluatingExpressionTreeProcessor(new NullEvaluatableExpressionFilter()),
                            new FunctionEvaluationEnablingProcessor(),
                            new TransformingExpressionTreeProcessor(ExpressionTransformerRegistry.CreateDefault())
                        })));


        private class ReadonlyNodeTypeProvider : INodeTypeProvider
        {
            private readonly INodeTypeProvider _nodeTypeProvider;

            public ReadonlyNodeTypeProvider(INodeTypeProvider nodeTypeProvider)
            {
                _nodeTypeProvider = nodeTypeProvider;
            }

            public bool IsRegistered(MethodInfo method) => _nodeTypeProvider.IsRegistered(method);

            public Type GetNodeType(MethodInfo method) => _nodeTypeProvider.GetNodeType(method);
        }

        private static readonly Lazy<ReadonlyNodeTypeProvider> _cachedNodeTypeProvider
            = new Lazy<ReadonlyNodeTypeProvider>(CreateNodeTypeProvider);

        private static ReadonlyNodeTypeProvider CreateNodeTypeProvider()
        {
            var methodInfoBasedNodeTypeRegistry = MethodInfoBasedNodeTypeRegistry.CreateFromRelinqAssembly();

            methodInfoBasedNodeTypeRegistry
                .Register(QueryAnnotationExpressionNode.SupportedMethods, typeof(QueryAnnotationExpressionNode));

            methodInfoBasedNodeTypeRegistry
                .Register(IncludeExpressionNode.SupportedMethods, typeof(IncludeExpressionNode));

            methodInfoBasedNodeTypeRegistry
                .Register(ThenIncludeExpressionNode.SupportedMethods, typeof(ThenIncludeExpressionNode));

            var innerProviders
                = new INodeTypeProvider[]
                {
                    methodInfoBasedNodeTypeRegistry,
                    MethodNameBasedNodeTypeRegistry.CreateFromRelinqAssembly()
                };

            return new ReadonlyNodeTypeProvider(new CompoundNodeTypeProvider(innerProviders));
        }

        private class NullEvaluatableExpressionFilter : EvaluatableExpressionFilterBase
        {
        }

        private class FunctionEvaluationEnablingProcessor : IExpressionTreeProcessor
        {
            public virtual Expression Process(Expression expressionTree)
                => new FunctionEvaluationEnablingVisitor().Visit(expressionTree);

            private class FunctionEvaluationEnablingVisitor : ExpressionVisitorBase
            {
                protected override Expression VisitExtension(Expression expression)
                {
                    var methodCallWrapper = expression as MethodCallEvaluationPreventingExpression;
                    if (methodCallWrapper != null)
                    {
                        return Visit(methodCallWrapper.MethodCall);
                    }

                    var propertyWrapper = expression as PropertyEvaluationPreventingExpression;
                    if (propertyWrapper != null)
                    {
                        return Visit(propertyWrapper.MemberExpression);
                    }

                    return propertyWrapper
                        != null ? Visit(propertyWrapper.MemberExpression)
                        : base.VisitExtension(expression);
                }

                protected override Expression VisitSubQuery(SubQueryExpression expression)
                {
                    var clonedModel = expression.QueryModel.Clone();

                    clonedModel.TransformExpressions(Visit);

                    return new SubQueryExpression(clonedModel);
                }
            }
        }
    }
}
