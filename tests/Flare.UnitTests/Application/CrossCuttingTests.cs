using System.Diagnostics.Metrics;
using System.Security.Claims;
using Flare.Application;
using Flare.Application.Audit;
using Flare.Application.DTOs;
using Flare.Application.Extensions;
using Flare.Application.Interfaces;
using Flare.Application.Metrics;
using Flare.Domain.Enums;
using Flare.UnitTests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Flare.UnitTests.Application;

public class CrossCuttingTests
{
    #region AuthenticationExtensions

    [Fact]
    public async Task SignInUser_issues_persistent_cookie_with_identity_claims()
    {
        var auth = Substitute.For<IAuthenticationService>();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton(auth).BuildServiceProvider()
        };
        var result = new AuthResultDto { UserId = Guid.NewGuid(), Username = "alice", FullName = "Alice A", GlobalRole = GlobalRole.Admin };

        await httpContext.SignInUserAsync(result);

        await auth.Received(1).SignInAsync(httpContext, CookieAuthenticationDefaults.AuthenticationScheme,
            Arg.Is<ClaimsPrincipal>(p =>
                p.FindFirstValue(ClaimTypes.NameIdentifier) == result.UserId.ToString() &&
                p.FindFirstValue("Username") == "alice" &&
                p.FindFirstValue(ClaimTypes.Name) == "Alice A" &&
                p.FindFirstValue(ClaimTypes.Role) == "Admin"),
            Arg.Is<AuthenticationProperties>(props => props.IsPersistent && props.ExpiresUtc > DateTimeOffset.UtcNow.AddDays(6)));
    }

    [Fact]
    public async Task SignOutUser_signs_out_cookie_scheme()
    {
        var auth = Substitute.For<IAuthenticationService>();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton(auth).BuildServiceProvider()
        };

        await httpContext.SignOutUserAsync();

        await auth.Received(1).SignOutAsync(httpContext, CookieAuthenticationDefaults.AuthenticationScheme, Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public void GetCurrentUserId_and_username_read_claims()
    {
        var id = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim("Username", "alice")]))
        };

        Assert.Equal(id, httpContext.GetCurrentUserId());
        Assert.Equal("alice", httpContext.GetCurrentUsername());
    }

    [Fact]
    public void GetCurrentUserId_returns_null_for_missing_or_invalid_claim()
    {
        Assert.Null(new DefaultHttpContext().GetCurrentUserId());
        Assert.Null(new DefaultHttpContext().GetCurrentUsername());

        var invalid = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "not-a-guid")]))
        };
        Assert.Null(invalid.GetCurrentUserId());
    }

    #endregion

    #region SerilogAuditLogger

    private static (SerilogAuditLogger logger, CapturingLoggerProvider provider) CreateAuditLogger()
    {
        var provider = new CapturingLoggerProvider();
        var factory = LoggerFactory.Create(b => b.AddProvider(provider).SetMinimumLevel(LogLevel.Trace));
        return (new SerilogAuditLogger(factory), provider);
    }

    [Fact]
    public void Project_audit_is_written_to_project_category()
    {
        var (logger, provider) = CreateAuditLogger();

        logger.LogProjectAudit("proj", "alice", "Scope", "dev", "Created");
        logger.LogProjectAudit("proj", "alice", "Project", null, "Updated", new { Name = "a" }, new { Name = "b" });

        Assert.Equal(2, provider.Entries.Count);
        Assert.All(provider.Entries, e =>
        {
            Assert.Equal("Flare.Audit.Project", e.Category);
            Assert.Equal(LogLevel.Information, e.Level);
            Assert.Equal("proj", e.Properties["ProjectAlias"]);
        });
        Assert.Contains("Created on Scope by alice", provider.Entries[0].Message);
        Assert.True(provider.Entries[1].Properties.ContainsKey("@OldValue"));
    }

    [Fact]
    public void User_audit_is_written_to_user_category()
    {
        var (logger, provider) = CreateAuditLogger();

        logger.LogUserAudit("bob", "admin", "User", null, "Created");
        logger.LogUserAudit("bob", "admin", "User", null, "Updated", new { Role = "User" }, new { Role = "Admin" });

        Assert.Equal(2, provider.Entries.Count);
        Assert.All(provider.Entries, e =>
        {
            Assert.Equal("Flare.Audit.User", e.Category);
            Assert.Equal("bob", e.Properties["SubjectUsername"]);
            Assert.Equal("admin", e.Properties["ActorUsername"]);
        });
        Assert.True(provider.Entries[1].Properties.ContainsKey("@NewValue"));
    }

    #endregion

    #region FlareMetrics

    [Fact]
    public void Metrics_record_single_and_bulk_evaluations_with_tags()
    {
        using var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var metrics = new FlareMetrics(services.GetRequiredService<IMeterFactory>());
        var measurements = new List<(long Value, Dictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "flare.flag.evaluations")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            measurements.Add((value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value))));
        listener.Start();

        metrics.RecordEvaluation("proj", "flag", "dev");
        metrics.RecordBulkEvaluation("proj", "prod");

        Assert.Equal(2, measurements.Count);
        Assert.Equal(1, measurements[0].Value);
        Assert.Equal("flag", measurements[0].Tags["flag"]);
        Assert.Equal("dev", measurements[0].Tags["scope"]);
        Assert.False(measurements[1].Tags.ContainsKey("flag"));
        Assert.Equal("prod", measurements[1].Tags["scope"]);
    }

    #endregion

    #region DI registration

    [Fact]
    public void AddServices_and_AddAuthorizationHandler_register_application_services()
    {
        var services = new ServiceCollection();

        services.AddServices().AddAuthorizationHandler();

        Assert.Contains(services, d => d.ServiceType == typeof(IFeatureFlagService));
        Assert.Contains(services, d => d.ServiceType == typeof(IAuditLogger) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(FlareMetrics));
        Assert.Equal(3, services.Count(d => d.ServiceType == typeof(IAuthorizationHandler)));
    }

    #endregion
}
