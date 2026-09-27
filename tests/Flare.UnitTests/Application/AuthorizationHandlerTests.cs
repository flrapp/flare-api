using System.Security.Claims;
using Flare.Application.Authorization.Handlers;
using Flare.Application.Authorization.Requirements;
using Flare.Application.Interfaces;
using Flare.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Flare.UnitTests.Application;

public class AuthorizationHandlerTests
{
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly IHttpContextAccessor _accessor = Substitute.For<IHttpContextAccessor>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _resourceId = Guid.NewGuid();

    private static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));

    private ClaimsPrincipal User() => Principal(new Claim(ClaimTypes.NameIdentifier, _userId.ToString()));

    private static async Task<bool> Authorize(IAuthorizationHandler handler, IAuthorizationRequirement requirement, ClaimsPrincipal user)
    {
        var context = new AuthorizationHandlerContext([requirement], user, null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private void GivenRequest(Action<HttpContext> configure)
    {
        var httpContext = new DefaultHttpContext();
        configure(httpContext);
        _accessor.HttpContext.Returns(httpContext);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("SuperAdmin", true)]
    [InlineData("User", false)]
    public async Task AdminRequirement_depends_on_role_claim(string role, bool expected)
    {
        var user = Principal(new Claim(ClaimTypes.Role, role));

        Assert.Equal(expected, await Authorize(new AdminRequirementHandler(), new AdminRequirement(), user));
    }

    [Fact]
    public async Task AdminRequirement_without_role_claim_fails()
    {
        Assert.False(await Authorize(new AdminRequirementHandler(), new AdminRequirement(), Principal()));
    }

    #region Project permission

    private ProjectPermissionRequirementHandler ProjectHandler() => new(_permissions, _accessor);
    private static ProjectPermissionRequirement ManageUsers => new(ProjectPermission.ManageUsers);

    [Fact]
    public async Task ProjectPermission_reads_project_id_from_route()
    {
        GivenRequest(c => c.Request.RouteValues["projectId"] = _resourceId.ToString());
        _permissions.HasProjectPermissionAsync(_userId, _resourceId, ProjectPermission.ManageUsers).Returns(true);

        Assert.True(await Authorize(ProjectHandler(), ManageUsers, User()));
    }

    [Fact]
    public async Task ProjectPermission_falls_back_to_query_string()
    {
        GivenRequest(c => c.Request.QueryString = new QueryString($"?projectId={_resourceId}"));
        _permissions.HasProjectPermissionAsync(_userId, _resourceId, ProjectPermission.ManageUsers).Returns(true);

        Assert.True(await Authorize(ProjectHandler(), ManageUsers, User()));
    }

    [Fact]
    public async Task ProjectPermission_denied_when_service_says_no()
    {
        GivenRequest(c => c.Request.RouteValues["projectId"] = _resourceId.ToString());

        Assert.False(await Authorize(ProjectHandler(), ManageUsers, User()));
    }

    [Fact]
    public async Task ProjectPermission_fails_without_valid_user_or_project()
    {
        GivenRequest(c => c.Request.RouteValues["projectId"] = "not-a-guid");
        Assert.False(await Authorize(ProjectHandler(), ManageUsers, User()));

        GivenRequest(c => c.Request.QueryString = new QueryString("?projectId=nope"));
        Assert.False(await Authorize(ProjectHandler(), ManageUsers, User()));

        GivenRequest(c => c.Request.RouteValues["projectId"] = _resourceId.ToString());
        Assert.False(await Authorize(ProjectHandler(), ManageUsers, Principal()));
        Assert.False(await Authorize(ProjectHandler(), ManageUsers, Principal(new Claim(ClaimTypes.NameIdentifier, "x"))));

        _accessor.HttpContext.Returns((HttpContext?)null);
        Assert.False(await Authorize(ProjectHandler(), ManageUsers, User()));

        await _permissions.DidNotReceiveWithAnyArgs().HasProjectPermissionAsync(default, default, default);
    }

    #endregion

    #region Scope permission

    private ScopePermissionRequirementHandler ScopeHandler() => new(_permissions, _accessor);
    private static ScopePermissionRequirement ReadFlags => new(ScopePermission.ReadFeatureFlags);

    [Fact]
    public async Task ScopePermission_reads_scope_id_from_route()
    {
        GivenRequest(c => c.Request.RouteValues["scopeId"] = _resourceId);
        _permissions.HasScopePermissionAsync(_userId, _resourceId, ScopePermission.ReadFeatureFlags).Returns(true);

        Assert.True(await Authorize(ScopeHandler(), ReadFlags, User()));
    }

    [Fact]
    public async Task ScopePermission_falls_back_to_query_string()
    {
        GivenRequest(c => c.Request.QueryString = new QueryString($"?scopeId={_resourceId}"));
        _permissions.HasScopePermissionAsync(_userId, _resourceId, ScopePermission.ReadFeatureFlags).Returns(true);

        Assert.True(await Authorize(ScopeHandler(), ReadFlags, User()));
    }

    [Fact]
    public async Task ScopePermission_denied_when_service_says_no()
    {
        GivenRequest(c => c.Request.RouteValues["scopeId"] = _resourceId.ToString());

        Assert.False(await Authorize(ScopeHandler(), ReadFlags, User()));
    }

    [Fact]
    public async Task ScopePermission_fails_without_valid_user_or_scope()
    {
        GivenRequest(c => c.Request.RouteValues["scopeId"] = "bad");
        Assert.False(await Authorize(ScopeHandler(), ReadFlags, User()));

        GivenRequest(c => c.Request.QueryString = new QueryString("?scopeId=bad"));
        Assert.False(await Authorize(ScopeHandler(), ReadFlags, User()));

        GivenRequest(_ => { });
        Assert.False(await Authorize(ScopeHandler(), ReadFlags, User()));
        Assert.False(await Authorize(ScopeHandler(), ReadFlags, Principal()));

        _accessor.HttpContext.Returns((HttpContext?)null);
        Assert.False(await Authorize(ScopeHandler(), ReadFlags, User()));
    }

    #endregion
}
