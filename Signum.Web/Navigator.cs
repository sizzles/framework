﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Signum.Engine;
using Signum.Engine.Maps;
using Signum.Utilities;
using Signum.Entities;
using Signum.Utilities.DataStructures;
using System.Reflection;
using Signum.Utilities.Reflection;
using System.Collections.Specialized;
using Signum.Web.Properties;
using Signum.Entities.Properties;
using Signum.Entities.Reflection;
using Signum.Entities.DynamicQuery;
using Signum.Engine.DynamicQuery;

namespace Signum.Web
{
    public static class Navigator
    {
        public static NavigationManager NavigationManager;
        
        public static Type ResolveType(string typeName)
        {
            return NavigationManager.ResolveType(typeName);
        }

        public static Type ResolveTypeFromUrlName(string typeUrlName)
        {
            return NavigationManager.ResolveTypeFromUrlName(typeUrlName); 
        }

        public static object ResolveQueryFromUrlName(string queryUrlName)
        {
            return NavigationManager.ResolveQueryFromUrlName(queryUrlName);
        }

        public static object ResolveQueryFromToStr(string queryNameToStr)
        {
            return NavigationManager.ResolveQueryFromToStr(queryNameToStr);
        }

        public static ViewResult View(this Controller controller, object obj)
        {
            return NavigationManager.View(controller, obj); 
        }

        public static PartialViewResult PartialView<T>(this Controller controller, T entity, string prefix)
        {
            return NavigationManager.PartialView(controller, entity, prefix);
        }

        public static ViewResult Find(Controller controller, object queryName)
        {
            return Find(controller, new FindOptions(queryName));
        }

        public static ViewResult Find(Controller controller, FindOptions findOptions)
        {
            return NavigationManager.Find(controller, findOptions);
        }

        public static PartialViewResult Search(Controller controller, object queryName, List<Filter> filters, int? resultsLimit)
        {
            return NavigationManager.Search(controller, queryName, filters, resultsLimit);
        }

        internal static List<Filter> ExtractFilters(NameValueCollection form)
        {
            return NavigationManager.ExtractFilters(form);
        }

        public static SortedList<string, object> ToSortedList(NameValueCollection form, string prefixToIgnore)
        {
            SortedList<string, object> formValues = new SortedList<string, object>(form.Count);
            foreach (string key in form.Keys)
            {
                if (!string.IsNullOrEmpty(key) && key != "prefixToIgnore")
                {
                    if (string.IsNullOrEmpty(prefixToIgnore) || !key.StartsWith(prefixToIgnore))
                        formValues.Add(key, form[key]);
                }
            }
            
            return formValues;
        }

        public static Dictionary<string, List<string>> ApplyChangesAndValidate<T>(NameValueCollection form, string prefixToIgnore, ref T obj)
        {
            SortedList<string, object> formValues = ToSortedList(form, prefixToIgnore);

            return NavigationManager.ApplyChangesAndValidate(formValues, ref obj, null);
        }

        public static Dictionary<string, List<string>> ApplyChangesAndValidate<T>(SortedList<string, object> formValues, ref T obj)
        {
            return NavigationManager.ApplyChangesAndValidate(formValues, ref obj, null);
        }

        public static Dictionary<string, List<string>> ApplyChangesAndValidate<T>(SortedList<string, object> formValues, ref T obj, string prefix)
            where T:Modifiable
        {
            return NavigationManager.ApplyChangesAndValidate(formValues, ref obj, prefix);
        }

        public static IdentifiableEntity ExtractEntity(NameValueCollection form)
        {
            return NavigationManager.ExtractEntity(form, null);
        }

        public static IdentifiableEntity ExtractEntity(NameValueCollection form, string prefix)
        {
            return NavigationManager.ExtractEntity(form, prefix);
        }

        public static ModifiableEntity CreateInstance(Type type)
        {
            lock (NavigationManager.Constructors)
            {
                return NavigationManager.Constructors
                    .GetOrCreate(type, () => ReflectionTools.CreateConstructor<ModifiableEntity>(type))
                    .Invoke();
            }
        }

        static Dictionary<string, Type> nameToType;
        public static Dictionary<string, Type> NameToType
        {
            get { return nameToType.ThrowIfNullC("Names to Types dictionary not initialized"); }
            set { nameToType = value; }
        }

        public static string NormalPageUrl
        {
            get { return NavigationManager.NormalPageUrl; }
            set { NavigationManager.NormalPageUrl = value; }
        }


        internal static void ConfigureEntityBase(EntityBase el, Type entityType, bool admin)
        {
            EntitySettings es = NavigationManager.Settings[entityType];
            if (es.IsCreable != null)
                el.Create = es.IsCreable(admin);

            if (es.IsViewable != null)
                el.View = es.IsViewable(admin);

            el.Find = false; //depends on having a query
        }
    }

    public class EntitySettings
    {
        public string PartialViewName;
        public Func<bool, bool> IsCreable;
        public Func<bool, bool> IsViewable;

        public EntitySettings(bool isSimpleType)
        {
            if (isSimpleType)
            {
                IsCreable = admin => admin;
                IsViewable = admin => admin;
            }
            else
            {
                IsCreable = admin => true;
                IsViewable = admin => true;
            }
        }
    }

    public class NavigationManager
    {
        public Dictionary<Type, EntitySettings> Settings = new Dictionary<Type, EntitySettings>();
        private DynamicQueryManager queries;
        public DynamicQueryManager Queries
        {
            get { return queries; }
            set 
            {
                queries = value;
                URLnamesToQueries = new Dictionary<string,object>();
                foreach (object key in queries.Queries.Keys)
                {
                    if (key.GetType().IsValueType || key.GetType().IsEnum)
                        URLnamesToQueries.Add(key.ToString(), key);
                    else
                        URLnamesToQueries.Add(((Type)key).Name, key);
                }
            }
        }
        internal Dictionary<string, object> URLnamesToQueries { get; private set; }

        internal string NormalPageUrl = "~/Plugin/Signum.Web.dll/Signum.Web.Views.NormalPage.aspx";
        internal string SearchWindowUrl = "~/Plugin/Signum.Web.dll/Signum.Web.Views.SearchWindow.aspx";
        internal string SearchControlUrl = "~/Plugin/Signum.Web.dll/Signum.Web.Views.SearchControl.ascx";
        
        internal Dictionary<string, Type> URLNamesToTypes { get; private set; }
        internal Dictionary<Type, string> TypesToURLNames { get; private set; }
        internal Dictionary<Type, TypeDN> TypesToTypesDN { get; private set; }
        internal Dictionary<string, Type> ModifiablesNamesToTypes { get; private set; }
        internal Dictionary<Type, Func<ModifiableEntity>> Constructors = new Dictionary<Type, Func<ModifiableEntity>>();

        public NavigationManager()
        {
            URLNamesToTypes = Schema.Current.Tables.Keys.ToDictionary(t =>
                    t.Name.EndsWith("DN") ? t.Name.Substring(0, t.Name.Length - 2) : t.Name);
            TypesToURLNames = Schema.Current.Tables.Keys.ToDictionary(
                t => t,
                e => e.Name.EndsWith("DN") ? e.Name.Substring(0, e.Name.Length - 2) : e.Name);

            InitializeTypesDN();
        }

        public NavigationManager(Dictionary<string, Type> customTypeURLNames)
        {
            URLNamesToTypes = customTypeURLNames;

            TypesToURLNames = new Dictionary<Type, string>();
            customTypeURLNames.ForEach(t => TypesToURLNames.Add(t.Value,t.Key));

            InitializeTypesDN();
        }

        public NavigationManager(Dictionary<string, Type> customTypeURLNames, Dictionary<Type, TypeDN> customTypesToTypeDN)
        {
            URLNamesToTypes = customTypeURLNames;

            TypesToURLNames = new Dictionary<Type, string>();
            customTypeURLNames.ForEach(t => TypesToURLNames.Add(t.Value, t.Key));

            TypesToTypesDN = customTypesToTypeDN;

            Navigator.NameToType = TypesToTypesDN.SelectDictionary(k => k.Name, (t, tdn) => t);
        }

        public virtual void InitializeModifiablesNamesToTypes()
        {
            ModifiablesNamesToTypes = new Dictionary<string, Type>();
            ModifiablesNamesToTypes
                .AddRange(Settings.Keys.Where(t => typeof(ModifiableEntity).IsAssignableFrom(t)),
                          k => k.Name,
                          v => v);
        }

        private void InitializeTypesDN()
        { 
            List<TypeDN> typesDN = Database.RetrieveAll<TypeDN>();

            TypesToTypesDN = TypeLogic.TypeToDN;
                //Schema.Current.Tables.Keys.ToDictionary(t => t, t => (Reflector.ExtractEnumProxy(t) ?? t).Name)
                //.JumpDictionary(typesDN.ToDictionary(td=>td.ClassName));

            Navigator.NameToType = Schema.Current.Tables.Keys.ToDictionary(t => t.Name, t => t); 
        }

        protected internal virtual ViewResult View(Controller controller, object obj)
        {
            EntitySettings es = Navigator.NavigationManager.Settings.TryGetC(obj.GetType()).ThrowIfNullC("No hay una vista asociada al tipo: " + obj.GetType());
            string urlName = Navigator.NavigationManager.TypesToURLNames.TryGetC(obj.GetType()).ThrowIfNullC("No hay un nombre asociado al tipo: " + obj.GetType());
            
            controller.ViewData[ViewDataKeys.MainControlUrl] = es.PartialViewName;
            IdentifiableEntity entity = (IdentifiableEntity)obj;
            controller.ViewData.Model = entity;
            if (controller.ViewData.Keys.Count(s => s==ViewDataKeys.PageTitle)==0)
                controller.ViewData[ViewDataKeys.PageTitle] = entity.ToStr;

            return new ViewResult()
            {
                ViewName = NormalPageUrl,
                MasterName = null,
                ViewData = controller.ViewData,
                TempData = controller.TempData
            };
        }

        protected internal virtual PartialViewResult PartialView<T>(Controller controller, T entity, string prefix)
        {
            EntitySettings es = Navigator.NavigationManager.Settings.TryGetC(entity.GetType()).ThrowIfNullC("No hay una vista asociada al tipo: " + entity.GetType());
            
            controller.ViewData[ViewDataKeys.MainControlUrl] = es.PartialViewName;
            controller.ViewData[ViewDataKeys.PopupPrefix] = prefix;
            controller.ViewData.Model = entity;
            
            return new PartialViewResult
            {
                ViewName = "~/Plugin/Signum.Web.dll/Signum.Web.Views.PopupControl.ascx",
                ViewData = controller.ViewData,
                TempData = controller.TempData
            };
        }

        protected internal virtual ViewResult Find(Controller controller, FindOptions findOptions)
        {
            QueryDescription queryDescription = Queries.QueryDescription(findOptions.QueryName);

            findOptions.FilterOptions = new List<FilterOptions>
            {
                new FilterOptions{Column = queryDescription.Columns[0], ColumnName="IdOrNull", Frozen=true, Operation=FilterOperation.GreaterThan, Value=1},
                new FilterOptions{Column = queryDescription.Columns[1], ColumnName="Nombre", Frozen=false, Operation=FilterOperation.DistinctTo, Value="Max"},
                new FilterOptions{Column = queryDescription.Columns[2], ColumnName="FechaNacimiento", Frozen=true, Operation=FilterOperation.GreaterThan, Value=DateTime.Now},
            };
                    
            List<Column> columns = queryDescription.Columns.Where(a => a.Filterable).ToList();

            controller.ViewData[ViewDataKeys.MainControlUrl] = SearchControlUrl;
            controller.ViewData[ViewDataKeys.QueryName] = findOptions.QueryName;
            controller.ViewData[ViewDataKeys.Columns] = columns;
            controller.ViewData[ViewDataKeys.Filters] = findOptions;

            return new ViewResult()
            {
                ViewName = SearchWindowUrl,
                MasterName = null,
                ViewData = controller.ViewData,
                TempData = controller.TempData
            };
        }

        protected internal virtual PartialViewResult PartialFind(Controller controller, FindOptions findOptions)
        {
            //QueryDescription queryDescription = Server.Service<IQueryServer>().GetQueryDescription(findOptions.QueryName);
            //List<Column> columns = queryDescription.Columns.Where(a => a.Filterable).ToList();
            
            //controller.ViewData[ViewDataKeys.Columns] = columns;
            //controller.ViewData[ViewDataKeys.Filters] = findOptions;

            return new PartialViewResult
            {
                ViewName = "~/Plugin/Signum.Web.dll/Signum.Web.Views.SearchControl.ascx",
                ViewData = controller.ViewData,
                TempData = controller.TempData
            };
        }

        protected internal virtual PartialViewResult Search(Controller controller, object queryName, List<Filter> filters, int? resultsLimit)
        {
            QueryResult queryResult = Queries.ExecuteQuery(queryName, filters, resultsLimit);

            controller.ViewData[ViewDataKeys.Results] = queryResult;

            return new PartialViewResult
            {
                ViewName = "~/Plugin/Signum.Web.dll/Signum.Web.Views.SearchResults.ascx",
                ViewData = controller.ViewData,
                TempData = controller.TempData
            };
        }

        protected internal virtual List<Filter> ExtractFilters(NameValueCollection form)
        {
            List<Filter> result = new List<Filter>();

            int index = 0;
            string name;
            object value;
            string operation;
            while (true)
            {
                if (form.AllKeys.SingleOrDefault(k => k=="name" + index.ToString()) == null)
                    break;

                name = form["name" + index.ToString()];
                value = form["val" + index.ToString()];
                operation = form["sel" + index.ToString()];

                FilterOperation filterOperation = ((FilterOperation[])Enum.GetValues(typeof(FilterOperation))).SingleOrDefault(op => op.NiceToString() == operation);

                result.Add(new Filter
                {
                    Column = new Column() { Name = name },
                    Operation = filterOperation,
                    Value = value,
                });

                index ++;
            }
            return result;
        }

        protected internal virtual Type ResolveTypeFromUrlName(string typeUrlName)
        {
            return URLNamesToTypes
                .TryGetC(typeUrlName)
                .ThrowIfNullC("No hay un tipo asociado al nombre: " + typeUrlName);
        }

        protected internal virtual object ResolveQueryFromUrlName(string queryUrlName)
        {
            return URLnamesToQueries 
                .TryGetC(queryUrlName)
                .ThrowIfNullC("No hay una query asociado al nombre: " + queryUrlName);
        }

        protected internal virtual object ResolveQueryFromToStr(string queryNameToStr)
        {
            return Queries.GetQueryNames()
                .SingleOrDefault(qn => qn.ToString() == queryNameToStr)
                .ThrowIfNullC("No hay una query asociado al nombre: " + queryNameToStr);
        }

        protected internal virtual Type ResolveType(string typeName)
        {
            Type type = null;
            if (Navigator.NameToType.ContainsKey(typeName))
                type = Navigator.NameToType[typeName];
            else
                type = Navigator.NavigationManager.ModifiablesNamesToTypes[typeName];
            
            if (type == null)
                throw new ArgumentException(Resource.Type0NotFoundInTheSchema);
            return type;
        }

        protected internal virtual Dictionary<string, List<string>> ApplyChangesAndValidate<T>(SortedList<string, object> formValues, ref T obj, string prefix)
        {
            Modification modification = GenerateModification(formValues, (Modifiable)(object)obj, prefix ?? "");

            obj = (T)modification.ApplyChanges(obj);

            return GenerateErrors((Modifiable)(object)obj, modification);
        }

        protected internal virtual Dictionary<string, List<string>> GenerateErrors(Modifiable obj, Modification modification)
        {
            Dictionary<string, List<string>> errors = new Dictionary<string, List<string>>();
            modification.Validate(obj, errors);

            Dictionary<Modifiable, string> dicGlobalErrors = obj.FullIntegrityCheckDictionary();
            //Split each error in one entry in the HashTable:
            var globalErrors = dicGlobalErrors.SelectMany(a => a.Value.Lines()).ToList();
            //eliminar de globalErrors los que ya hemos metido en el diccionario
            errors.SelectMany(a => a.Value)
                  .ForEach(e => globalErrors.Remove(e));
            //meter el resto en el diccionario
            if (globalErrors.Count > 0)
                errors.Add(ViewDataKeys.GlobalErrors, globalErrors.ToList());

            return errors;
        }

        protected internal virtual Modification GenerateModification(SortedList<string, object> formValues, Modifiable obj, string prefix)
        {
            MinMax<int> interval = Modification.FindSubInterval(formValues, prefix);

            return Modification.Create(obj.GetType(), formValues, interval, prefix);
        }

        protected internal virtual IdentifiableEntity ExtractEntity(NameValueCollection form, string prefix)
        {
            string typeName = form[(prefix ?? "") + TypeContext.Separator + TypeContext.RuntimeType];
            string id = form[(prefix ?? "") + TypeContext.Separator + TypeContext.Id];

            Type type = Navigator.NameToType.GetOrThrow(typeName, Resource.Type0NotFoundInTheSchema);

            if (!string.IsNullOrEmpty(id))
                return Database.Retrieve(type, int.Parse(id));
            else
                return (IdentifiableEntity)Constructor.Construct(type);
        }
    }

    public static class ModelStateExtensions
    { 
        public static void FromDictionary(this ModelStateDictionary modelState, Dictionary<string, List<string>> errors, NameValueCollection form)
        {
            if (errors != null)
                errors.ForEach(p => p.Value.ForEach(v => modelState.AddModelError(p.Key, v, form[p.Key])));
        }

        public static string ToJsonData(this ModelStateDictionary modelState)
        {
            return modelState.ToJSonObjectBig(
                key => key.Quote(),
                value => value.Errors.ToJSonArray(me => me.ErrorMessage.Quote()));
        }

        //http://www.crankingoutcode.com/2009/02/01/IssuesWithAddModelErrorSetModelValueWithMVCRC1.aspx
        //Necesary to set model value if you add a model error, otherwise some htmlhelpers throw exception
        public static void AddModelError(this ModelStateDictionary modelState, string key, string errorMessage, string attemptedValue)
        {
            modelState.AddModelError(key, errorMessage);
            modelState.SetModelValue(key, new ValueProviderResult(attemptedValue, attemptedValue, null));
        }

    }
}
