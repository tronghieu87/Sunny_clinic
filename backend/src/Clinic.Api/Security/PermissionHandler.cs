using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
namespace Clinic.Api.Security;
public sealed record PermissionRequirement(string Code, bool TenantWide) : IAuthorizationRequirement;
public sealed class PermissionHandler(PermissionService permissions, IHttpContextAccessor http, SuperAdminAccess admin, VerifiedTenant scope) : AuthorizationHandler<PermissionRequirement> {
 protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement) {
  if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return;
  if (admin.IsSuperAdmin && scope.TenantId is not null) { context.Succeed(requirement); return; }
  var codes = await permissions.Get(userId, requirement.TenantWide, http.HttpContext?.RequestAborted ?? default);
  if (codes.Contains(requirement.Code)) context.Succeed(requirement);
 }
}
