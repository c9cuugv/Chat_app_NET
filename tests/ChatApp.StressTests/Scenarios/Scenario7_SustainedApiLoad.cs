using ChatApp.StressTests.Infrastructure;
using FluentAssertions;
using NBomber.CSharp;
using NBomber.Contracts.Stats;
using System.Net.Http.Headers;
using Xunit;

namespace ChatApp.StressTests.Scenarios;

/// <summary>
/// Scenario 7 — Sustained API Load (HTTP only, no WebSocket)
///
/// 100 virtual users hit read endpoints continuously for 30 seconds,
/// simulating a realistic mix of list-users and health-check traffic.
///
/// Pass criteria:
///   - p99 latency ≤ 100 ms
///   - Error rate &lt; 1 %
///   - Throughput ≥ 200 req/sec
///
/// Vulnerability surfaced: no explicit DB connection pool config in
/// appsettings.json (default Npgsql MaxPoolSize = 20). Under 100 VUs,
/// queued connection requests will inflate p99 beyond 100 ms.
/// </summary>
[Collection("StressTests")]
public class Scenario7_SustainedApiLoad : IAsyncLifetime
{
    private StressTestFactory _factory = null!;
    private string            _token   = null!;

    public async Task InitializeAsync()
    {
        _factory = new StressTestFactory();
        await _factory.ResetDatabaseAsync();

        // Pre-seed 20 users so GET /api/Users returns realistic data
        var userFactory = new TestUserFactory(_factory.CreateClient());
        var users = await userFactory.CreateUsersAsync(20, "s7api");
        _token = users[0].Token;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact(DisplayName = "S7 — 100 VU × 30s GET /api/Users: p99 ≤ 100ms, error rate < 1%")]
    public async Task SustainedReadLoad_MeetsLatencyAndThroughputSLOs()
    {
        var capturedToken = _token;

        // ── NBomber 6 scenarios ───────────────────────────────────────────────
        // NBomber 6 removed HttpStep / ScenarioBuilder / step-based API.
        // Two separate Scenario.Create() calls replace the old two-step scenario.

        var getUsersScenario = Scenario.Create("get_users", async ctx =>
        {
            using var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", capturedToken);

            var resp = await client.GetAsync("/api/Users");
            return resp.IsSuccessStatusCode
                ? Response.Ok()
                : Response.Fail();
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: 100, during: TimeSpan.FromSeconds(30))
        );

        var getHealthScenario = Scenario.Create("get_health", async ctx =>
        {
            using var client = _factory.CreateClient();
            var resp = await client.GetAsync("/health");
            return resp.IsSuccessStatusCode
                ? Response.Ok()
                : Response.Fail();
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: 100, during: TimeSpan.FromSeconds(30))
        );

        var stats = NBomberRunner
            .RegisterScenarios(getUsersScenario, getHealthScenario)
            .WithReportFolder("NBomberReports/Scenario7")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Csv)
            .Run();

        // ── ASSERT ───────────────────────────────────────────────────────────
        // In NBomber 6 there are no StepStats — access by ScenarioName directly.
        var usersStats  = stats.ScenarioStats.First(s => s.ScenarioName == "get_users");
        var healthStats = stats.ScenarioStats.First(s => s.ScenarioName == "get_health");

        // GET /api/Users assertions
        var totalUserReqs = usersStats.AllOkCount + usersStats.AllFailCount;
        var errorRate     = totalUserReqs > 0
            ? (double)usersStats.AllFailCount / totalUserReqs * 100
            : 0;

        errorRate.Should().BeLessThan(1.0,
            $"GET /api/Users error rate must be < 1% (actual: {errorRate:F2}%)");

        usersStats.Ok.Latency.Percent99.Should().BeLessOrEqualTo(100,
            "GET /api/Users p99 must be ≤ 100ms under 100 concurrent users — " +
            "failure indicates DB connection pool exhaustion (default MaxPoolSize=20)");

        // Throughput: count both scenarios combined over 30 s
        var totalOk = usersStats.AllOkCount + healthStats.AllOkCount;
        var rps     = totalOk / 30.0;

        rps.Should().BeGreaterOrEqualTo(200,
            $"combined throughput must be ≥ 200 req/sec (actual: {rps:F0} req/sec)");

        // Health check should always be fast
        healthStats.Ok.Latency.Percent99.Should().BeLessOrEqualTo(50,
            "GET /health p99 must be ≤ 50ms (it only touches the DB connection, not data)");
    }
}
