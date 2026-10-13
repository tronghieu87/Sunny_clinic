# Clinic — development

Current scope: authentication, tenant/branch access, role assignments and tenant-scoped API permission administration. Patient workflows are not implemented yet. Use no real patient data.

From `Clinic`, start PostgreSQL:

```sh
/Applications/Docker.app/Contents/Resources/bin/docker compose up -d
```

Local development uses one PostgreSQL 18 instance with two databases: `clinic_authorization` for identity, tenant and permission administration, and `clinic_data` for clinic records. Both .NET connection strings must target `localhost:5432` and use their respective database names. Configure `ConnectionStrings:AuthorizationDb` and `ConnectionStrings:ClinicDb` in .NET User Secrets; the API rejects missing, same-database, or different-instance configuration. The `clinic_data` database is initialized automatically when the PostgreSQL volume is first created. Existing volumes are preserved; the former second-instance data was copied into `clinic_data` on the shared instance.

From `Clinic/backend`, start the API (uses existing development user-secrets):

```sh
ASPNETCORE_ENVIRONMENT=Development /usr/local/share/dotnet/dotnet run --project src/Clinic.Api --no-launch-profile --urls http://127.0.0.1:5080
```

From `Clinic/frontend`, start the UI:

```sh
npm ci
npm run dev
```

Open http://127.0.0.1:5173. The Vite development proxy sends `/api/*` to the local API. Production hosting must configure its own reverse proxy; the development proxy is not part of the production bundle. No frontend secrets required.

Authentication uses signed JWT bearer access tokens (10 minutes) and opaque, rotating refresh tokens (30-day absolute session) stored as hashes in `clinic_authorization`. The browser keeps the access token in memory and the refresh token in an HttpOnly, SameSite=Lax cookie; mobile login can request `clientType: "mobile"` and receives its refresh token in the response for secure OS storage. API requests validate the active database session and selected tenant/branch on every request, so logout, password reset and session revocation take effect immediately. Create a local signing key without putting it in source control or shell history:

```sh
JWT_SIGNING_KEY=$(openssl rand -hex 64)
dotnet user-secrets set --project src/Clinic.Api 'Jwt:SigningKey' "$JWT_SIGNING_KEY" >/dev/null
unset JWT_SIGNING_KEY
```

Deployments must set `Jwt__SigningKey` in their secret store. Swagger supports the Bearer scheme. Partner client-credentials/OIDC onboarding and asymmetric signing-key rotation remain separate integration work; do not share a clinic user's password with partners.

Verification: `npm run build`, `npm run lint`, and the integration checks below. Private backend status URLs: `/health/live` and `/health/ready`.

Tenant/branch foundation is migrated. Patient records now require a tenant and a patient code. Organization endpoints are protected. Current tenant comes from verified membership. Query filters and write guards are application safeguards; PostgreSQL RLS and a restricted runtime role are still pending. Do not use `IgnoreQueryFilters`, raw SQL or bulk writes in business code without explicit scope enforcement.

Isolation checks (from `backend`):

```sh
/usr/local/share/dotnet/dotnet run --project tests/Clinic.IsolationChecks
```

These use development user-secrets and synthetic records in a transaction that is rolled back, including on failure. The foundation migration refuses legacy patient records without explicit owner mapping; it never assigns them automatically.

## Local account setup

No default account or public registration endpoint is created. From `backend`, run in your own interactive terminal:

```sh
ASPNETCORE_ENVIRONMENT=Development /usr/local/share/dotnet/dotnet run --project src/Clinic.Api --no-launch-profile -- --setup-local
```

Enter the organization, branch, username and password (input hidden). Identity requires at least 8 characters including uppercase, lowercase, digit and symbol. This creates a new local organization and a member with a management role scoped to that organization (no automatic clinical permissions). Existing members are not upgraded. Start the API normally afterwards. Do not put passwords in chat, command-line arguments or frontend configuration.

Authentication uses an HttpOnly cookie, fresh CSRF tokens for state-changing requests, login rate limiting, Identity lockout and per-request verification of active workplace access. Development permits local HTTP; production requires HTTPS. Dynamic API permissions and role assignment administration are implemented; detailed clinical/financial permissions, MFA/account recovery, audit expansion, RLS, restricted runtime DB credentials and production Data Protection key encryption remain pending.

Account administration requires a unique email address for each login account. Password-reset links use ASP.NET Identity one-time tokens and are delivered through SMTP. Configure these backend user-secrets or environment variables; do not commit SMTP credentials:

```sh
cd backend
/usr/local/share/dotnet/dotnet user-secrets set --project src/Clinic.Api "Email:PublicBaseUrl" "http://127.0.0.1:5173"
/usr/local/share/dotnet/dotnet user-secrets set --project src/Clinic.Api "Email:Smtp:Host" "smtp.example.com"
/usr/local/share/dotnet/dotnet user-secrets set --project src/Clinic.Api "Email:Smtp:Port" "587"
/usr/local/share/dotnet/dotnet user-secrets set --project src/Clinic.Api "Email:Smtp:UserName" "clinic-mailer"
/usr/local/share/dotnet/dotnet user-secrets set --project src/Clinic.Api "Email:Smtp:Password" "SET_THIS_PRIVATELY_IN_YOUR_TERMINAL"
/usr/local/share/dotnet/dotnet user-secrets set --project src/Clinic.Api "Email:Smtp:FromAddress" "no-reply@example.com"
/usr/local/share/dotnet/dotnet user-secrets set --project src/Clinic.Api "Email:Smtp:FromName" "Clinic"
/usr/local/share/dotnet/dotnet user-secrets set --project src/Clinic.Api "Email:Smtp:UseSsl" "false"
```

The password command is a placeholder: replace it locally in your terminal and never send the SMTP password in chat. User Secrets are stored outside the repository under `~/.microsoft/usersecrets/f6c974f2-3a60-453d-a539-a8f183b996f7/secrets.json`. For a server, supply the corresponding `Email__...` environment variables through its secret manager instead. Use `Email:Smtp:UseSsl=true` for implicit TLS (commonly port 465); otherwise the sender requires STARTTLS. `Email:Smtp:CheckCertificateRevocation` defaults to `true` in production. Local Development sets it to `false` because this host reports `RevocationStatusUnknown`; normal certificate-chain and hostname validation remain enabled. `PublicBaseUrl` must be HTTPS outside local development. Restart the API after changing settings. Without valid SMTP settings, the admin action returns a service-unavailable error instead of claiming the email was sent. Password reset applies to the shared login account; deactivating a user from the admin page disables only that tenant membership.

Auth integration checks, with the local API running:

```sh
/usr/local/share/dotnet/dotnet run --project tests/Clinic.AuthChecks
```

These create a randomly named synthetic account/organization, exercise HTTP login and workplace access, then remove only those records in a finally block. If forcibly interrupted, synthetic records may need explicit cleanup. Never run against production.

## API permissions

Administrators define tenant-scoped permissions, select registered business APIs, then assign permissions to roles. API identity includes method and route; new controller APIs are denied until explicitly classified and mapped. Developers mark business endpoints with stable BusinessApi metadata; startup synchronizes the actual endpoint catalog. Auth and permission-administration endpoints retain protected control-plane policies and cannot be mapped as business permissions. Membership and branch scope remain mandatory.

Migration DynamicApiPermissions preserves existing permissions and grants by tenant and archives the original catalog in system_permission_templates. Downgrade cannot safely merge tenant customizations into the old global catalog; use a forward migration or restore a backup.

## Super admin and navigation

After login and workplace selection, the default page is /#/work. Permission administration is separate at /#/permissions and visible only to authorized administrators. The working page currently shows workplace information; clinical workflows are still pending.

For an explicitly requested new development super-admin account, run from backend in an interactive terminal:

```sh
ASPNETCORE_ENVIRONMENT=Development /usr/local/share/dotnet/dotnet run --project src/Clinic.Api --no-launch-profile -- --setup-super-admin
```

This creates a new account, initial clinic/branch and an audit event; it does not promote existing accounts. Enter the password privately (8+ characters with uppercase, lowercase, digit and symbol). Super-admin status is read from the active database user each request, bypasses API permission policies/mappings, and is never granted through editable business roles. It can select any active workplace, but business queries/writes retain the explicitly selected tenant scope. Authentication, CSRF and business validation remain enforced.

## Six administration pages

Permission administration now has separate pages for tenant catalog, user groups, operation definitions, menu/function configuration, group function grants and API mappings. Roles are user groups. Configure operation -> menu/function with path and operations -> group grants -> function/operation API mapping, then assign existing employees to groups. Tenant creation is super-admin only; choose the new workplace to configure it. Menu deletion deactivates and retains history. Paths target implemented screens; configuring a path does not generate a new screen. Member provisioning and clinical screens are still pending.

New tenant-scoped tables: menu_items, menu_operations, role_menu_operations, menu_operation_endpoints. Existing foundation/legacy permission grants are retained; new grants distinguish the same operation used in different functions. API access remains enforced by the backend even if client navigation is manipulated.
