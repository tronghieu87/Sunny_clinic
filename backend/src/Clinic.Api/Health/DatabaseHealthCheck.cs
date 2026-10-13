using Clinic.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Clinic.Api.Health;

public sealed class DatabaseHealthCheck(AuthorizationDbContext authorizationDatabase, ClinicDataDbContext clinicDatabase) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var authorizationAvailable = await authorizationDatabase.Database.CanConnectAsync(cancellationToken);
            var clinicAvailable = await clinicDatabase.Database.CanConnectAsync(cancellationToken);
            return authorizationAvailable && clinicAvailable
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("One or more application databases are unavailable.");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Keep connection details and credentials out of the public response.
            return HealthCheckResult.Unhealthy("Database unavailable.");
        }
    }
}
