using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Clinic.Application;
using Clinic.Api.Health;
using Clinic.Api.Security;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var authorizationConnectionString = builder.Configuration.GetConnectionString("AuthorizationDb")
 ?? throw new InvalidOperationException("Connection string 'AuthorizationDb' is missing.");
var clinicConnectionString = builder.Configuration.GetConnectionString("ClinicDb")
 ?? throw new InvalidOperationException("Connection string 'ClinicDb' is missing.");
var authorizationDatabase = new Npgsql.NpgsqlConnectionStringBuilder(authorizationConnectionString);
var clinicDatabase = new Npgsql.NpgsqlConnectionStringBuilder(clinicConnectionString);
if (!string.Equals(authorizationDatabase.Host, clinicDatabase.Host, StringComparison.OrdinalIgnoreCase) ||
    authorizationDatabase.Port != clinicDatabase.Port ||
    string.Equals(authorizationDatabase.Database, clinicDatabase.Database, StringComparison.OrdinalIgnoreCase))
 throw new InvalidOperationException("AuthorizationDb and ClinicDb must use the same PostgreSQL instance and different database names.");
builder.Services.AddScoped<VerifiedTenant>();
builder.Services.AddScoped<SuperAdminAccess>();
builder.Services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<VerifiedTenant>());
builder.Services.AddScoped<AccessResolver>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<MenuAccess>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<SmtpEmailSender>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionHandler>();
builder.Services.AddAuthorization(o => {
 foreach (var code in PermissionCatalog.All.Keys) {
  o.AddPolicy("permission:" + code, p => p.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(code, false)));
  o.AddPolicy("tenant:" + code, p => p.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(code, true)));
 }
});
builder.Services.AddDbContext<AuthorizationDbContext>(o => o.UseNpgsql(authorizationConnectionString));
builder.Services.AddDbContext<ClinicDataDbContext>(o => o.UseNpgsql(clinicConnectionString));
builder.Services.AddIdentity<ClinicUser, IdentityRole<Guid>>(o => {
 o.Password.RequiredLength = 8;
 o.User.RequireUniqueEmail = true;
 o.Lockout.MaxFailedAccessAttempts = 5;
 o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<AuthorizationDbContext>().AddDefaultTokenProviders();
builder.Services.AddSingleton<SessionTokenService>();
builder.Services.AddAuthentication(options => {
 options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
 options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
 options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options => {
 var issuer = builder.Configuration["Jwt:Issuer"] ?? "Sunheart.Clinic";
 var audience = builder.Configuration["Jwt:Audience"] ?? "Sunheart.Clinic.Api";
 var signingKey = builder.Configuration["Jwt:SigningKey"];
 if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
  throw new InvalidOperationException("JWT signing key is missing or too short. Configure Jwt:SigningKey using a secret store.");
 options.MapInboundClaims = true;
 options.TokenValidationParameters = new TokenValidationParameters {
  ValidateIssuer = true, ValidIssuer = issuer,
  ValidateAudience = true, ValidAudience = audience,
  ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
  ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
  NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role
 };
});
builder.Services.ConfigureApplicationCookie(o => {
 o.Cookie.Name = "Clinic.Session";
 o.Cookie.HttpOnly = true;
 o.Cookie.SameSite = SameSiteMode.Lax;
 o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
 o.ExpireTimeSpan = TimeSpan.FromHours(8);
 o.SlidingExpiration = false;
 o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
 o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAntiforgery(o => {
 o.HeaderName = "X-CSRF-TOKEN";
 o.Cookie.Name = "Clinic.Csrf";
 o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(o => {
 o.RejectionStatusCode = 429;
 var loginLimit = builder.Configuration.GetValue("Security:LoginPerMinute", builder.Environment.IsDevelopment() ? 50 : 10);
 o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = Math.Clamp(loginLimit, 5, 100), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("databases", tags: ["ready"]);
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(c => {
 c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
 {
  Title = "Clinic API",
  Version = "v1",
  Description = "Development API reference. Business APIs still require an authenticated session, selected workplace and configured permissions."
 });
 c.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
 c.OperationFilter<BusinessApiOperationFilter>();
 c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
 {
  Description = "JWT access token. Enter the token value without the 'Bearer ' prefix.",
  Name = "Authorization", In = Microsoft.OpenApi.ParameterLocation.Header,
  Type = Microsoft.OpenApi.SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT"
 });
 c.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
 {
  [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = []
 });
});
var app = builder.Build();
if (args.Contains("--promote-super-admin")) {
 if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Local promotion is development-only.");
 var index = Array.IndexOf(args, "--promote-super-admin");
 if (index + 1 >= args.Length) throw new InvalidOperationException("Account name required.");
 await LocalSetup.Promote(app.Services, args[index + 1]); return;
}
if (args.Contains("--setup-local") || args.Contains("--setup-super-admin")) {
 if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Local setup is development-only.");
 await LocalSetup.Run(app.Services, args.Contains("--setup-super-admin")); return;
}
if (args.Contains("--import-administrative-units")) {
 if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Local reference catalog import is development-only.");
 await AdministrativeUnitImporter.ImportAsync(app.Services); return;
}
if (args.Contains("--import-icd10")) {
 if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Local reference catalog import is development-only.");
 await Icd10Importer.ImportAsync(app.Services); return;
}
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) {
 app.MapOpenApi();
 app.UseSwagger(options => options.PreSerializeFilters.Add((document, _) => document.Servers = [new Microsoft.OpenApi.OpenApiServer { Url = "/api" }]));
 app.UseSwaggerUI(options => {
  options.SwaggerEndpoint("./v1/swagger.json", "Clinic API v1");
  options.UseRequestInterceptor("""(request) => { request.credentials = `same-origin`; if (/(POST|PUT|PATCH|DELETE)/i.test(request.method || `GET`)) return fetch(`/auth/csrf`, { credentials: `same-origin`, cache: `no-store` }).then(response => response.ok ? response.json() : null).then(body => { if (body) { request.headers = request.headers || {}; request.headers[`X-CSRF-TOKEN`] = body.token; } return request; }); return request; }""");
 });
}
else app.UseHsts();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.Use(async (context, next) => {
 context.Response.Headers.CacheControl = "no-store";
 if (context.User.Identity?.IsAuthenticated == true) {
  var db = context.RequestServices.GetRequiredService<AuthorizationDbContext>();
  if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
      !await db.Users.AnyAsync(u => u.Id == userId && u.IsActive, context.RequestAborted)) { context.Response.StatusCode = 401; return; }
  if (!SessionTokenService.TryGetSessionId(context.User, out var sessionId)) { context.Response.StatusCode = 401; return; }
  var activeSession = await db.UserSessions.AsNoTracking().Where(s => s.Id == sessionId && s.UserId == userId && s.RevokedAtUtc == null && s.ExpiresAtUtc > DateTime.UtcNow)
   .Select(s => new { s.TenantId, s.BranchId }).SingleOrDefaultAsync(context.RequestAborted);
  if (activeSession is null || !GuidMatches(context.User.FindFirstValue("clinic_tenant"), activeSession.TenantId) ||
      !GuidMatches(context.User.FindFirstValue("clinic_branch"), activeSession.BranchId)) { context.Response.StatusCode = 401; return; }
  var isSuperAdmin = await db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.IsSuperAdmin, context.RequestAborted);
  context.RequestServices.GetRequiredService<SuperAdminAccess>().IsSuperAdmin = isSuperAdmin;
  var tenantClaim = context.User.FindFirstValue("clinic_tenant");
  var branchClaim = context.User.FindFirstValue("clinic_branch");
  if (tenantClaim is not null || branchClaim is not null) {
   var valid = Guid.TryParse(tenantClaim, out var tenantId) && Guid.TryParse(branchClaim, out var branchId);
   if (!valid || !await context.RequestServices.GetRequiredService<AccessResolver>().CanAccess(userId, tenantId, Guid.Parse(branchClaim!), context.RequestAborted)) {
    // Allow logout and selecting another authorized workplace after revocation.
    if (!(context.Request.Path.StartsWithSegments("/auth"))) { context.Response.StatusCode = 403; return; }
   } else context.RequestServices.GetRequiredService<VerifiedTenant>().Set(tenantId, Guid.Parse(branchClaim!), isSuperAdmin);
  }
 }
 await next();
});
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) => {
 if (!(context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) &&
     (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method))) {
  try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
  catch (AntiforgeryValidationException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = "Phiên xác thực không hợp lệ. Vui lòng thử lại." }); return; }
 }
 await next();
});
app.Use(EndpointAccess.Check);
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
await EndpointAccess.Sync(app);
await ClinicStructureMenuSetup.Sync(app.Services);
await ClinicStructureDataSetup.Sync(app.Services);
await StandardCategorySetup.Sync(app.Services);
await EmployeePositionSetup.Sync(app.Services);
app.Run();

static bool GuidMatches(string? claim, Guid? stored) => stored is null ? claim is null : Guid.TryParse(claim, out var parsed) && parsed == stored;
