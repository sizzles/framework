﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Utilities;
using System.Data.SqlClient;
using Signum.Entities;
using Signum.Utilities.DataStructures;
using Signum.Engine;
using System.Data;
using Signum.Engine.Maps;
using Signum.Engine.Properties;

namespace Signum.Engine
{
    internal static class Deleter
    {
        public static void Delete(Type type, int id)
        {
            Schema.Current.OnDeleting(type, id); 

            SqlPreCommand comando = DeleteCommand(type, id);

            int result = Executor.ExecuteNonQuery(comando.ToSimple());

            if (!ConnectionScope.Current.IsMock && result != 1)
                throw new ApplicationException(Resources.EntityOfType0AndId1NotFound.Formato(type.Name, id));

            Schema.Current.OnDeleted(type, id); 
        }

        internal static SqlPreCommand DeleteCommand(Type type, int id)
        {
            Table table = ConnectionScope.Current.Schema.Table(type);

            SqlPreCommand comando = SqlPreCommand.Combine(Spacing.Double,
                SqlBuilder.DeclareLastEntityID(),
                table.DeleteSql(id));
            return comando;
        }

        public static void Delete(Type type, List<int> ids)
        {
            Schema.Current.OnDeleting(type, ids);

            SqlPreCommand comando = DeleteCommand(type, ids);

            if (Executor.ExecuteNonQuery(comando.ToSimple()) != ids.Count)
                throw new ApplicationException(Resources.NotAllEntitiesOfType0AndIds1Removed.Formato(type.Name, ids.ToString(", ")));

            Schema.Current.OnDeleted(type, ids); 
        }

        internal static SqlPreCommand DeleteCommand(Type type, List<int> ids)
        {
            Table table = ConnectionScope.Current.Schema.Table(type);

            SqlPreCommand comando = SqlPreCommand.Combine(Spacing.Double,
                SqlBuilder.DeclareLastEntityID(),
                ids.Select(id => table.DeleteSql(id)).Combine(Spacing.Simple));
            return comando;
        }
    }
}
