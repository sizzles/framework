﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using Signum.Utilities;
using Signum.Utilities.DataStructures;
using Signum.Entities;
using Signum.Engine.Properties;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace Signum.Engine
{
    public enum Spacing
    {
        Simple,
        Double,
        Triple
    }

    public abstract class SqlPreCommand
    {
        public abstract IEnumerable<SqlPreCommandSimple> Leaves();

        protected internal abstract void GenerateScript(StringBuilder sb);

        protected internal abstract void GenerateParameters(List<SqlParameter> list);

        protected internal abstract int NumParameters { get; }

        public abstract SqlPreCommandSimple ToSimple();

        public override string ToString()
        {
            return this.PlainSql();
        }

        public static SqlPreCommand Combine(Spacing spacing, params SqlPreCommand[] sentences)
        {
            if (sentences.Contains(null))
                sentences = sentences.NotNull().ToArray();

            if (sentences.Length == 0)
                return null;

            if (sentences.Length == 1)
                return sentences[0];

            return new SqlPreCommandConcat(spacing, sentences);
        }
    }

    public static class SqlPreCommandExtensions
    {
        public static SqlPreCommand Combine(this IEnumerable<SqlPreCommand> preCommands, Spacing spacing)
        {
            return SqlPreCommand.Combine(spacing, preCommands.ToArray());
        }

        static readonly Regex regex = new Regex(@"@[_\p{Ll}\p{Lu}\p{Lt}\p{Lo}\p{Nl}][_\p{Ll}\p{Lu}\p{Lt}\p{Lo}\p{Nl}\p{Nd}]*");
        /// <summary>
        /// For debugging purposes
        /// </summary>
        public static string PlainSql(this SqlPreCommand command)
        {
            SqlPreCommandSimple cs = command.ToSimple();
            if (cs.Parameters == null)
                return cs.Sql;

            var dic = cs.Parameters.ToDictionary(a=>a.ParameterName, a=>Encode(a.Value)); 

            return regex.Replace(cs.Sql, m=> dic.TryGetC(m.Value) ?? m.Value);
        }


        public static void OpenSqlFile(this SqlPreCommand command)
        {
            OpenSqlFile(command, "Sync {0:dd-MM-yyyy}.sql".Formato(DateTime.Now));
        }

        public static void OpenSqlFileRetry(this SqlPreCommand command)
        {
            command.OpenSqlFile();
            Console.WriteLine("Open again?");
            string val = Console.ReadLine();
            if (!val.StartsWith("y") && !val.StartsWith("Y"))
                return;

            command.OpenSqlFile();
        }

        public static void OpenSqlFile(this SqlPreCommand command, string fileName)
        {
            string content = command.PlainSql(); 

            File.WriteAllText(fileName, content, Encoding.GetEncoding(1252));

            Thread.Sleep(1000);

            Process.Start(fileName); 
        }

        static string Encode(object value)
        {
            if (value == null || value == DBNull.Value)
                return "NULL";

            if (value is string)
                return "\'" + ((string)value).Replace("'", "''") + "'";

            if (value is DateTime)
                return "convert(datetime, '{0:s}', 126)".Formato(value);

            if (value is bool)
               return (((bool)value) ? 1 : 0).ToString();

            return value.ToString();
        }
    }

    public class SqlPreCommandSimple : SqlPreCommand
    {
        public string Sql { get; private set; }
        public List<SqlParameter> Parameters { get; private set; }

        public SqlPreCommandSimple(string sql)
        {
            this.Sql = sql;
        }

        public SqlPreCommandSimple(string sql, List<SqlParameter> parameters)
        {
            this.Sql = sql;
            this.Parameters = parameters;
        }

        public override IEnumerable<SqlPreCommandSimple> Leaves()
        {
            yield return this;
        }

        protected internal override void GenerateScript(StringBuilder sb)
        {
            sb.Append(Sql);
        }

        protected internal override void GenerateParameters(List<SqlParameter> list)
        {
            if (Parameters != null)
                list.AddRange(Parameters);
        }

        public override SqlPreCommandSimple ToSimple()
        {
            return this;
        }

        protected internal override int NumParameters
        {
            get { return Parameters.TryCS(p => p.Count) ?? 0; }
        }
    }

    public class SqlPreCommandConcat : SqlPreCommand
    {
        public Spacing Spacing { get; private set; }
        public SqlPreCommand[] Commands { get; private set; }

        internal SqlPreCommandConcat(Spacing spacing, SqlPreCommand[] commands)
        {
            this.Spacing = spacing;
            this.Commands = commands;
        }

        public override IEnumerable<SqlPreCommandSimple> Leaves()
        {
            return Commands.SelectMany(c => c.Leaves());
        }

        protected internal override void GenerateScript(StringBuilder sb)
        {
            string sep = separators[Spacing];
            bool borrar = false;
            foreach (SqlPreCommand com in Commands)
            {
                com.GenerateScript(sb);
                sb.Append(sep);
                borrar = true;
            }

            if (borrar) sb.Remove(sb.Length - sep.Length, sep.Length);
        }

        protected internal override void GenerateParameters(List<SqlParameter> list)
        {
            foreach (SqlPreCommand com in Commands)
                com.GenerateParameters(list);
        }

        public override SqlPreCommandSimple ToSimple()
        {
            StringBuilder sb = new StringBuilder();
            GenerateScript(sb);

            List<SqlParameter> parameters = new List<SqlParameter>();
            GenerateParameters(parameters);

            return new SqlPreCommandSimple(sb.ToString(), parameters);
        }

        static Dictionary<Spacing, string> separators = new Dictionary<Spacing, string>()
        {
            {Spacing.Simple, ";\r\n"},
            {Spacing.Double, ";\r\n\r\n"},
            {Spacing.Triple, ";\r\n\r\n\r\n"},
        };

        protected internal override int NumParameters
        {
            get { return Commands.Sum(c => c.NumParameters); }
        }
    }

}
