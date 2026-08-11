using System;
using System.Linq.Expressions;

namespace LaNata_Api.Audit.Helpers
{
    internal static class AuditReflection
    {
        // Value types get boxed into a Convert node by the compiler, so the operand is unwrapped
        public static string GetMemberName(Expression expression)
        {
            return expression switch
            {
                LambdaExpression lambda => GetMemberName(lambda.Body),
                UnaryExpression unary => GetMemberName(unary.Operand),
                MemberExpression member => member.Member.Name,
                _ => throw new ArgumentException("Only simple property expressions are supported.", nameof(expression))
            };
        }

        public static object GetPropertyValue(object instance, string propertyName)
        {
            return instance?
                .GetType()
                .GetProperty(propertyName)?
                .GetValue(instance);
        }
    }
}
