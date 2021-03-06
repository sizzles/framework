﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Utilities.Reflection;
using Signum.Entities.DynamicQuery;
using Signum.Entities;
using System.Linq.Expressions;
using System.Reflection;
using Signum.Utilities;
using Signum.Utilities.ExpressionTrees;

namespace Signum.Engine.DynamicQuery
{
    public class AutoDynamicQueryCore<T> : DynamicQueryCore<T> 
    {
        public IQueryable<T> Query { get; private set; }
        
        Dictionary<string, Meta> metas;

        public AutoDynamicQueryCore(IQueryable<T> query)
        {
            this.Query = query;

            metas = DynamicQueryCore.QueryMetadata(Query);

            StaticColumns = MemberEntryFactory.GenerateList<T>(MemberOptions.Properties | MemberOptions.Fields)
              .Select((e, i) => new ColumnDescriptionFactory(i, e.MemberInfo, metas[e.MemberInfo.Name])).ToArray();
        }

        public override ResultTable ExecuteQuery(QueryRequest request)
        {
            request.Columns.Insert(0, new _EntityColumn(EntityColumnFactory().BuildColumnDescription(), QueryName));

            DQueryable<T> query = Query
                .ToDQueryable(GetQueryDescription())
                .SelectMany(request.Multiplications)
                .Where(request.Filters)
                .OrderBy(request.Orders)
                .Select(request.Columns);

            var result = query.TryPaginate(request.Pagination);

            return result.ToResultTable(request);
        }

        public override object ExecuteQueryValue(QueryValueRequest request)
        {
            var query = Query.ToDQueryable(GetQueryDescription())
                .SelectMany(request.Multiplications)
                .Where(request.Filters);
            
            if (request.ValueToken == null)
                return query.Query.Count();

            if (request.ValueToken is AggregateToken)
                return query.SimpleAggregate((AggregateToken)request.ValueToken);

            return query.SelectOne(request.ValueToken).Unique(UniqueType.Single);
        }

        public override Lite<Entity> ExecuteUniqueEntity(UniqueEntityRequest request)
        {
            var ex = new _EntityColumn(EntityColumnFactory().BuildColumnDescription(), QueryName);

            DQueryable<T> orderQuery = Query
                .ToDQueryable(GetQueryDescription())
                .SelectMany(request.Multiplications)
                .Where(request.Filters)
                .OrderBy(request.Orders);

            var result = orderQuery
                .SelectOne(ex.Token)
                .Unique(request.UniqueType);

            return (Lite<Entity>)result;
        }

        public override ResultTable ExecuteQueryGroup(QueryGroupRequest request)
        {
            var simpleFilters = request.Filters.Where(f => !(f.Token is AggregateToken)).ToList();
            var aggregateFilters = request.Filters.Where(f => f.Token is AggregateToken).ToList();

            var keys = request.Columns.Select(t => t.Token).Where(t => !(t is AggregateToken)).ToHashSet();

            var allAggregates = request.AllTokens().OfType<AggregateToken>().ToHashSet();

            DQueryable<T> query = Query
                .ToDQueryable(GetQueryDescription())
                .SelectMany(request.Multiplications)
                .Where(simpleFilters)
                .GroupBy(keys, allAggregates)
                .Where(aggregateFilters)
                .OrderBy(request.Orders);

            var cols = request.Columns
                .Select(column => (column, Expression.Lambda(column.Token.BuildExpression(query.Context), query.Context.Parameter))).ToList();

            var values = query.Query.ToArray();

            return values.ToResultTable(cols, values.Length, new Pagination.All());
        }
        
        public override IQueryable<Lite<Entity>> GetEntities(List<Filter> filters)
        {
            var ex = new _EntityColumn(EntityColumnFactory().BuildColumnDescription(), QueryName);

            DQueryable<T> query = Query
             .ToDQueryable(GetQueryDescription())
             .Where(filters)
             .Select(new List<Column> { ex });

            var exp = Expression.Lambda<Func<object, Lite<Entity>>>(Expression.Convert(ex.Token.BuildExpression(query.Context), typeof(Lite<Entity>)), query.Context.Parameter);

            return query.Query.Select(exp);
        }

        public override Expression Expression
        {
            get { return Query.Expression; }
        }
    }
}
