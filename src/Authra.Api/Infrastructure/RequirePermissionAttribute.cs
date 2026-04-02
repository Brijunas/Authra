using Microsoft.AspNetCore.Authorization;

namespace Authra.Api.Infrastructure;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
        : base($"Permission:{permission}")
    {
    }
}
