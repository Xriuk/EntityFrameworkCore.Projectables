using System.Linq.Expressions;
using System.Reflection;

namespace EntityFrameworkCore.Projectables.Services;

public interface IProjectionExpressionBaseResolver
{
    LambdaExpression FindGeneratedBaseExpression(MemberInfo projectableMemberInfo,
        ProjectableAttribute? projectableAttribute = null);
}