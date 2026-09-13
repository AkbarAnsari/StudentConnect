using Microsoft.Extensions.Diagnostics.HealthChecks;
using StudentConnect.Api.Data;

namespace StudentConnect.Api.Health;

// Backs the unauthenticated /health endpoint — a quick "can we reach Postgres?" check,
// the kind of thing a load balancer or container orchestrator polls before routing traffic.
public class DatabaseHealthCheck(Database db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await db.ExecuteScalarAsync<int>("SELECT 1", bind: null, ct);
            return HealthCheckResult.Healthy("Database reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database unreachable.", ex);
        }
    }
}
