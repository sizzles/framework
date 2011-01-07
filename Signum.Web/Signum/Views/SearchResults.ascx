﻿<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl" %>
<%@ Import Namespace="Signum.Web" %>
<%@ Import Namespace="Signum.Entities" %>
<%@ Import Namespace="Signum.Entities.DynamicQuery" %>
<%@ Import Namespace="Signum.Entities.Reflection" %>
<%@ Import Namespace="System.Collections.Generic" %>
<%@ Import Namespace="Signum.Utilities" %>
<%@ Import Namespace="Signum.Web.Properties" %>
<%@ Import Namespace="Signum.Engine" %>

<% Context context = (Context)Model;
   FindOptions findOptions = (FindOptions)ViewData[ViewDataKeys.FindOptions];
   QueryDescription queryDescription = (QueryDescription)ViewData[ViewDataKeys.QueryDescription];
   Type entitiesType = Reflector.ExtractLite(queryDescription.Columns.Single(a => a.IsEntity).Type);
   bool viewable = findOptions.View && Navigator.IsNavigable(entitiesType, true);
 
    ResultTable queryResult = (ResultTable)ViewData[ViewDataKeys.Results];
    Dictionary<int, Func<HtmlHelper, object, MvcHtmlString>> formatters = (Dictionary<int, Func<HtmlHelper, object, MvcHtmlString>>)ViewData[ViewDataKeys.Formatters];

    foreach (var row in queryResult.Rows)
    {
        Lite entityField = row.Entity;
      %>
      <tr data-entity="<%: entityField.Key() %>">
      <%
        if (findOptions.AllowMultiple.HasValue)
        {
            %>
            <td>
            <%
            if (findOptions.AllowMultiple.Value)
            {
                Html.Write(Html.CheckBox(
                    context.Compose("rowSelection", row.Index.ToString()),
                    new { value = entityField.Id.ToString() + "__" + Navigator.ResolveWebTypeName(entityField.RuntimeType) + "__" + entityField.ToStr }));

            }
            else
            {
                Html.Write(Html.RadioButton(
                    context.Compose("rowSelection"),
                    entityField.Id.ToString() + "__" + Navigator.ResolveWebTypeName(entityField.RuntimeType) + "__" + entityField.ToStr));
            }  
             %>
            </td>
            <%
        }
        
        if (viewable)
        {
             %>
            <td>
            <%= QuerySettings.EntityFormatRules.Last(fr => fr.IsApplyable(entityField)).Formatter(Html, entityField) %>
            </td>
            <%
        }

        foreach (var col in queryResult.Columns)
        {
             %>
            <td>
            <%: formatters[col.Index](Html, row[col]) %>
            </td>
            <%
        }
        %>
        </tr>
        <%
    }%>