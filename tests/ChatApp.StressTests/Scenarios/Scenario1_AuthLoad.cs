using ChatApp.StressTests.Infrastructure;
using FluentAssertions;
using NBomber.CSharp;
using NBomber.Contracts.Stats;
using System.Net.Http.Json;
using Xunit;

namespace ChatApp.StressTests.Scenarios;

/// <summary>
/// Scenario 1 — Auth Load
///
/// Fires 50 concurrent register + login requests and asserts:
///   - 0 server errors (no 5xx)
///   - Login p99 latency ≤ 500 ms
///
/// Vulnerability targeted:
///   BCrypt is CPU-bound; default Npgsql pool size is 20 connections.
///   Under 50 parallel callers, either the thread pool or the DB pool
///   will queue. The p99 threshold surfaces whichever becomes the bottleneck.
/// </summary>
[Collection("StressTests")]
public class Scenario1_AuthLoad : IAsyncLifetime
{
    private StressTestFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new StressTestFactory();
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact(DisplayName = "S1 — 50 concurrent register+login: p99 ≤ 500ms, 0 errors")]
    public async Task ConcurrentAuthLoad_MeetsLatencyAndErrorSLOs()
    {
        // ── SETUP ────────────────────────────────────────────────────────────
        var testId = Guid.NewGuid().ToString("N")[..8];
        var creds = Enumerable.Range(0, 50).Select(i => new
        {
            Username = $"authload_{testId}_{i}",
            Email    = $"authload_{testId}_{i}@stress.test",
            Password = "Password123!"
        }).ToList();

        int credIndex = -1; // thread-safe via Interlocked

        // ── NBomber 6 scenarios ───────────────────────────────────────────────
        // NBomber 6 removed HttpStep / ScenarioBuilder / step-based API.
        // Each Scenario.Create() call defines one atomic load unit.
        // Two separate scenarios replace the old two-step approach.

        var registerScenario = Scenario.Create("register", async ctx =>
        {
            var idx  = Interlocked.Increment(ref credIndex) % creds.Count;
            var cred = creds[idx];
            using var client = _factory.CreateClient();

            var resp = await client.PostAsJsonAsync("/api/Auth/register", new
            {
                cred.Username,
                cred.Email,
                cred.Password
            });

            // 200 OK = success; 400 = duplicate (harmless retry artefact)
            return resp.IsSuccessStatusCode ||
                   resp.StatusCode == System.Net.HttpStatusCode.BadRequest
                ? Response.Ok()
                : Response.Fail();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1),
                              during: TimeSpan.FromSeconds(5))
        );

        var loginScenario = Scenario.Create("login", async ctx =>
        {
            var idx  = Interlocked.Increment(ref credIndex) % creds.Count;
            var cred = creds[idx];
            using var client = _factory.CreateClient();

            var resp = await client.PostAsJsonAsync("/api/Auth/login", new
            {
                cred.Email,
                cred.Password
            });

            return resp.IsSuccessStatusCode
                ? Response.Ok()
                : Response.Fail();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1),
                              during: TimeSpan.FromSeconds(5))
        );

        var stats = NBomberRunner
            .RegisterScenarios(registerScenario, loginScenario)
            .WithReportFolder("NBomberReports/Scenario1")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Csv)
            .Run();

        // ── ASSERT ───────────────────────────────────────────────────────────
        // In NBomber 6 there are no StepStats — access via ScenarioName directly.
        var loginStats    = stats.ScenarioStats.First(s => s.ScenarioName == "login");
        var registerStats = stats.ScenarioStats.First(s => s.ScenarioName == "register");

        loginStats.Fail.Request.Count.Should().Be(0,
            "all login requests must succeed (0 HTTP 5xx or network errors)");

        loginStats.Ok.Latency.Percent99.Should().BeLessOrEqualTo(500,
            "login p99 latency must be ≤ 500ms under 50 concurrent users — " +
            "failure indicates BCrypt CPU bottleneck or DB pool exhaustion");

        registerStats.Fail.Request.Count.Should().Be(0,
            "register must not produce 5xx errors");
    }

    [Fact(DisplayName = "S1 — 50 parallel logins with pre-seeded users: all succeed")]
    public async Task FiftyParallelLogins_AllSucceed()
    {
        // ── SETUP ────────────────────────────────────────────────────────────
        // Pre-register users outside the timed window so BCrypt cost is paid
        // before the concurrent login burst, isolating login latency only.
        var userFactory = new TestUserFactory(_factory.CreateClient());
        var users = await userFactory.CreateUsersAsync(50, "s1login");

        // ── ACT ──────────────────────────────────────────────────────────────
        var start = DateTimeOffset.UtcNow;

        var loginTasks = users.Select(async u =>
        {
            using var client = _factory.CreateClient();
            var resp = await client.PostAsJsonAsync("/api/Auth/login", new
            {
                u.Email,
                u.Password
            });
            return resp.IsSuccessStatusCode;
        });

        var results = await Task.WhenAll(loginTasks);
        var elapsed = DateTimeOffset.UtcNow - start;

        // ── ASSERT ───────────────────────────────────────────────────────────
        results.Should().AllSatisfy(ok =>
            ok.Should().BeTrue("every parallel login must return 200 OK"));

        elapsed.TotalMilliseconds.Should().BeLessOrEqualTo(5000,
            "50 parallel logins should complete within 5 seconds");
    }
}
