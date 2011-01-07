﻿#region usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Collections;
using System.Linq.Expressions;
using System.Web.Mvc.Html;
using Signum.Entities;
using Signum.Entities.Reflection;
using Signum.Utilities;
using System.Configuration;
using Signum.Web.Properties;
#endregion

namespace Signum.Web
{
    public static class EntityListDetailHelper
    {
        private static MvcHtmlString InternalEntityListDetail<T>(this HtmlHelper helper, EntityListDetail listDetail)
        {
            if (!listDetail.Visible || listDetail.HideIfNull && listDetail.UntypedValue == null)
                return MvcHtmlString.Empty;

            string defaultDetailDiv = listDetail.Compose(EntityBaseKeys.Detail);
            if (!listDetail.DetailDiv.HasText())
                listDetail.DetailDiv = defaultDetailDiv;

            HtmlStringBuilder sb = new HtmlStringBuilder();

            sb.AddLine(EntityBaseHelper.BaseLineLabel(helper, listDetail));

            sb.AddLine(helper.HiddenStaticInfo(listDetail));

            sb.AddLine(helper.Hidden(listDetail.Compose(TypeContext.Ticks), 
                EntityInfoHelper.GetTicks(helper, listDetail).TryToString() ?? ""));

            //If it's an embeddedEntity write an empty template with index 0 to be used when creating a new item
            if (listDetail.ElementType.IsEmbeddedEntity())
            {
                TypeElementContext<T> templateTC = new TypeElementContext<T>((T)(object)Constructor.Construct(typeof(T)), (TypeContext)listDetail.Parent, 0);
                sb.AddLine(EntityBaseHelper.EmbeddedTemplate(listDetail, EntityBaseHelper.RenderTypeContext(helper, templateTC, RenderMode.Content, listDetail)));
            }

            using (listDetail.ShowFieldDiv ? sb.Surround(new HtmlTag("div").Class("fieldList")) : null)
            {
                HtmlStringBuilder sbSelect = new HtmlStringBuilder();
                using(sbSelect.Surround(new HtmlTag("select").IdName(listDetail.ControlID).Attr("multiple", "multiple").Attr("ondblclick", listDetail.GetViewing()).Class("entityList")))
                {
                    if (listDetail.UntypedValue != null)
                    {
                        foreach (var itemTC in TypeContextUtilities.TypeElementContext((TypeContext<MList<T>>)listDetail.Parent))
                            sb.Add(InternalListDetailElement(helper, sbSelect, itemTC, listDetail));
                    }
                }

                sb.Add(sbSelect.ToHtml());

                sb.AddLine(new HtmlStringBuilder()
                {
                    ListBaseHelper.CreateButton(helper, listDetail, null).Surround("td").Surround("tr"),
                    ListBaseHelper.FindButton(helper, listDetail).Surround("td").Surround("tr"),
                    ListBaseHelper.RemoveButton(helper, listDetail).Surround("td").Surround("tr")
                }.ToHtml().Surround("table"));
            }

            sb.AddLine(EntityBaseHelper.BreakLineDiv(helper, listDetail));

            if (listDetail.DetailDiv == defaultDetailDiv)
                sb.AddLine(helper.Div(listDetail.DetailDiv, null, "detail"));

            if (listDetail.UntypedValue != null && ((IList)listDetail.UntypedValue).Count > 0)
                sb.AddLine(MvcHtmlString.Create("<script type=\"text/javascript\">\n" +
                        "$(document).ready(function() {" +
                        "$('#" + listDetail.ControlID + "').dblclick();\n" +
                        "});" +
                        "</script>"));

            sb.AddLine(EntityBaseHelper.BreakLineDiv(helper, listDetail));

            return sb.ToHtml();
        }

        private static MvcHtmlString InternalListDetailElement<T>(this HtmlHelper helper, HtmlStringBuilder sbOptions, TypeElementContext<T> itemTC, EntityListDetail listDetail)
        {
            HtmlStringBuilder sb = new HtmlStringBuilder();

            if (!listDetail.ForceNewInUI)
                sb.AddLine(helper.Hidden(itemTC.Compose(EntityListBaseKeys.Index), itemTC.Index.ToString()));

            sb.AddLine(helper.HiddenRuntimeInfo(itemTC));

            //TODO: Anto - RequestLoadAll con ItemTC
            if (typeof(T).IsEmbeddedEntity() || ((IdentifiableEntity)(object)itemTC.Value).IsNew || EntityBaseHelper.RequiresLoadAll(helper, listDetail) || listDetail.ForceNewInUI)
                sb.AddLine(EntityBaseHelper.RenderTypeContext(helper, itemTC, RenderMode.ContentInInvisibleDiv, listDetail));
            else if (itemTC.Value != null)
                sb.Add(helper.Div(itemTC.Compose(EntityBaseKeys.Entity), null, "", new Dictionary<string, object> { { "style", "display:none" } }));

            //Note this is added to the sbOptions, not to the result sb
            HtmlTag tbOption = new HtmlTag("option", itemTC.Compose(EntityBaseKeys.ToStr))
                    .Attrs(new
                    {
                        name = itemTC.Compose(EntityBaseKeys.ToStr),
                        value = ""
                    })
                    .Class("valueLine")
                    .Class("entityListOption")
                    .SetInnerText(
                        (itemTC.Value as IIdentifiable).TryCC(i => i.ToString()) ??
                        (itemTC.Value as Lite).TryCC(i => i.ToStr) ??
                        (itemTC.Value as EmbeddedEntity).TryCC(i => i.ToString()) ?? "");

            if (itemTC.Index == 0)
                tbOption.Attr("selected", "selected");


            sbOptions.Add(tbOption.ToHtml());

            return sb.ToHtml();
        }

        public static void EntityListDetail<T, S>(this HtmlHelper helper, TypeContext<T> tc, Expression<Func<T, MList<S>>> property)
        {
            helper.EntityListDetail<T, S>(tc, property, null);
        }

        public static void EntityListDetail<T, S>(this HtmlHelper helper, TypeContext<T> tc, Expression<Func<T, MList<S>>> property, Action<EntityListDetail> settingsModifier)
        {
            TypeContext<MList<S>> context = Common.WalkExpression(tc, property);

            EntityListDetail el = new EntityListDetail(context.Type, context.UntypedValue, context, null, context.PropertyRoute);

            EntityBaseHelper.ConfigureEntityBase(el, Reflector.ExtractLite(typeof(S)) ?? typeof(S));

            Common.FireCommonTasks(el);

            if (settingsModifier != null)
                settingsModifier(el);

            helper.Write(helper.InternalEntityListDetail<S>(el));
        }
    }
}
