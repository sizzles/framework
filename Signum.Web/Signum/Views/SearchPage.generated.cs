﻿#pragma warning disable 1591
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.34011
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Signum.Web.Views
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Web;
    using System.Web.Helpers;
    using System.Web.Mvc;
    using System.Web.Mvc.Ajax;
    using System.Web.Mvc.Html;
    using System.Web.Routing;
    using System.Web.Security;
    using System.Web.UI;
    using System.Web.WebPages;
    using Signum.Entities;
    using Signum.Utilities;
    using Signum.Web;
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("RazorGenerator", "2.0.0.0")]
    [System.Web.WebPages.PageVirtualPathAttribute("~/Signum/Views/SearchPage.cshtml")]
    public partial class SearchPage : System.Web.Mvc.WebViewPage<dynamic>
    {
        public SearchPage()
        {
        }
        public override void Execute()
        {
WriteLiteral("\r\n");

            
            #line 2 "..\..\Signum\Views\SearchPage.cshtml"
 using (Html.BeginForm())
{

            
            #line default
            #line hidden
WriteLiteral("    <div");

WriteLiteral(" id=\"divSearchPage\"");

WriteLiteral(">\r\n        <h2>\r\n            <span");

WriteLiteral(" class=\"sf-entity-title\"");

WriteLiteral(">");

            
            #line 6 "..\..\Signum\Views\SearchPage.cshtml"
                                      Write(ViewBag.Title);

            
            #line default
            #line hidden
WriteLiteral("</span>\r\n                     <a");

WriteAttribute("id", Tuple.Create(" id=\"", 166), Tuple.Create("\"", 201)
            
            #line 7 "..\..\Signum\Views\SearchPage.cshtml"
, Tuple.Create(Tuple.Create("", 171), Tuple.Create<System.Object, System.Int32>(Model.Compose("sfFullScreen")
            
            #line default
            #line hidden
, 171), false)
);

WriteLiteral(" class=\"sf-popup-fullscreen\"");

WriteLiteral(">\r\n                            <span");

WriteLiteral(" class=\"glyphicon glyphicon-new-window\"");

WriteLiteral("></span>\r\n                        </a>\r\n        </h2>\r\n");

WriteLiteral("        ");

            
            #line 11 "..\..\Signum\Views\SearchPage.cshtml"
   Write(Html.ValidationSummaryAjax());

            
            #line default
            #line hidden
WriteLiteral("\r\n");

WriteLiteral("        ");

            
            #line 12 "..\..\Signum\Views\SearchPage.cshtml"
   Write(Html.Partial(ViewData[ViewDataKeys.PartialViewName].ToString()));

            
            #line default
            #line hidden
WriteLiteral("\r\n");

WriteLiteral("        ");

            
            #line 13 "..\..\Signum\Views\SearchPage.cshtml"
   Write(Html.AntiForgeryToken());

            
            #line default
            #line hidden
WriteLiteral("\r\n    </div>\r\n");

WriteLiteral("    <div");

WriteLiteral(" class=\"clearall\"");

WriteLiteral("></div>   \r\n");

            
            #line 16 "..\..\Signum\Views\SearchPage.cshtml"
}
            
            #line default
            #line hidden
        }
    }
}
#pragma warning restore 1591
