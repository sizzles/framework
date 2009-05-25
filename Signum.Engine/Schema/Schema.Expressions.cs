﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Data;
using System.IO;
using Microsoft.SqlServer.Server;
using Signum.Utilities;
using Signum.Entities;
using Signum.Engine;
using System.Linq.Expressions;
using System.Diagnostics;
using System.Collections.ObjectModel;
using Signum.Engine.Linq;
using Signum.Entities.Reflection;

namespace Signum.Engine.Maps
{
    public partial class Table
    {
        internal ReadOnlyCollection<FieldBinding> CreateBindings(string alias)
        {
            var bindings = Fields.Values.Select(c => new FieldBinding(c.FieldInfo, c.Expression(alias))).ToReadOnly();

            if (!IsView)
            {
                ColumnExpression ce = bindings.IDColumn();

                bindings.Select(fb => fb.Binding).OfType<MListExpression>().ForEach(ml => ml.BackID = ce);
            }

            return bindings; 
        }
    }

    public partial class RelationalTable
    {
        internal ColumnExpression BackColumnExpression(string alias)
        {
            return new ColumnExpression(BackReference.ReferenceType(), alias, BackReference.Name);
        }

        internal Expression CampoExpression(string alias)
        {
            return Field.Expression(alias); 
        }
    }

    public static partial class ColumnExtensions
    {
        public static Type ReferenceType(this IColumn columna)
        {
            Debug.Assert(columna.SqlDbType == SqlBuilder.PrimaryKeyType);

            return columna.Nullable ? typeof(int?) : typeof(int);
        }
    }

    public abstract partial class Field
    {
        internal abstract Expression Expression(string alias);
    }

    public partial class PrimaryKeyField
    {
        internal override Expression Expression(string alias)
        {
            return new ColumnExpression(this.FieldType, alias, this.Name);
        } 
    }

    public partial class ValueField
    {
        internal override Expression Expression(string alias)
        {
            return new ColumnExpression(this.FieldInfo.FieldType //queremos los nullables
                , alias, this.Name);
        } 
    }

    public static partial class ReferenceFieldExtensions
    {
        internal static Expression MaybeLazy(this IReferenceField campo, Expression reference)
        {
            if (!campo.IsLazy)
                return reference;
            else
                return new LazyReferenceExpression(Reflector.GenerateLazy(reference.Type), reference);
        }
    }


    public partial class ReferenceField
    {
        internal override Expression Expression(string alias)
        {
            return this.MaybeLazy(new FieldInitExpression(this.FieldType, alias ,new ColumnExpression(this.ReferenceType(), alias, Name))); 
        } 
    }

    public partial class EnumField
    {
        internal override Expression Expression(string alias)
        {
            return new EnumExpression(this.FieldType,
                new ColumnExpression(this.ReferenceType(), alias, Name));
        }
    }

    public partial class CollectionField
    {
        internal override Expression Expression(string alias)
        {
            return new MListExpression(FieldType, null, RelationalTable); // keep back id empty for some seconds 
        }
    }

    public partial class EmbeddedField
    {
        internal override Expression Expression(string alias)
        {
            List<FieldBinding> fb = new List<FieldBinding>();
            foreach (var kvp in EmbeddedFields)
	        {
                FieldInfo fi = FieldType.GetField(kvp.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); 
                fb.Add(new FieldBinding(fi, kvp.Value.Expression(alias))); 
            }
            return new FieldInitExpression(this.FieldType, alias, null) { Bindings = fb.NotNull().ToReadOnly() }; 
        }
    }

    public partial class ImplementedByField
    {
        internal override Expression Expression(string alias)
        {
            List<ImplementationColumnExpression> ri = new List<ImplementationColumnExpression>();
            foreach (var kvp in ImplementationColumns)
	        {
                ri.Add(new ImplementationColumnExpression(kvp.Key,
                    new FieldInitExpression(kvp.Key, new ColumnExpression(kvp.Value.ReferenceType(), alias, kvp.Value.Name))));
            }

            return this.MaybeLazy(new ImplementedByExpression(FieldType, ri.NotNull().ToReadOnly())); 
        }
    }

    public partial class ImplementedByAllField
    {
        internal override Expression Expression(string alias)
        {
            return this.MaybeLazy(new ImplementedByAllExpression(FieldType,
                new ColumnExpression( Column.ReferenceType(), alias, Column.Name),
                new ColumnExpression( Column.ReferenceType(), alias, ColumnTypes.Name)));
        }
    }
}
