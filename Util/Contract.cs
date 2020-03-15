using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using AgileObjects.ReadableExpressions;

namespace Ikeran.Util
{
    public static class Contract
    {
#if DEBUG
        public static void Assert(Expression<Func<bool>> b)
        {
            if (!b.Compile()()) throw new ContractException("contract failed: " + ShowExpressionDetailed(b.Body));
        }
        public static void Assert(bool b, string message)
        {
            if (!b) throw new ContractException("contract failed: " + message);
        }

        public static void Requires(params Expression<Func<bool>>[] exprs)
        {
            List<string> failures = null;
            foreach (var expr in exprs)
            {
                var result = expr.Compile()();
                if (!result)
                {
                    if (failures == null)
                    {
                        failures = new List<string>();
                    }
                    string regularString = expr.ToReadableString();
                    failures.Add("requirement failed: " + ShowExpressionDetailed(expr));
                }
            }
            if (failures != null && failures.Count > 0)
            {
                throw new ContractException("" + string.Join("\n\t", failures));
            }
        }

        private static string ShowExpressionDetailed(Expression expr, ExpressionType context = ExpressionType.Block)
        {
            if (expr is BinaryExpression binexp)
            {
                if (expr.Type == typeof(bool))
                {
                    var str = string.Format(
                        "{0} {1} {2}",
                        ShowExpressionDetailed(binexp.Left, expr.NodeType),
                        OpToString(expr.NodeType),
                        ShowExpressionDetailed(binexp.Right, expr.NodeType));
                    if (expr.NodeType == ExpressionType.Or && context == ExpressionType.And ||
                        expr.NodeType == ExpressionType.And && context == ExpressionType.Or)
                    {
                        return string.Format("({0})", str);
                    }
                    return str;
                }
            }
            var result = Expression.Lambda<Func<object>>(Expression.Convert(expr, typeof(object))).Compile()();
            if (result == null)
            {
                return "<null>";
            }
            return result.ToString();
        }

        private static string OpToString(ExpressionType type)
        {
            switch (type)
            {
                case ExpressionType.Or:
                    return "||";
                case ExpressionType.And:
                    return "&&";
                case ExpressionType.Equal:
                    return "==";
                case ExpressionType.NotEqual:
                    return "!=";
                case ExpressionType.LessThan:
                    return "<";
                case ExpressionType.LessThanOrEqual:
                    return "<=";
                case ExpressionType.GreaterThan:
                    return ">";
                case ExpressionType.GreaterThanOrEqual:
                    return ">=";
            }
            throw new Exception("unrecognized whatsit " + type);
        }

#else
        public static void Assert(Func<bool> b) {
            if (!b())
            {
                throw new ContractException("assertion failed");
            }
        }
        public static void Assert(bool b, string message) {
            if (!b)
            {
                throw new ContractException("assertion failed: " + message);
            }
        }
        public static void Requires(params Func<bool>[] exprs) { }
#endif
    }

    public class ContractException : Exception
    {
        public ContractException(string message) : base(message)
        {
        }
    }
}
