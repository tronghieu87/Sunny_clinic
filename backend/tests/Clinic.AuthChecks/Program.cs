using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http.Headers;
using Clinic.Application;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder().AddUserSecrets("f6c974f2-3a60-453d-a539-a8f183b996f7").Build();
var authorizationConnectionString = config.GetConnectionString("AuthorizationDb") ?? throw new InvalidOperationException("Authorization database connection missing.");
var clinicConnectionString = config.GetConnectionString("ClinicDb") ?? throw new InvalidOperationException("Clinic database connection missing.");
var options = new DbContextOptionsBuilder<AuthorizationDbContext>().UseNpgsql(authorizationConnectionString).Options;
var clinicOptions = new DbContextOptionsBuilder<ClinicDataDbContext>().UseNpgsql(clinicConnectionString).Options;
var tenantId = Guid.NewGuid(); var branchId = Guid.NewGuid(); var deniedBranchId = Guid.NewGuid(); var userId = Guid.NewGuid();
await using var db = new AuthorizationDbContext(options, new Scope(tenantId));
var targetUserId = Guid.NewGuid();
Guid? createdTenantId = null;
Guid? createdAccountUserId = null;
var syntheticEmployeeId = Guid.NewGuid();
var globalMenuId = Guid.NewGuid(); var globalOperationCode = "check.read." + Guid.NewGuid().ToString("N")[..12];
var username = "check-" + userId.ToString("N");
var password = "Aa1!" + Guid.NewGuid().ToString("N");
var user = new ClinicUser { Id = userId, UserName = username, NormalizedUserName = username.ToUpperInvariant(), SecurityStamp = Guid.NewGuid().ToString(), LockoutEnabled = true };
user.PasswordHash = new PasswordHasher<ClinicUser>().HashPassword(user, password);
var membership = new TenantMembership { TenantId = tenantId, UserId = userId, AllBranches = false };
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
using var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5080"), Timeout = TimeSpan.FromSeconds(15) };
var refreshTokens = new Dictionary<HttpClient, string>();
async Task<HttpResponseMessage> Post(string path, object? body = null, HttpClient? custom = null, HttpMethod? method = null) {
 var activeClient = custom ?? client;
 var token = await activeClient.GetFromJsonAsync<JsonElement>("/auth/csrf");
 using var request = new HttpRequestMessage(method ?? HttpMethod.Post, path);
 request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
 if (body is not null) {
  var payload = System.Text.Json.JsonSerializer.SerializeToNode(body);
  if (payload is System.Text.Json.Nodes.JsonObject obj) obj.Remove("reason"); // Every administration mutation must work without an entered reason.
  if (path == "/auth/login" && payload is System.Text.Json.Nodes.JsonObject login && login["clientType"] is null) login["clientType"] = "mobile";
  request.Content = JsonContent.Create(payload);
 }
 var response = await activeClient.SendAsync(request);
 if (response.IsSuccessStatusCode && (path == "/auth/login" || path == "/auth/workplace" || path == "/auth/refresh")) {
  var responseJson = await response.Content.ReadAsStringAsync();
  var payload = JsonDocument.Parse(responseJson).RootElement.Clone();
  response.Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json");
  if (payload.TryGetProperty("accessToken", out var accessToken)) activeClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.GetString());
  if (payload.TryGetProperty("refreshToken", out var refreshToken) && refreshToken.ValueKind == JsonValueKind.String) refreshTokens[activeClient] = refreshToken.GetString()!;
 }
 return response;
}
try {
 foreach (var item in Clinic.Application.PermissionCatalog.All) db.Permissions.Add(new Permission { TenantId = tenantId, Code = item.Key, Description = item.Value, IsSystem = true });
 db.PermissionEndpoints.AddRange(new PermissionEndpoint { TenantId = tenantId, PermissionCode = "organization.read", EndpointCode = "organization.details" }, new PermissionEndpoint { TenantId = tenantId, PermissionCode = "branches.read", EndpointCode = "organization.branches" });
 db.Users.Add(user); db.Tenants.Add(new Tenant { Id = tenantId, Code = username, Name = "Synthetic auth tenant" });
 db.Branches.AddRange(new Branch { Id = branchId, TenantId = tenantId, Code = "ALLOW", Name = "Allowed" }, new Branch { Id = deniedBranchId, TenantId = tenantId, Code = "DENY", Name = "Denied" });
 db.Memberships.Add(membership); db.BranchAccesses.Add(new BranchAccess { TenantId = tenantId, MembershipId = membership.Id, BranchId = branchId });
 await db.SaveChangesAsync();
 Check((await client.GetAsync("/auth/session")).StatusCode == HttpStatusCode.Unauthorized, "anonymous session denied");
 var noCsrfResponse = await client.PostAsJsonAsync("/auth/login", new { userName = username, password });
 Check(noCsrfResponse.StatusCode == HttpStatusCode.BadRequest, "login without CSRF denied");
 Check((await Post("/auth/login", new { userName = username, password = "wrong" })).StatusCode == HttpStatusCode.Unauthorized, "wrong password denied");
 var loginResponse = await Post("/auth/login", new { userName = username, password });
 Check(loginResponse.StatusCode == HttpStatusCode.OK && refreshTokens.ContainsKey(client), "valid JWT login returns access and refresh tokens");
 using (var rotationClient = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = client.BaseAddress }) {
  Check((await Post("/auth/login", new { userName = username, password }, rotationClient)).StatusCode == HttpStatusCode.OK, "separate device receives its own JWT session");
  var originalRefresh = refreshTokens[rotationClient];
  Check((await Post("/auth/refresh", new { refreshToken = originalRefresh }, rotationClient)).StatusCode == HttpStatusCode.OK && refreshTokens[rotationClient] != originalRefresh, "refresh token rotates and issues a new access token");
  Check((await Post("/auth/refresh", new { refreshToken = originalRefresh }, rotationClient)).StatusCode == HttpStatusCode.Unauthorized, "refresh token replay is rejected");
  Check((await rotationClient.GetAsync("/auth/session")).StatusCode == HttpStatusCode.Unauthorized, "refresh replay revokes the affected session");
 }
 using (var browserClient = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = client.BaseAddress }) {
  var browserLogin = await Post("/auth/login", new { userName = username, password, clientType = "browser" }, browserClient);
  var browserTokens = await browserLogin.Content.ReadFromJsonAsync<JsonElement>();
  var refreshCookie = string.Join(";", browserLogin.Headers.GetValues("Set-Cookie"));
  Check(browserLogin.IsSuccessStatusCode && browserTokens.GetProperty("refreshToken").ValueKind == JsonValueKind.Null &&
    refreshCookie.Contains("Clinic.Refresh=", StringComparison.Ordinal) && refreshCookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
    refreshCookie.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase), "browser login keeps refresh token only in an HttpOnly SameSite cookie");
  var csrf = await browserClient.GetFromJsonAsync<JsonElement>("/auth/csrf");
  var refreshCookieValue = refreshCookie.Split(';', StringSplitOptions.TrimEntries)
    .Single(value => value.StartsWith("Clinic.Refresh=", StringComparison.Ordinal))["Clinic.Refresh=".Length..];
  using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh") { Content = JsonContent.Create(new { }) };
  refreshRequest.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
  refreshRequest.Headers.Add("Cookie", "Clinic.Refresh=" + refreshCookieValue);
  var cookieRefresh = await browserClient.SendAsync(refreshRequest);
  var refreshedBrowserTokens = await cookieRefresh.Content.ReadFromJsonAsync<JsonElement>();
  var rotatedRefreshCookie = string.Join(";", cookieRefresh.Headers.GetValues("Set-Cookie"));
  Check(cookieRefresh.IsSuccessStatusCode && refreshedBrowserTokens.GetProperty("refreshToken").ValueKind == JsonValueKind.Null &&
    rotatedRefreshCookie.Contains("Clinic.Refresh=", StringComparison.Ordinal), "browser refresh validates the CSRF token and rotates the HttpOnly refresh cookie");
 }
 var session = await client.GetFromJsonAsync<JsonElement>("/auth/session");
 Check(session.GetProperty("tenantId").ValueKind == JsonValueKind.Null, "login does not invent tenant scope");
 var places = await client.GetFromJsonAsync<JsonElement>("/auth/workplaces");
 Check(places.GetArrayLength() == 1 && places[0].GetProperty("branchId").GetGuid() == branchId, "workplace discovery only granted branch");
 Check((await Post("/auth/workplace", new { tenantId, branchId = deniedBranchId })).StatusCode == HttpStatusCode.Forbidden, "ungranted branch denied");
 Check((await Post("/auth/workplace", new { tenantId = Guid.NewGuid(), branchId })).StatusCode == HttpStatusCode.Forbidden, "foreign tenant selection denied");
 Check((await Post("/auth/workplace", new { tenantId, branchId })).StatusCode == HttpStatusCode.OK, "authorized workplace selection returns scoped access token");
 session = await client.GetFromJsonAsync<JsonElement>("/auth/session");
 Check(session.GetProperty("tenantId").GetGuid() == tenantId && session.GetProperty("branchId").GetGuid() == branchId, "signed session contains verified scope");
 Check((await client.GetAsync("/organization")).StatusCode == HttpStatusCode.Forbidden, "membership alone grants no business permission");
 var role = new BusinessRole { TenantId = tenantId, Code = "CHECK", Name = "Synthetic role" };
 var assignment = new RoleAssignment { TenantId = tenantId, RoleId = role.Id, MembershipId = membership.Id, BranchId = branchId };
 db.BusinessRoles.Add(role);
 db.RolePermissions.AddRange(new RolePermission { TenantId = tenantId, RoleId = role.Id, PermissionCode = "organization.read" }, new RolePermission { TenantId = tenantId, RoleId = role.Id, PermissionCode = "branches.read" });
 db.RoleAssignments.Add(assignment); await db.SaveChangesAsync();
 Check((await client.GetAsync("/organization")).StatusCode == HttpStatusCode.OK, "branch role grants scoped organization read");
 Check((await client.GetAsync("/organization/branches")).StatusCode == HttpStatusCode.Forbidden, "branch role cannot enumerate all branches");
 assignment.BranchId = null; await db.SaveChangesAsync();
 Check((await client.GetAsync("/organization/branches")).StatusCode == HttpStatusCode.OK, "tenant-wide role allows branch list");
 Check((await client.GetAsync("/organization/members")).StatusCode == HttpStatusCode.Forbidden, "missing member-management permission denied");
 Check((await Post("/organization/roles", new { code = "DENIED", name = "Denied", reason = "Synthetic" })).StatusCode == HttpStatusCode.Forbidden, "role creation requires tenant administrator permission");
 role.IsActive = false; await db.SaveChangesAsync();
 Check((await client.GetAsync("/organization")).StatusCode == HttpStatusCode.Forbidden, "role revocation effective immediately");
 role.IsActive = true;
 db.RolePermissions.AddRange(new RolePermission { TenantId = tenantId, RoleId = role.Id, PermissionCode = "roles.manage" }, new RolePermission { TenantId = tenantId, RoleId = role.Id, PermissionCode = "members.manage" });
 var targetUserName = "target-" + targetUserId.ToString("N");
 db.Users.Add(new ClinicUser { Id = targetUserId, UserName = targetUserName, DisplayName = targetUserName, NormalizedUserName = targetUserName.ToUpperInvariant(), SecurityStamp = Guid.NewGuid().ToString(), PasswordHash = new PasswordHasher<ClinicUser>().HashPassword(new ClinicUser(), password) });
 var targetMember = new TenantMembership { TenantId = tenantId, UserId = targetUserId, AllBranches = true };
 db.Memberships.Add(targetMember); await db.SaveChangesAsync();
 Check((await Post("/organization/role-assignments", new { membershipId = Guid.NewGuid(), roleId = role.Id, reason = "Synthetic invalid" })).StatusCode == HttpStatusCode.NotFound, "foreign or unknown membership grant denied");
 Check((await Post("/organization/role-assignments", new { membershipId = membership.Id, roleId = role.Id, reason = "Synthetic self" })).StatusCode == HttpStatusCode.Conflict, "self permission mutation denied");
 var managedUsername = "managed-" + targetUserId.ToString("N");
 var initialManagedPassword = "Aa1!" + Guid.NewGuid().ToString("N");
 var managedDisplayName = "Test account " + targetUserId.ToString("N")[..8];
 var createdAccount = await Post("/organization/accounts", new { userName = managedUsername, displayName = managedDisplayName, email = managedUsername + "@example.test", password = initialManagedPassword, allBranches = true });
 Check(createdAccount.StatusCode == HttpStatusCode.Created, "tenant admin creates an account through Identity");
 var managedMembershipId = (await createdAccount.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
 await using (var clinicCheck = new ClinicDataDbContext(clinicOptions, new Scope(tenantId))) {
  clinicCheck.Employees.Add(new Employee { Id = syntheticEmployeeId, TenantId = tenantId, EmployeeCode = "NV-CHECK-" + syntheticEmployeeId.ToString("N")[..8], FullName = "Synthetic employee" });
  await clinicCheck.SaveChangesAsync();
 }
 var staffOptions = await client.GetFromJsonAsync<JsonElement>("/organization/accounts/employee-options");
 Check(staffOptions.EnumerateArray().Any(item => item.GetProperty("id").GetGuid() == syntheticEmployeeId), "account editor lists current-tenant employees");
 Check((await Post("/organization/accounts/" + managedMembershipId + "/employee", new { employeeId = syntheticEmployeeId }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NoContent, "account links a current-tenant employee");
 await using (var clinicCheck = new ClinicDataDbContext(clinicOptions, new Scope(tenantId)))
  Check(await clinicCheck.Employees.AnyAsync(employee => employee.Id == syntheticEmployeeId && employee.MembershipId == managedMembershipId), "employee link is persisted");
 Check((await Post("/organization/accounts/" + targetMember.Id + "/employee", new { employeeId = syntheticEmployeeId }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.Conflict, "employee cannot link to two accounts in one tenant");
 Check((await Post("/organization/accounts/" + managedMembershipId + "/employee", new { employeeId = (Guid?)null }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NoContent, "account unlinks employee");
 Check(createdAccount.Headers.Location?.ToString().EndsWith("/organization/accounts/" + managedMembershipId) == true && (await client.GetAsync(createdAccount.Headers.Location)).StatusCode == HttpStatusCode.OK, "created account Location resolves to its tenant resource");
 createdAccountUserId = await db.Memberships.Where(m => m.Id == managedMembershipId).Select(m => (Guid?)m.UserId).SingleAsync();
 Check(await db.Users.Where(u => u.Id == createdAccountUserId).Select(u => u.Email).SingleAsync() == managedUsername + "@example.test", "account email is persisted");
 Check(await db.Users.Where(u => u.Id == createdAccountUserId).Select(u => u.DisplayName).SingleAsync() == managedDisplayName, "account display name is persisted");
 var updatedDisplayName = managedDisplayName + " Updated";
 Check((await Post("/organization/accounts/" + managedMembershipId + "/display-name", new { displayName = updatedDisplayName }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NoContent, "tenant admin updates account display name");
 Check(await db.Users.Where(u => u.Id == createdAccountUserId).Select(u => u.DisplayName).SingleAsync() == updatedDisplayName, "updated display name is persisted");
 var changedManagedEmail = managedUsername + ".updated@example.test";
 Check((await Post("/organization/accounts/" + managedMembershipId + "/email", new { email = changedManagedEmail }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NoContent, "tenant admin updates account email");
 Check(await db.Users.Where(u => u.Id == createdAccountUserId).Select(u => u.Email).SingleAsync() == changedManagedEmail, "updated email is persisted");
 var addedLegacyEmail = "target-" + targetUserId.ToString("N") + "@example.test";
 Check((await Post("/organization/accounts/" + targetMember.Id + "/email", new { email = addedLegacyEmail }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NoContent, "tenant admin adds a missing email to an existing account");
 Check(await db.Users.Where(u => u.Id == targetUserId).Select(u => u.Email).SingleAsync() == addedLegacyEmail, "previously missing account email is persisted");
 await db.Entry(db.Users.Local.Single(u => u.Id == targetUserId)).ReloadAsync();
 Check((await Post("/organization/accounts/" + targetMember.Id + "/email", new { email = changedManagedEmail }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.Conflict, "duplicate email update is rejected");
 Check((await Post("/organization/accounts", new { userName = "duplicate-" + targetUserId.ToString("N"), displayName = "Duplicate account", email = changedManagedEmail, password = initialManagedPassword, allBranches = true })).StatusCode == HttpStatusCode.Conflict, "duplicate email account creation is rejected");
 Check(!await db.RoleAssignments.AnyAsync(a => a.MembershipId == managedMembershipId), "new account receives no implicit role grants");
 Check(await db.AuditEvents.AnyAsync(a => a.ResourceId == managedMembershipId && a.Action == "account.created") && await db.AuditEvents.AnyAsync(a => a.ResourceId == managedMembershipId && a.Action == "account.email_updated") && await db.AuditEvents.AnyAsync(a => a.ResourceId == managedMembershipId && a.Action == "account.display_name_updated"), "account creation, name and email changes are audited");
 Check((await Post("/organization/accounts/" + membership.Id + "/password-reset-email", null)).StatusCode == HttpStatusCode.Conflict, "password reset email requires a registered recipient address");
 Check((await Post("/auth/reset-password", new { userId = createdAccountUserId, token = "invalid", password = initialManagedPassword })).StatusCode == HttpStatusCode.BadRequest, "invalid password reset token is rejected");
 Check((await Post("/organization/accounts/" + membership.Id + "/status", new { isActive = false }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.Conflict, "administrator cannot deactivate their own tenant membership");
 using var managedClient = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = client.BaseAddress };
 Check((await Post("/auth/login", new { userName = managedUsername, password = initialManagedPassword }, managedClient)).StatusCode == HttpStatusCode.OK, "created account can authenticate with its initial password");
 var resetManagedPassword = "Bb2!" + Guid.NewGuid().ToString("N");
 Check((await Post("/organization/accounts/" + managedMembershipId + "/password", new { password = resetManagedPassword })).StatusCode == HttpStatusCode.NoContent, "tenant admin resets account password");
 Check((await managedClient.GetAsync("/auth/session")).StatusCode == HttpStatusCode.Unauthorized, "password reset revokes the account's active JWT sessions");
 using var resetClient = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = client.BaseAddress };
 Check((await Post("/auth/login", new { userName = managedUsername, password = initialManagedPassword }, resetClient)).StatusCode == HttpStatusCode.Unauthorized, "old password stops authenticating after reset");
 using var activeAccountClient = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = client.BaseAddress };
 Check((await Post("/auth/login", new { userName = managedUsername, password = resetManagedPassword }, activeAccountClient)).StatusCode == HttpStatusCode.OK, "new password authenticates after reset");
 Check((await Post("/auth/workplace", new { tenantId, branchId }, activeAccountClient)).StatusCode == HttpStatusCode.OK, "new account selects its active tenant workplace");
 Check((await Post("/organization/accounts/" + managedMembershipId + "/status", new { isActive = false }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NoContent, "tenant admin deactivates membership");
 Check((await activeAccountClient.GetFromJsonAsync<JsonElement>("/auth/workplaces")).GetArrayLength() == 0, "deactivated tenant membership loses workplace access");
 Check(await db.AuditEvents.AnyAsync(a => a.ResourceId == managedMembershipId && a.Action == "account.password_reset") && await db.AuditEvents.AnyAsync(a => a.ResourceId == managedMembershipId && a.Action == "account.deactivated"), "password reset and deactivation are audited");
 Check((await Post("/organization/accounts/" + managedMembershipId + "/status", new { isActive = true }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NoContent, "tenant admin reactivates membership");
 Check((await activeAccountClient.GetFromJsonAsync<JsonElement>("/auth/workplaces")).GetArrayLength() > 0, "reactivated tenant membership restores workplace access");
 var auditCountBeforeAssignment = await db.AuditEvents.CountAsync();
 var granted = await Post("/organization/role-assignments", new { membershipId = targetMember.Id, roleId = role.Id, branchId });
 Check(granted.StatusCode == HttpStatusCode.OK, "administrator grants role to member");
 var assignmentId = (await granted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
 Check((await Post("/organization/role-assignments/" + assignmentId + "/revoke", new { reason = "Synthetic revoke" })).StatusCode == HttpStatusCode.NoContent, "administrator revokes role");
 Check(await db.AuditEvents.CountAsync() == auditCountBeforeAssignment + 2, "grant and revoke audited atomically");
 var audit = await db.AuditEvents.FirstAsync(); audit.Reason = "Tampered";
 try { await db.SaveChangesAsync(); throw new Exception("FAIL: mutable audit"); } catch (InvalidOperationException) { Check(true, "audit mutation denied"); db.Entry(audit).State = EntityState.Unchanged; }
 var createdRole = await Post("/organization/roles", new { code = "dynamic", name = "Synthetic dynamic role" });
 Check(createdRole.StatusCode == HttpStatusCode.Created, "admin creates tenant role through API");
 var dynamicRoleId = (await createdRole.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
 var dynamicRole = await db.BusinessRoles.SingleAsync(r => r.Id == dynamicRoleId);
 Check(dynamicRole.TenantId == tenantId && dynamicRole.Code == "DYNAMIC", "created role scoped and code normalized");
 Check(await db.AuditEvents.AnyAsync(a => a.ResourceId == dynamicRoleId && a.Action == "role.created"), "role creation audited");
 Check((await Post("/organization/roles", new { code = "DYNAMIC", name = "Duplicate", reason = "Synthetic" })).StatusCode == HttpStatusCode.Conflict, "duplicate tenant role rejected");
 Check((await Post("/organization/roles", new { code = "BAD CODE", name = "Invalid", reason = "Synthetic" })).StatusCode == HttpStatusCode.BadRequest, "invalid role code rejected");
 Check((await Post("/organization/roles/" + dynamicRoleId, new { name = "Renamed group", isActive = true, reason = "Synthetic rename" }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NoContent, "group rename accepted");
 Check((await Post("/organization/roles/" + Guid.NewGuid(), new { name = "Foreign", isActive = false, reason = "Synthetic" }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NotFound, "foreign group update denied");
 Check((await Post("/organization/roles/" + dynamicRoleId, new { reason = "Synthetic deactivate" }, null, HttpMethod.Delete)).StatusCode == HttpStatusCode.NoContent, "unassigned group deactivated without deleting history");
 Check(await db.BusinessRoles.AsNoTracking().AnyAsync(r => r.Id == dynamicRoleId && !r.IsActive), "deactivated group retained");
 await Post("/organization/roles/" + dynamicRoleId, new { name = "Renamed group", isActive = true, reason = "Synthetic restore" }, null, HttpMethod.Put);
 Check((await Post("/organization/roles/" + role.Id, new { reason = "Synthetic self deletion" }, null, HttpMethod.Delete)).StatusCode == HttpStatusCode.Conflict, "assigned administrator group cannot be deactivated");
 Check(await db.AuditEvents.AnyAsync(a => a.ResourceId == dynamicRoleId && a.Action == "role.updated"), "group changes audited");
 db.RoleAssignments.Add(new RoleAssignment { TenantId = tenantId, MembershipId = targetMember.Id, RoleId = dynamicRole.Id, BranchId = branchId }); await db.SaveChangesAsync();
 Check(new PasswordHasher<ClinicUser>().VerifyHashedPassword(await db.Users.SingleAsync(u => u.Id == targetUserId), (await db.Users.SingleAsync(u => u.Id == targetUserId)).PasswordHash!, password) != PasswordVerificationResult.Failed, "synthetic target account password hash is valid");
 using var target = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = client.BaseAddress };
 var dynamicLogin = await Post("/auth/login", new { userName = "target-" + targetUserId.ToString("N"), password }, target);
 Check(dynamicLogin.StatusCode == HttpStatusCode.OK, "dynamic test member login");
 await Post("/auth/workplace", new { tenantId, branchId }, target);
 Check((await target.GetAsync("/organization")).StatusCode == HttpStatusCode.Forbidden, "unmapped business API denied");
 Check((await Post("/organization/permissions/" + globalOperationCode, new { description = "Synthetic global action", endpointCodes = Array.Empty<string>(), isActive = true }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.Forbidden, "tenant admin cannot change shared operation catalog");
 Check((await Post("/organization/accounts/" + targetMember.Id + "/super-admin", new { isSuperAdmin = true }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.Forbidden,
  "tenant administrator cannot grant global super admin");
 user.IsSuperAdmin = true; await db.SaveChangesAsync();
 var accountList = await client.GetFromJsonAsync<JsonElement>("/organization/accounts");
 Check(accountList.EnumerateArray().Any(item => item.GetProperty("userName").GetString() == username && item.GetProperty("isSuperAdmin").GetBoolean()),
  "account list distinguishes global super admin from tenant roles");
 Check((await Post("/organization/accounts/" + membership.Id + "/super-admin", new { isSuperAdmin = false }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.Conflict,
  "super admin cannot revoke own global access");
 Check((await Post("/organization/accounts/" + targetMember.Id + "/super-admin", new { isSuperAdmin = true }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NoContent,
  "super admin grants global access to another active account");
 Check((await target.GetFromJsonAsync<JsonElement>("/auth/session")).GetProperty("isSuperAdmin").GetBoolean(), "global grant takes effect in an existing session");
 Check((await Post("/organization/accounts/" + targetMember.Id + "/super-admin", new { isSuperAdmin = false }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NoContent,
  "super admin revokes global access from another account");
 Check(!(await target.GetFromJsonAsync<JsonElement>("/auth/session")).GetProperty("isSuperAdmin").GetBoolean(), "global revocation takes effect in an existing session");
 Check((await Post("/organization/permissions/" + globalOperationCode, new { description = "Synthetic global action", endpointCodes = Array.Empty<string>(), isActive = true }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NoContent, "super admin defines shared operation");
 Check((await Post("/organization/menus/" + globalMenuId, new { code = "check-menu-" + globalMenuId.ToString("N"), name = "Synthetic shared feature", kind = "function", sortOrder = 500, isActive = true, operationCodes = new[] { globalOperationCode } }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NoContent, "super admin defines shared function");
 Check((await Post("/organization/menus/" + globalMenuId + "/endpoint-config", new { operations = new[] { new { permissionCode = globalOperationCode, endpointCodes = new[] { "organization.details" } } } }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NoContent, "super admin maps shared function to API");
 Check((await Post("/organization/menus/roles/" + dynamicRole.Id, new { grants = new[] { new { menuId = globalMenuId, permissionCode = globalOperationCode } } }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NoContent, "tenant grant references shared function");
 Check((await target.GetAsync("/organization")).StatusCode == HttpStatusCode.OK, "tenant grant authorizes mapped API");
 Check((await target.GetAsync("/organization/branches")).StatusCode == HttpStatusCode.Forbidden, "different endpoint still denied");
 Check((await target.GetAsync("/WeatherForecast")).StatusCode == HttpStatusCode.Forbidden, "unclassified controller defaults denied");
 Check((await Post("/organization/permissions/roles.manage", new { description = "Invalid", endpointCodes = Array.Empty<string>(), isActive = false }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.Conflict, "reserved security permission protected");
 Check((await Post("/organization/menus/" + globalMenuId + "/endpoint-config", new { operations = new[] { new { permissionCode = globalOperationCode, endpointCodes = Array.Empty<string>() } } }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.NoContent, "super admin revokes shared API mapping");
 Check((await target.GetAsync("/organization")).StatusCode == HttpStatusCode.Forbidden, "shared mapping revocation takes effect immediately");
 var superUser = await db.Users.SingleAsync(u => u.Id == targetUserId);
 superUser.IsSuperAdmin = true; await db.SaveChangesAsync();
 var newTenant = await Post("/platform/tenants", new { name = "Synthetic created tenant" }, target);
 Check(newTenant.StatusCode == HttpStatusCode.Created, "super admin creates tenant without requiring an initial branch");
 if (newTenant.IsSuccessStatusCode) createdTenantId = (await newTenant.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
 if (createdTenantId is Guid emptyTenantId) {
  Guid? createdBranchId = null;
  var emptyBranches = await target.GetFromJsonAsync<JsonElement>($"/master-data/branches?tenantId={emptyTenantId}");
  Check(emptyBranches.GetProperty("total").GetInt32() == 0, "new tenant starts with no branches");
  var firstBranch = await Post("/master-data/branches", new { tenantId = emptyTenantId, name = "Synthetic first branch" }, target);
  Check(firstBranch.StatusCode == HttpStatusCode.Created, "super admin creates the first branch under a selected tenant");
  if (firstBranch.IsSuccessStatusCode) {
   var createdBranch = await firstBranch.Content.ReadFromJsonAsync<JsonElement>();
   createdBranchId = createdBranch.GetProperty("id").GetGuid();
   Check(createdBranch.GetProperty("code").GetString()?.StartsWith("CN-") == true, "branch code is generated by the API");
  }
  var listedBranches = await target.GetFromJsonAsync<JsonElement>($"/master-data/branches?tenantId={emptyTenantId}");
  Check(listedBranches.GetProperty("total").GetInt32() == 1, "branch list is scoped to the selected tenant");
  await using (var tenantScope = new AuthorizationDbContext(options, new Scope(emptyTenantId))) {
   Check(await tenantScope.PlatformMenus.AnyAsync(m => m.Path == "/master-data/branches" && m.IsActive), "new tenant receives the branch catalog menu before its first branch is added");
   Check(!await tenantScope.MenuItems.AnyAsync(), "new tenant does not receive a duplicate tenant menu catalog");
   var branchMenu = await tenantScope.PlatformMenus.SingleAsync(m => m.Code == "master-data.branches");
   var branchGroup = await tenantScope.MenuItems.IgnoreQueryFilters().SingleOrDefaultAsync(m => m.TenantId == emptyTenantId && m.Code == "clinic-branches");
   var organizationMenu = await tenantScope.PlatformMenus.SingleAsync(m => m.Code == "clinic-organization");
   Check(branchMenu.ParentId == organizationMenu.Id && (branchGroup is null || !branchGroup.IsActive),
    "branch catalog appears directly under clinic organization and old group is retired");
   Check(await tenantScope.PlatformMenuEndpoints.AnyAsync(m => m.EndpointCode == "branches.catalog.create"), "branch operation is mapped to its registered API");
  }
  if (createdBranchId is Guid branchToKeep)
   Check((await Post($"/master-data/branches/{branchToKeep}/status", new { isActive = false, tenantId = emptyTenantId }, target, HttpMethod.Put)).StatusCode == HttpStatusCode.Conflict,
    "the last active branch cannot be deactivated");
  Check((await Post("/master-data/branches", new { tenantId = Guid.NewGuid(), name = "Cross tenant branch" }, target)).StatusCode == HttpStatusCode.Forbidden,
   "super admin cannot create a branch for an unknown tenant");
 }
 Check((await target.GetFromJsonAsync<JsonElement>("/auth/session")).GetProperty("tenantId").GetGuid() == tenantId, "tenant provisioning preserves current session scope");
 Check(!await db.Tenants.AnyAsync(t => t.Id == createdTenantId), "created tenant invisible in another tenant context");

 Check((await target.GetFromJsonAsync<JsonElement>("/auth/session")).GetProperty("isSuperAdmin").GetBoolean(), "super admin status loaded from database immediately");
 Check((await target.GetAsync("/organization")).StatusCode == HttpStatusCode.OK, "super admin bypasses business API permission mapping");
 Check((await target.GetAsync("/organization/members")).StatusCode == HttpStatusCode.OK, "super admin bypasses protected permission policy");
 Check((await target.GetAsync("/WeatherForecast")).StatusCode == HttpStatusCode.OK, "super admin bypasses unmapped API permission");
 Check((await target.GetFromJsonAsync<JsonElement>("/auth/workplaces")).GetArrayLength() >= 2, "super admin discovers active workplaces");
 Check((await Post("/auth/workplace", new { tenantId, branchId = deniedBranchId }, target)).StatusCode == HttpStatusCode.OK, "super admin selects explicit active branch");
 Check((await Post("/auth/workplace", new { tenantId = Guid.NewGuid(), branchId }, target)).StatusCode == HttpStatusCode.Forbidden, "super admin cannot forge tenant branch scope");
 superUser.IsSuperAdmin = false; await db.SaveChangesAsync();
 Check((await target.GetAsync("/organization/members")).StatusCode == HttpStatusCode.Forbidden, "super admin revocation applies to existing session immediately");
 if (createdTenantId is Guid crossTenantId)
  Check((await Post("/master-data/branches", new { tenantId = crossTenantId, name = "Unauthorized branch" }, target)).StatusCode == HttpStatusCode.Forbidden,
   "tenant user cannot create a branch in a different tenant");

 user.IsSuperAdmin = false; await db.SaveChangesAsync();
 Check((await client.GetAsync("/platform/tenants")).StatusCode == HttpStatusCode.Forbidden, "tenant catalog requires platform super admin");
 Check((await Post("/organization/permissions/" + globalOperationCode, new { description = "Denied edit", endpointCodes = Array.Empty<string>(), isActive = true }, null, HttpMethod.Put)).StatusCode == HttpStatusCode.Forbidden,
  "revoked super admin cannot edit shared operations");
 membership.IsActive = false; await db.SaveChangesAsync();
 session = await client.GetFromJsonAsync<JsonElement>("/auth/session");
 Check(session.GetProperty("tenantId").ValueKind == JsonValueKind.Null, "revoked membership removes verified scope immediately");
 Check((await Post("/auth/workplace", new { tenantId, branchId })).StatusCode == HttpStatusCode.Forbidden, "revoked membership cannot reselect");
 Check((await Post("/auth/logout")).StatusCode == HttpStatusCode.NoContent, "logout after revocation allowed");
 Check((await client.GetAsync("/auth/session")).StatusCode == HttpStatusCode.Unauthorized, "logout invalidates client session");
} finally {
 await using (var clinicCleanup = new ClinicDataDbContext(clinicOptions, new Scope(tenantId))) {
  await clinicCleanup.AuditEvents.Where(a => a.ResourceId == syntheticEmployeeId).ExecuteDeleteAsync();
  await clinicCleanup.Employees.Where(employee => employee.Id == syntheticEmployeeId).ExecuteDeleteAsync();
 }
 if (createdTenantId is Guid provisioned) {
  await using var clinicCleanup = new ClinicDataDbContext(clinicOptions, new Scope(provisioned));
  await using var clinicTx = await clinicCleanup.Database.BeginTransactionAsync();
  await clinicCleanup.CategoryItems.Where(item => item.TenantId == provisioned).ExecuteDeleteAsync();
  await clinicCleanup.CategoryGroups.Where(group => group.TenantId == provisioned).ExecuteDeleteAsync();
  await clinicTx.CommitAsync();
  await using var other = new AuthorizationDbContext(options, new Scope(provisioned));
  await using var otherTx = await other.Database.BeginTransactionAsync();
  await other.AuditEvents.ExecuteDeleteAsync();
  await other.TenantRoleMenuGrants.ExecuteDeleteAsync();
  await other.RoleMenuOperations.ExecuteDeleteAsync(); await other.MenuOperationEndpoints.ExecuteDeleteAsync(); await other.MenuOperations.ExecuteDeleteAsync(); await other.MenuItems.ExecuteDeleteAsync();
  await other.PermissionEndpoints.ExecuteDeleteAsync(); await other.RolePermissions.ExecuteDeleteAsync(); await other.Permissions.ExecuteDeleteAsync();
  await other.RoleAssignments.ExecuteDeleteAsync(); await other.BranchAccesses.ExecuteDeleteAsync(); await other.Memberships.ExecuteDeleteAsync(); await other.BusinessRoles.ExecuteDeleteAsync();
  await other.Branches.ExecuteDeleteAsync(); await other.Tenants.ExecuteDeleteAsync(); await otherTx.CommitAsync();
 }
 // Delete only this run's synthetic records, in dependency order, within one transaction.
 await using var cleanup = new AuthorizationDbContext(options, new Scope(tenantId));
 await using var tx = await cleanup.Database.BeginTransactionAsync();
 await cleanup.AuditEvents.Where(a => a.TenantId == tenantId).ExecuteDeleteAsync();
 await cleanup.RoleAssignments.Where(a => a.TenantId == tenantId).ExecuteDeleteAsync();
 await cleanup.TenantRoleMenuGrants.Where(m => m.TenantId == tenantId).ExecuteDeleteAsync();
 await cleanup.PlatformMenuEndpoints.Where(m => m.MenuId == globalMenuId).ExecuteDeleteAsync();
 await cleanup.PlatformMenuOperations.Where(m => m.MenuId == globalMenuId).ExecuteDeleteAsync();
 await cleanup.PlatformMenus.Where(m => m.Id == globalMenuId).ExecuteDeleteAsync();
 await cleanup.PlatformOperations.Where(m => m.Code == globalOperationCode).ExecuteDeleteAsync();
 await cleanup.RoleMenuOperations.Where(m => m.TenantId == tenantId).ExecuteDeleteAsync();
 await cleanup.MenuOperationEndpoints.Where(m => m.TenantId == tenantId).ExecuteDeleteAsync();
 await cleanup.MenuOperations.Where(m => m.TenantId == tenantId).ExecuteDeleteAsync();
 await cleanup.MenuItems.Where(m => m.TenantId == tenantId && m.ParentId != null).ExecuteDeleteAsync();
 await cleanup.MenuItems.Where(m => m.TenantId == tenantId).ExecuteDeleteAsync();
 await cleanup.PermissionEndpoints.Where(p => p.TenantId == tenantId).ExecuteDeleteAsync();
 await cleanup.RolePermissions.Where(p => p.TenantId == tenantId).ExecuteDeleteAsync();
 await cleanup.Permissions.Where(p => p.TenantId == tenantId).ExecuteDeleteAsync();
 await cleanup.BusinessRoles.Where(r => r.TenantId == tenantId).ExecuteDeleteAsync();
 await cleanup.BranchAccesses.Where(a => a.TenantId == tenantId).ExecuteDeleteAsync();
 await cleanup.Memberships.Where(m => m.TenantId == tenantId).ExecuteDeleteAsync();
 await cleanup.Branches.Where(b => b.TenantId == tenantId).ExecuteDeleteAsync();
 await cleanup.Tenants.Where(t => t.Id == tenantId).ExecuteDeleteAsync();
 await cleanup.Users.Where(u => u.Id == userId || u.Id == targetUserId || u.Id == createdAccountUserId).ExecuteDeleteAsync();
 await tx.CommitAsync();
}
Console.WriteLine("All authentication checks passed; synthetic records removed.");
sealed record Scope(Guid? TenantId) : ICurrentTenant;
