using System.Reflection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Clinic.Api.Security;

public sealed class BusinessApiOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var api = context.MethodInfo.GetCustomAttribute<BusinessApiAttribute>();
        if (api is null) return;

        operation.Summary = api.Description;
        operation.Description = $"{api.Description}\n\nPermission/API code: `{api.Code}`. Configure this method and route under the matching function in the permission administration page.";
    }
}
