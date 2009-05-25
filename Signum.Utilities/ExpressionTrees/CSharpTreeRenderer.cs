﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Collections.ObjectModel;
using System.Reflection;
using System.CodeDom;
using Microsoft.CSharp;
using System.IO;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using Signum.Utilities.Reflection;

namespace Signum.Utilities.ExpressionTrees
{
    public static class CSharTreeRenderer
    {
        static List<ExpressionType> precedence = new List<ExpressionType>
        {   
            //elements
            ExpressionType.Parameter,
            ExpressionType.Quote,
            ExpressionType.Constant,
            //primary
            ExpressionType.New,
            ExpressionType.NewArrayInit,
            ExpressionType.ListInit,

            ExpressionType.NewArrayBounds,
            ExpressionType.ArrayIndex,
            ExpressionType.ArrayLength,
            ExpressionType.Convert,
            ExpressionType.ConvertChecked,
            ExpressionType.Invoke,
            ExpressionType.Call,
            ExpressionType.MemberAccess,
            ExpressionType.MemberInit,
            //unary
            ExpressionType.Negate,
            ExpressionType.UnaryPlus,
            ExpressionType.NegateChecked,
            ExpressionType.Not,
            //multiplicative
            ExpressionType.Divide, 
            ExpressionType.Modulo,    
            ExpressionType.Multiply,
            ExpressionType.MultiplyChecked,
            //aditive
            ExpressionType.Add,
            ExpressionType.AddChecked,
            ExpressionType.Subtract,
            ExpressionType.SubtractChecked,
            //shift
            ExpressionType.LeftShift, 
            ExpressionType.RightShift,
            //relational an type testing
            ExpressionType.GreaterThan,
            ExpressionType.GreaterThanOrEqual, 
            ExpressionType.LessThan,
            ExpressionType.LessThanOrEqual,
            ExpressionType.TypeAs,
            ExpressionType.TypeIs,
            //equality
            ExpressionType.Equal,  
            ExpressionType.NotEqual,
            //logical
            ExpressionType.And,
            ExpressionType.AndAlso,
            ExpressionType.ExclusiveOr, 
            ExpressionType.Or,
            ExpressionType.OrElse,
            //conditional
            ExpressionType.Conditional,
            //asignment

            ExpressionType.Coalesce,
            ExpressionType.Lambda,
        };


        public static string GenerateCSharpCode(this Expression expression)
        {
            return VisitReal(expression);
        }

        static string VisitReal(Expression exp)
        {
            if (exp == null)
                throw new ArgumentNullException("exp");

            bool collapse = false;
            bool literal = false; 
            if (exp.NodeType == ExpressionType.Call)
            {
                MethodCallExpression mc = (MethodCallExpression)exp;
                if (mc.Method.Name == "Literal" && mc.Method.DeclaringType == typeof(Tree))
                {
                    exp = mc.Arguments[0];
                    literal = true;
                }
                
                if (mc.Method.Name == "Collapse" && mc.Method.DeclaringType == typeof(Tree))
                {
                    exp = mc.Arguments[0];
                    collapse = true; 
                }
            }

            switch (exp.NodeType)
            {
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.ArrayIndex:
                case ExpressionType.Coalesce:
                case ExpressionType.Divide:
                case ExpressionType.Equal:
                case ExpressionType.ExclusiveOr:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.LeftShift:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.Modulo:
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                case ExpressionType.NotEqual:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                case ExpressionType.Power:
                case ExpressionType.RightShift:
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                    return VisitBinary((BinaryExpression)exp);

                case ExpressionType.ArrayLength:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                case ExpressionType.Negate:
                case ExpressionType.UnaryPlus:
                case ExpressionType.NegateChecked:
                case ExpressionType.Not:
                case ExpressionType.Quote:
                case ExpressionType.TypeAs:
                    return VisitUnary((UnaryExpression)exp);

                case ExpressionType.Call:
                    return VisitMethodCall((MethodCallExpression)exp);

                case ExpressionType.Conditional:
                    return VisitConditional((ConditionalExpression)exp);

                case ExpressionType.Constant:
                    return VisitConstant((ConstantExpression)exp, literal);

                case ExpressionType.Invoke:
                    return VisitInvocation((InvocationExpression)exp);

                case ExpressionType.Lambda:
                    return VisitLambda((LambdaExpression)exp);

                case ExpressionType.ListInit:
                    return VisitListInit((ListInitExpression)exp, collapse);

                case ExpressionType.MemberAccess:
                    return VisitMemberAccess((MemberExpression)exp, literal);

                case ExpressionType.MemberInit:
                    return VisitMemberInit((MemberInitExpression)exp, collapse);

                case ExpressionType.New:
                    return VisitNew((NewExpression)exp);

                case ExpressionType.NewArrayInit:
                case ExpressionType.NewArrayBounds:
                    return VisitNewArray((NewArrayExpression)exp, collapse);

                case ExpressionType.Parameter:
                    return VisitParameter((ParameterExpression)exp);

                case ExpressionType.TypeIs:
                    return VisitTypeIs((TypeBinaryExpression)exp);
            }
            throw new Exception("UnhandledExpressionType");
        }

        static string Visit(Expression exp, ExpressionType nodeType)
        {
            string result =  VisitReal(exp);

            int prevIndex = precedence.IndexOf(nodeType);
            if (prevIndex == -1)
                throw new NotImplementedException("Not supported {0}".Formato(nodeType));
            int newIndex = precedence.IndexOf(exp.NodeType);
            if (newIndex == -1)
                throw new NotImplementedException("Not supported {0}".Formato(exp.NodeType));

            if (prevIndex < newIndex)
                return "({0})".Formato(result);
            else return result; 
        }


        #region Simple
        static Dictionary<ExpressionType, string> unarySymbol = new Dictionary<ExpressionType, string>()
        {
            {ExpressionType.ArrayLength, "{0}.Length"},
            {ExpressionType.Convert, "{0}"},
            {ExpressionType.ConvertChecked, "{0}"},
            {ExpressionType.Negate, "-{0}"},
            {ExpressionType.UnaryPlus, "+{0}"},
            {ExpressionType.NegateChecked, "-{0}"},
            {ExpressionType.Not, "!{0}"},
            {ExpressionType.Quote, ""},
        };

        static string VisitUnary(UnaryExpression u)
        {
            if (u.NodeType == ExpressionType.TypeAs)
                return "{0} as {1}".Formato(Visit(u.Operand, u.NodeType), u.Type.Name);
            else
                return unarySymbol.GetOrThrow(u.NodeType, "The nodeType {0} is not supported as unary expression")
                    .Formato(Visit(u.Operand, u.NodeType));
        }

        static string VisitTypeIs(TypeBinaryExpression b)
        {
            return "{0} is {1}".Formato(Visit(b.Expression, b.NodeType), b.TypeOperand.TypeName());
        }

        static Dictionary<ExpressionType, string> binarySymbol = new Dictionary<ExpressionType, string>
        {
            { ExpressionType.Add, "{0} + {1}"},
            { ExpressionType.AddChecked, "{0} + {1}"},
            { ExpressionType.And, "{0} & {1}"},
            { ExpressionType.AndAlso, "{0} && {1}"},
            { ExpressionType.ArrayIndex, "{0}[{1}]" },
            { ExpressionType.Coalesce, "{0} ?? {1}"},
            { ExpressionType.Divide, "{0} / {1}"}, 
            { ExpressionType.Equal, "{0} == {1}"}, 
            { ExpressionType.ExclusiveOr,"{0} ^ {1}"},
            { ExpressionType.GreaterThan,"{0} > {1}"},
            { ExpressionType.GreaterThanOrEqual,"{0} >= {1}"},
            { ExpressionType.LeftShift,"{0} << {1}"},
            { ExpressionType.LessThan,"{0} < {1}"},
            { ExpressionType.LessThanOrEqual,"{0} <= {1}"},
            { ExpressionType.Modulo,"{0} % {1}"},
            { ExpressionType.Multiply,"{0} * {1}"},
            { ExpressionType.MultiplyChecked,"{0} * {1}"},
            { ExpressionType.NotEqual,"{0} != {1}"},
            { ExpressionType.Or,"{0} | {1}"},
            { ExpressionType.OrElse,"{0} || {1}"},
            { ExpressionType.RightShift,"{0} >> {1}"},
            { ExpressionType.Subtract,"{0} - {1}"},
            { ExpressionType.SubtractChecked,"{0} - {1}"}
        };

        static string VisitBinary(BinaryExpression b)
        {
            return binarySymbol.GetOrThrow(b.NodeType, "Node {0} is not supported as a binary expression")
                .Formato(Visit(b.Left, b.NodeType), Visit(b.Right, b.NodeType));
        }

        static string VisitConditional(ConditionalExpression c)
        {
            return "{0} ? {1} : {2}".Formato(Visit(c.Test, c.NodeType), Visit(c.IfTrue, c.NodeType), Visit(c.IfFalse, c.NodeType));
        }

        static string VisitMemberAccess(MemberExpression m, bool literal)
        {
            if (m.Expression == null)
                return m.Member.Name;
            else if (m.Expression is ConstantExpression)
            {
                object obj = ((ConstantExpression)m.Expression).Value;

                object value = m.Member is FieldInfo ? ((FieldInfo)m.Member).GetValue(obj) : ((PropertyInfo)m.Member).GetValue(obj, null);
                if (literal)
                    return value == null ? "null" : value.ToString();
                else
                    return CSharpAuxRenderer.Value(value, value.TryCC(v=>v.GetType()));
            }
            else
                return "{0}.{1}".Formato(Visit(m.Expression, m.NodeType), m.Member.Name);
        }

        static string VisitConstant(ConstantExpression c, bool literal)
        {
            if (literal)
                return c.Value.ToString();
            else
                return CSharpAuxRenderer.Value(c.Value, c.Type);
        }

        static string VisitParameter(ParameterExpression p)
        {
            return p.Name;
        }

        #endregion

        #region Colecciones
        const int IdentationSpaces = 4;

        static string Line<T>(ReadOnlyCollection<T> collection, Func<T, string> func)
        {
            return collection.ToString(func, ", ");
        }

        static string Block<T>(ReadOnlyCollection<T> collection, Func<T, string> func, bool collapse)
        {
            if (collection.Count == 0)
                return "{ }";
            if(collapse)
                return "{{ {0} }}".Formato(collection.ToString(func, ", "));

            return "\r\n{{\r\n{0}\r\n}}".Formato(collection.ToString(func, ",\r\n").Indent(IdentationSpaces));
        }
        #endregion
        
        #region Elements
        static string VisitBinding(MemberBinding binding)
        {
            switch (binding.BindingType)
            {
                case MemberBindingType.Assignment:
                    return VisitMemberAssignment((MemberAssignment)binding);
                default:
                    throw new ApplicationException("Unexpected {0}".Formato(binding.BindingType));
            }
        }

        static string VisitMemberAssignment(MemberAssignment assignment)
        {
            return "{0} = {1}".Formato(assignment.Member.Name, VisitReal(assignment.Expression));
        }

        static string VisitElementInitializer(ElementInit initializer)
        {
            if (initializer.Arguments.Count == 1)
                return VisitReal(initializer.Arguments[0]);
            else
                return Block(initializer.Arguments, VisitReal, true);
        }

        #endregion

        #region Collection Containers
        static string VisitMemberInit(MemberInitExpression init, bool collapse)
        {
            string newExpr = Visit(init.NewExpression, init.NodeType);
            if (newExpr.EndsWith("()"))
                newExpr = newExpr.RemoveRight(2);
            return @"{0} {1}".Formato(newExpr, Block(init.Bindings, VisitBinding, collapse));
        }

        static string VisitListInit(ListInitExpression init, bool collapse)
        {
            string newExpr = Visit(init.NewExpression, init.NodeType);
            if (newExpr.EndsWith("()"))
                newExpr = newExpr.RemoveRight(2);
            return @"{0} {1}".Formato(newExpr, Block(init.Initializers, VisitElementInitializer, collapse));
        }

        static string VisitInvocation(InvocationExpression iv)
        {
            return "{0}({1})".Formato(Visit(iv.Expression, iv.NodeType), Line(iv.Arguments, VisitReal));
        }

        static string VisitMethodCall(MethodCallExpression m)
        {
            return "{0}.{1}({2})".Formato(
                m.Object != null ? Visit(m.Object, m.NodeType) : m.Method.DeclaringType.TypeName(),
                m.Method.MethodName(),
                Line(m.Arguments, VisitReal));
        }

        static string VisitNew(NewExpression nex)
        {
            return "new {0}({1})".Formato(nex.Type.TypeName(), Line(nex.Arguments, VisitReal));
        }

        static string VisitNewArray(NewArrayExpression na, bool collapse)
        {
            string arrayType = na.Type.GetElementType().Name;

            if (na.NodeType == ExpressionType.NewArrayBounds)
                return "new {0}[{1}]".Formato(arrayType, VisitReal(na.Expressions.Single()));
            else
                return "new {0}[] {1}".Formato(arrayType, Block(na.Expressions, VisitReal, collapse));
        } 
        #endregion

        static string VisitLambda(LambdaExpression lambda)
        {
            string body = Visit(lambda.Body, lambda.NodeType);
            if (lambda.Parameters.Count == 1)
                return "{0} => {1}".Formato(Visit(lambda.Parameters.Single(), lambda.NodeType), body);
            else
                return "({0}) => {1}".Formato(lambda.Parameters.ToString(p => Visit(p, lambda.NodeType), ","), body);
        }
    }
}
