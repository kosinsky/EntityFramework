// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Query.Expressions;
using Microsoft.Data.Entity.Query.Sql;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq.Clauses;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public class IncludeExpressionVisitor : ExpressionVisitorBase, IIncludeExpressionVisitor
    {
        private readonly IMaterializerFactory _materializerFactory;
        private readonly ICommandBuilderFactory _commandBuilderFactory;
        private readonly IRelationalMetadataExtensionProvider _relationalMetadataExtensionProvider;
        private readonly ISqlQueryGeneratorFactory _sqlQueryGeneratorFactory;

        private IQuerySource _querySource;
        private IReadOnlyList<INavigation> _navigationPath;
        private RelationalQueryCompilationContext _queryCompilationContext;
        private IReadOnlyList<int> _queryIndexes;
        private bool _querySourceRequiresTracking;

        private bool _foundCreateEntityForQuerySource;

        public IncludeExpressionVisitor(
            [NotNull] IMaterializerFactory materializerFactory,
            [NotNull] ICommandBuilderFactory commandBuilderFactory,
            [NotNull] IRelationalMetadataExtensionProvider relationalMetadataExtensionProvider,
            [NotNull] ISqlQueryGeneratorFactory sqlQueryGeneratorFactory)
        {
            Check.NotNull(materializerFactory, nameof(materializerFactory));
            Check.NotNull(commandBuilderFactory, nameof(commandBuilderFactory));
            Check.NotNull(relationalMetadataExtensionProvider, nameof(relationalMetadataExtensionProvider));
            Check.NotNull(sqlQueryGeneratorFactory, nameof(sqlQueryGeneratorFactory));

            _materializerFactory = materializerFactory;
            _commandBuilderFactory = commandBuilderFactory;
            _relationalMetadataExtensionProvider = relationalMetadataExtensionProvider;
            _sqlQueryGeneratorFactory = sqlQueryGeneratorFactory;
        }

        public virtual Expression VisitInclude(
            [NotNull] IQuerySource querySource,
            [NotNull] IReadOnlyList<INavigation> navigationPath,
            [NotNull] RelationalQueryCompilationContext queryCompilationContext,
            [NotNull] IReadOnlyList<int> queryIndexes,
            [NotNull] Expression expression,
            bool querySourceRequiresTracking)
        {
            Check.NotNull(querySource, nameof(querySource));
            Check.NotNull(navigationPath, nameof(navigationPath));
            Check.NotNull(queryCompilationContext, nameof(queryCompilationContext));
            Check.NotNull(queryIndexes, nameof(queryIndexes));
            Check.NotNull(expression, nameof(expression));

            _querySource = querySource;
            _navigationPath = navigationPath;
            _queryCompilationContext = queryCompilationContext;
            _queryIndexes = queryIndexes;
            _querySourceRequiresTracking = querySourceRequiresTracking;

            return Visit(expression);
        }

        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            Check.NotNull(expression, nameof(expression));

            if (expression.Method.MethodIsClosedFormOf(RelationalEntityQueryableExpressionVisitor.CreateEntityMethodInfo)
                && ((ConstantExpression)expression.Arguments[0]).Value == _querySource)
            {
                _foundCreateEntityForQuerySource = true;
            }

            if (expression.Method.MethodIsClosedFormOf(_queryCompilationContext.QueryMethodProvider.ShapedQueryMethod))
            {
                _foundCreateEntityForQuerySource = false;

                var newExpression = base.VisitMethodCall(expression);

                if (_foundCreateEntityForQuerySource)
                {
                    return
                        Expression.Call(
                            _queryCompilationContext.QueryMethodProvider.IncludeMethod
                                .MakeGenericMethod(expression.Method.GetGenericArguments()[0]),
                            Expression.Convert(expression.Arguments[0], typeof(RelationalQueryContext)),
                            expression,
                            Expression.Constant(_querySource),
                            Expression.Constant(_navigationPath),
                            Expression.NewArrayInit(
                                _queryCompilationContext.QueryMethodProvider.IncludeRelatedValuesFactoryType,
                                CreateIncludeRelatedValuesStrategyFactories(_querySource, _navigationPath)),
                            Expression.Constant(_querySourceRequiresTracking));
                }

                return newExpression;
            }

            return base.VisitMethodCall(expression);
        }

        private IEnumerable<Expression> CreateIncludeRelatedValuesStrategyFactories(
            IQuerySource querySource,
            IEnumerable<INavigation> navigationPath)
        {
            var selectExpression
                = _queryCompilationContext.FindSelectExpression(querySource);

            var targetTableExpression
                = selectExpression.GetTableForQuerySource(querySource);

            var canProduceInnerJoin = true;
            var navigationCount = 0;
            foreach (var navigation in navigationPath)
            {
                var queryIndex = _queryIndexes[navigationCount];
                navigationCount++;

                var targetEntityType = navigation.GetTargetType();
                var targetTableName = _relationalMetadataExtensionProvider.For(targetEntityType).TableName;
                var targetTableAlias = targetTableName[0].ToString().ToLower();

                if (!navigation.IsCollection())
                {
                    var joinedTableExpression
                        = new TableExpression(
                            targetTableName,
                            _relationalMetadataExtensionProvider.For(targetEntityType).Schema,
                            targetTableAlias,
                            querySource);

                    var valueBufferOffset = selectExpression.Projection.Count;

                    canProduceInnerJoin
                        = canProduceInnerJoin
                          && (navigation.ForeignKey.IsRequired
                              && navigation.PointsToPrincipal());

                    var joinExpression
                        = canProduceInnerJoin
                            ? selectExpression
                                .AddInnerJoin(joinedTableExpression)
                            : selectExpression
                                .AddOuterJoin(joinedTableExpression);

                    var materializer
                        = _materializerFactory
                            .CreateMaterializer(
                                targetEntityType,
                                selectExpression,
                                (p, se) => se.AddToProjection(
                                    new AliasExpression(
                                        new ColumnExpression(
                                            _relationalMetadataExtensionProvider.For(p).ColumnName,
                                            p,
                                            joinedTableExpression))) - valueBufferOffset,
                                querySource: null);

                    joinExpression.Predicate
                        = BuildJoinEqualityExpression(
                            navigation,
                            navigation.PointsToPrincipal() ? targetTableExpression : joinExpression,
                            navigation.PointsToPrincipal() ? joinExpression : targetTableExpression,
                            querySource);

                    targetTableExpression = joinedTableExpression;

                    yield return
                        Expression.Lambda(
                            Expression.Call(
                                _queryCompilationContext.QueryMethodProvider
                                    .CreateReferenceIncludeRelatedValuesStrategyMethod,
                                Expression.Convert(
                                    EntityQueryModelVisitor.QueryContextParameter,
                                    typeof(RelationalQueryContext)),
                                Expression.Constant(valueBufferOffset),
                                Expression.Constant(queryIndex),
                                materializer));
                }
                else
                {
                    var principalTable
                        = selectExpression.Tables.Last(t => t.QuerySource == querySource);

                    foreach (var property in navigation.ForeignKey.PrincipalKey.Properties)
                    {
                        selectExpression
                            .AddToOrderBy(
                                _relationalMetadataExtensionProvider.For(property).ColumnName,
                                property,
                                principalTable,
                                OrderingDirection.Asc);
                    }

                    var targetSelectExpression = new SelectExpression();

                    targetTableExpression
                        = new TableExpression(
                            targetTableName,
                            _relationalMetadataExtensionProvider.For(targetEntityType).Schema,
                            targetTableAlias,
                            querySource);

                    targetSelectExpression.AddTable(targetTableExpression);

                    var materializer
                        = _materializerFactory
                            .CreateMaterializer(
                                targetEntityType,
                                targetSelectExpression,
                                (p, se) => se.AddToProjection(
                                    _relationalMetadataExtensionProvider.For(p).ColumnName,
                                    p,
                                    querySource),
                                querySource: null);

                    var innerJoinSelectExpression
                        = selectExpression.Clone(
                            selectExpression.OrderBy
                                .Select(o => o.Expression)
                                .Last(o => o.IsAliasWithColumnExpression())
                                .TryGetColumnExpression().TableAlias);

                    innerJoinSelectExpression.IsDistinct = true;
                    innerJoinSelectExpression.ClearProjection();

                    var innerJoinExpression = targetSelectExpression.AddInnerJoin(innerJoinSelectExpression);

                    LiftOrderBy(innerJoinSelectExpression, targetSelectExpression, innerJoinExpression);

                    innerJoinExpression.Predicate
                        = BuildJoinEqualityExpression(
                            navigation,
                            targetTableExpression,
                            innerJoinExpression,
                            querySource);

                    selectExpression = targetSelectExpression;

                    yield return
                        Expression.Lambda(
                            Expression.Call(
                                _queryCompilationContext.QueryMethodProvider
                                    .CreateCollectionIncludeRelatedValuesStrategyMethod,
                                Expression.Call(
                                    _queryCompilationContext.QueryMethodProvider.QueryMethod,
                                    EntityQueryModelVisitor.QueryContextParameter,
                                    Expression.Constant(
                                        _commandBuilderFactory.Create(
                                            () => _sqlQueryGeneratorFactory
                                                .CreateGenerator(targetSelectExpression))),
                                    Expression.Constant(queryIndex, typeof(int?))),
                                materializer));
                }
            }
        }

        private static void LiftOrderBy(
            SelectExpression innerJoinSelectExpression,
            SelectExpression targetSelectExpression,
            TableExpressionBase innerJoinExpression)
        {
            foreach (var ordering in innerJoinSelectExpression.OrderBy)
            {
                var orderingExpression = ordering.Expression;

                var aliasExpression = ordering.Expression as AliasExpression;

                if (aliasExpression?.Alias != null)
                {
                    var columnExpression = aliasExpression.TryGetColumnExpression();

                    if (columnExpression != null)
                    {
                        orderingExpression
                            = new ColumnExpression(
                                aliasExpression.Alias,
                                columnExpression.Property,
                                columnExpression.Table);
                    }
                }

                var index = innerJoinSelectExpression.AddToProjection(orderingExpression);

                var expression = innerJoinSelectExpression.Projection[index];

                var newExpression
                    = targetSelectExpression.UpdateColumnExpression(expression, innerJoinExpression);

                targetSelectExpression.AddToOrderBy(new Ordering(newExpression, ordering.OrderingDirection));
            }

            innerJoinSelectExpression.ClearOrderBy();
        }

        private Expression BuildJoinEqualityExpression(
            INavigation navigation,
            TableExpressionBase targetTableExpression,
            TableExpressionBase joinExpression,
            IQuerySource querySource)
        {
            Expression joinPredicateExpression = null;

            var targetTableProjections = ExtractProjections(targetTableExpression).ToList();
            var joinTableProjections = ExtractProjections(joinExpression).ToList();

            for (var i = 0; i < navigation.ForeignKey.Properties.Count; i++)
            {
                var principalKeyProperty = navigation.ForeignKey.PrincipalKey.Properties[i];
                var foreignKeyProperty = navigation.ForeignKey.Properties[i];

                var foreignKeyColumnExpression
                    = BuildColumnExpression(targetTableProjections, targetTableExpression, foreignKeyProperty, querySource);

                var primaryKeyColumnExpression
                    = BuildColumnExpression(joinTableProjections, joinExpression, principalKeyProperty, querySource);

                var primaryKeyExpression = primaryKeyColumnExpression;

                if (foreignKeyColumnExpression.Type != primaryKeyExpression.Type)
                {
                    if (foreignKeyColumnExpression.Type.IsNullableType()
                        && !primaryKeyExpression.Type.IsNullableType())
                    {
                        primaryKeyExpression
                            = Expression.Convert(primaryKeyExpression, foreignKeyColumnExpression.Type);
                    }
                    else if (primaryKeyExpression.Type.IsNullableType()
                             && !foreignKeyColumnExpression.Type.IsNullableType())
                    {
                        foreignKeyColumnExpression
                            = Expression.Convert(foreignKeyColumnExpression, primaryKeyColumnExpression.Type);
                    }
                }

                var equalExpression
                    = Expression.Equal(foreignKeyColumnExpression, primaryKeyExpression);

                joinPredicateExpression
                    = joinPredicateExpression == null
                        ? equalExpression
                        : Expression.AndAlso(joinPredicateExpression, equalExpression);
            }

            return joinPredicateExpression;
        }

        private Expression BuildColumnExpression(
            IReadOnlyCollection<Expression> projections,
            TableExpressionBase tableExpression,
            IProperty property,
            IQuerySource querySource)
        {
            Check.NotNull(property, nameof(property));

            if (projections.Count == 0)
            {
                return new ColumnExpression(
                    _relationalMetadataExtensionProvider.For(property).ColumnName,
                    property,
                    tableExpression);
            }

            var aliasExpressions
                = projections
                    .OfType<AliasExpression>()
                    .Where(p => p.TryGetColumnExpression()?.Property == property)
                    .ToList();

            var aliasExpression
                = aliasExpressions.Count == 1
                    ? aliasExpressions[0]
                    : aliasExpressions.Last(ae => ae.TryGetColumnExpression().Table.QuerySource == querySource);

            return new ColumnExpression(
                aliasExpression.Alias ?? aliasExpression.TryGetColumnExpression().Name,
                property,
                tableExpression);
        }

        private static IEnumerable<Expression> ExtractProjections(TableExpressionBase tableExpression)
        {
            var selectExpression = tableExpression as SelectExpression;

            if (selectExpression != null)
            {
                return selectExpression.Projection.ToList();
            }

            var joinExpression = tableExpression as JoinExpressionBase;

            return joinExpression != null
                ? ExtractProjections(joinExpression.TableExpression)
                : Enumerable.Empty<Expression>();
        }
    }
}
