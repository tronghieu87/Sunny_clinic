using System.Security.Claims;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
namespace Clinic.Api.Security;
[AttributeUsage(AttributeTargets.Method)]
public sealed class BusinessApiAttribute(string code, string description, bool tenantWide = false) : Attribute {
 public string Code { get; } = code; public string Description { get; } = description; public bool TenantWide { get; } = tenantWide;
}
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)] public sealed class ControlPlaneAttribute : Attribute;
public static class EndpointAccess {
 public static async Task Sync(WebApplication app) {
  var endpoints = ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints)
   .OfType<RouteEndpoint>().Where(e => e.Metadata.GetMetadata<BusinessApiAttribute>() is not null).ToList();
  var codes = new HashSet<string>();
  using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AuthorizationDbContext>();
  var stored = await db.ApiEndpoints.ToListAsync();
  foreach (var endpoint in endpoints) {
   var metadata = endpoint.Metadata.GetMetadata<BusinessApiAttribute>()!;
   if (!codes.Add(metadata.Code)) throw new InvalidOperationException("Duplicate API endpoint code.");
   var method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single();
   var row = stored.SingleOrDefault(e => e.Code == metadata.Code);
   if (row is null) { row = new ApiEndpoint { Code = metadata.Code }; db.ApiEndpoints.Add(row); }
   row.Method = method; row.Route = endpoint.RoutePattern.RawText!; row.Description = metadata.Description; row.TenantWide = metadata.TenantWide; row.IsActive = true;
  }
  foreach (var stale in stored.Where(e => !codes.Contains(e.Code))) stale.IsActive = false;
  await db.SaveChangesAsync();
 }
 public static async Task Check(HttpContext context, RequestDelegate next) {
  var endpoint = context.GetEndpoint();
  if (endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>() is null || endpoint.Metadata.GetMetadata<ControlPlaneAttribute>() is not null) { await next(context); return; }
  if (context.User.Identity?.IsAuthenticated != true) { context.Response.StatusCode = 401; return; }
  if (context.RequestServices.GetRequiredService<SuperAdminAccess>().IsSuperAdmin && context.RequestServices.GetRequiredService<VerifiedTenant>().TenantId is not null) { await next(context); return; }
  var metadata = endpoint.Metadata.GetMetadata<BusinessApiAttribute>();
  if (metadata is null) { context.Response.StatusCode = 403; return; }
  var db = context.RequestServices.GetRequiredService<AuthorizationDbContext>();
  var current = await db.ApiEndpoints.AsNoTracking().SingleOrDefaultAsync(e => e.Code == metadata.Code && e.IsActive, context.RequestAborted);
  if (current is null || current.Method != context.Request.Method || current.Route != ((RouteEndpoint)endpoint).RoutePattern.RawText || current.TenantWide != metadata.TenantWide) { context.Response.StatusCode = 403; return; }
  var userId = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
  var permissions = await context.RequestServices.GetRequiredService<PermissionService>().Get(userId, current.TenantWide, context.RequestAborted);
  // Legacy direct mappings are retained only for reserved built-in policies.
  // Business operation mappings are controlled by the shared platform catalog.
  var reserved = Clinic.Application.PermissionCatalog.All.Keys.ToArray();
  if (!await db.PermissionEndpoints.AnyAsync(m => m.EndpointCode == current.Code && reserved.Contains(m.PermissionCode) && permissions.Contains(m.PermissionCode), context.RequestAborted) && !await context.RequestServices.GetRequiredService<MenuAccess>().Allows(userId, current.Code, current.TenantWide, context.RequestAborted)) { context.Response.StatusCode = 403; return; }
  await next(context);
 }
}
