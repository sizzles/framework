﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.ComponentModel;
using Signum.Entities;
using Signum.Utilities;
using Signum.Entities.Reflection;

namespace Signum.Windows
{
    public class EntityBase : UserControl
    {
        public static readonly DependencyProperty LabelTextProperty =
            DependencyProperty.Register("LabelText", typeof(string), typeof(EntityBase), new UIPropertyMetadata("Property"));
        public string LabelText
        {
            get { return (string)GetValue(LabelTextProperty); }
            set { SetValue(LabelTextProperty, value); }
        }

        public static readonly DependencyProperty EntityProperty =
            DependencyProperty.Register("Entity", typeof(object), typeof(EntityBase), new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((EntityBase)d).OnEntityChanged(e.OldValue, e.NewValue)));
        public object Entity
        {
            get { return (object)GetValue(EntityProperty); }
            set
            {
                SetValue(EntityProperty, null);  //entities have equals overriden
                SetValue(EntityProperty, value);
            }
        }

        public static readonly DependencyProperty EntityTypeProperty =
         DependencyProperty.Register("EntityType", typeof(Type), typeof(EntityBase), new UIPropertyMetadata((d, e) => ((EntityBase)d).SetEntityType((Type)e.NewValue)));
        public Type EntityType
        {
            get { return (Type)GetValue(EntityTypeProperty); }
            set { SetValue(EntityTypeProperty, value); }
        }


        protected Type[] safeImplementations;
        public static readonly DependencyProperty ImplementationsProperty =
            DependencyProperty.Register("Implementations", typeof(Type[]), typeof(EntityBase), new UIPropertyMetadata((d, e) => ((EntityBase)d).safeImplementations = (Type[])e.NewValue));
        public Type[] Implementations
        {
            get { return (Type[])GetValue(ImplementationsProperty); }
            set { SetValue(ImplementationsProperty, value); }
        }

        public static readonly DependencyProperty EntityTemplateProperty =
           DependencyProperty.Register("EntityTemplate", typeof(DataTemplate), typeof(EntityBase), new UIPropertyMetadata(null));
        public DataTemplate EntityTemplate
        {
            get { return (DataTemplate)GetValue(EntityTemplateProperty); }
            set { SetValue(EntityTemplateProperty, value); }
        }

        public static readonly DependencyProperty CreateProperty =
            DependencyProperty.Register("Create", typeof(bool), typeof(EntityBase), new FrameworkPropertyMetadata(true, (d, e) => ((EntityBase)d).UpdateVisibility()));
        public bool Create
        {
            get { return (bool)GetValue(CreateProperty); }
            set { SetValue(CreateProperty, value); }
        }

        public static readonly DependencyProperty ViewProperty =
            DependencyProperty.Register("View", typeof(bool), typeof(EntityBase), new FrameworkPropertyMetadata(true, (d, e) => ((EntityBase)d).UpdateVisibility()));
        public bool View
        {
            get { return (bool)GetValue(ViewProperty); }
            set { SetValue(ViewProperty, value); }
        }

        public static readonly DependencyProperty FindProperty =
            DependencyProperty.Register("Find", typeof(bool), typeof(EntityBase), new FrameworkPropertyMetadata(true, (d, e) => ((EntityBase)d).UpdateVisibility()));
        public bool Find
        {
            get { return (bool)GetValue(FindProperty); }
            set { SetValue(FindProperty, value); }
        }

        public static readonly DependencyProperty RemoveProperty =
            DependencyProperty.Register("Remove", typeof(bool), typeof(EntityBase), new FrameworkPropertyMetadata(true, (d, e) => ((EntityBase)d).UpdateVisibility()));
        public bool Remove
        {
            get { return (bool)GetValue(RemoveProperty); }
            set { SetValue(RemoveProperty, value); }
        }

        public static readonly DependencyProperty ViewOnCreateProperty =
            DependencyProperty.Register("ViewOnCreate", typeof(bool), typeof(EntityBase), new UIPropertyMetadata(true));
        public bool ViewOnCreate
        {
            get { return (bool)GetValue(ViewOnCreateProperty); }
            set { SetValue(ViewOnCreateProperty, value); }
        }

        public event Func<object> Creating;
        public event Func<object> Finding;
        public event Func<object, object> Viewing;
        public event Func<object, bool> Removing;

        public event EntityChangedEventHandler EntityChanged; 

        private void SetEntityType(Type type)
        {
 	        if(typeof(Lazy).IsAssignableFrom(type))
            {
                cleanLazy = true; 
                cleanType = EntityType.GetGenericArguments()[0]; 
            }
            else
            {
                cleanLazy = false; 
                cleanType = EntityType; 
            }
        }

        protected Type cleanType;
        protected bool cleanLazy;

        protected bool isUserInteraction =false;

        protected void SetEntityUserInteraction(object entity)
        {
            try
            {
                isUserInteraction = true;
                Entity = entity; 
            }
            finally
            {
                isUserInteraction = false;
            }
        }

  
        public EntityBase()
        {
            this.Loaded += new RoutedEventHandler(OnLoad);
        }

        public virtual void OnLoad(object sender, RoutedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            if (this.EntityType == null)
            {
                throw new ApplicationException(Properties.Resources.EntityTypeItsNotDeterminedForControl0);
            }

            if (this.NotSet(EntityTemplateProperty))
            {
                EntityTemplate = Navigator.FindDataTemplate(this, EntityType);
            }

            EntitySettings es = Navigator.NavigationManager.Settings.TryGetC(cleanType);

            if (this.NotSet(CreateProperty) && Create && Implementations == null)
                Create = es == null ? true : es.IsCreable(false);

            if (this.NotSet(ViewProperty) && View && Implementations == null)
                View = es == null ? false : es.IsViewable(false);

            if (this.NotSet(FindProperty) && Find && Implementations == null)
                Find = Navigator.IsFindable(cleanType);

            if (this.NotSet(ViewOnCreateProperty) && Implementations == null && (es == null || (es.View == null && es.ViewWindow == null)))
                ViewOnCreate = false;

            UpdateVisibility();
        }


        protected virtual void UpdateVisibility()
        {
        }


        protected virtual bool CanRemove()
        {
            return Entity != null && Remove && !Common.GetIsReadOnly(this);
        }

        protected bool CanView()
        {
            return CanView(Entity);
        }

        protected virtual bool CanView(object entity)
        {
            if (entity == null)
                return false;

            if (View && this.NotSet(ViewProperty) && Implementations != null)
            {
                Type rt = (entity as Lazy).TryCC(l => l.RuntimeType) ?? entity.GetType();

                EntitySettings es = Navigator.NavigationManager.Settings.TryGetC(rt);

                return es != null && es.IsViewable(false);
            }
            else
                return View;
        }

        protected virtual bool CanFind()
        {
            return Entity == null && Find && !Common.GetIsReadOnly(this);
        }

        protected virtual bool CanCreate()
        {
            return Entity == null && Create && !Common.GetIsReadOnly(this);
        }

        protected virtual void OnEntityChanged(object oldValue, object newValue)
        {
            if (EntityChanged != null)
                EntityChanged(this, isUserInteraction, oldValue, newValue);

            UpdateVisibility();
        }

        protected object OnCreate()
        {
            if (!CanCreate())
                return null;

            object value;
            if (Creating == null)
            {
                Type type = Implementations == null ? cleanType : Navigator.SelectType(Implementations);
                if (type == null)
                    return null;

                object entity = Constructor.Construct(type, this.FindCurrentWindow());

                value = Server.Convert(entity, EntityType);
            }
            else
                value = Creating();

            if (value == null)
                return null;

            if (ViewOnCreate)
            {
                value = OnViewing(value);
            }

            return value;
        }

        protected object OnFinding(bool allowMultiple)
        {
            if (!CanFind())
                return null;

            object value;
            if (Finding == null)
            {
                Type type = Implementations == null ? cleanType : Navigator.SelectType(Implementations);
                if (type == null)
                    return null;

                value = Navigator.Find(new FindOptions(type) { AllowMultiple = allowMultiple });
            }
            else
                value = Finding();

            if (value == null)
                return null;

            if (value is object[])
                return ((object[])value).Select(o => Server.Convert(o, EntityType)).ToArray();
            else
                return Server.Convert(value, EntityType);
        }

        protected object OnViewing(object entity)
        {
            if (!CanView(entity))
                return null;

            if (Viewing == null)
                return Navigator.View(entity, typeof(EmbeddedEntity).IsAssignableFrom(cleanType) ? Common.GetTypeContext(this) : null);
            else
                return Viewing(entity);
        }

        protected bool OnRemoving(object entity)
        {
            if (!CanRemove())
                return false;

            return Removing == null ? true : Removing(entity);
        }
    }

    public delegate void EntityChangedEventHandler(object sender, bool userInteraction, object oldValue, object newValue);
}
