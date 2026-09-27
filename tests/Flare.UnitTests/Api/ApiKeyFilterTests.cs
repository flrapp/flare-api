using Flare.Api.Filters;
using Flare.Application.Interfaces;
using Flare.Domain.Constants;
using Flare.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NSubstitute;

namespace Flare.UnitTests.Api;

public class ApiKeyFilterTests
{
    private readonly IApiKeyValidator _validator = Substitute.For<IApiKeyValidator>();

    private static AuthorizationFilterContext Context(params (string Name, string Value)[] headers)
    {
        var httpContext = new DefaultHttpContext();
        foreach (var (name, value) in headers)
            httpContext.Request.Headers[name] = value;
        return new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>());
    }

    #region Bearer

    [Fact]
    public async Task Bearer_valid_key_populates_project_items()
    {
        var project = new Project { Id = Guid.NewGuid(), Alias = "proj" };
        _validator.ValidateApiKeyAndGetProjectAsync("secret").Returns(project);
        var context = Context((HeadersKeys.AuthorizationHeaderName, "bearer  secret "));

        await new BearerApiKeyAuthorizationFilter(_validator).OnAuthorizationAsync(context);

        Assert.Equal(project.Id, context.HttpContext.Items[HttpContextKeys.ProjectId]);
        Assert.Equal("proj", context.HttpContext.Items[HttpContextKeys.ProjectAlias]);
    }

    [Theory]
    [InlineData(null, "Authorization header is required")]
    [InlineData("Basic abc", "Invalid authorization scheme. Bearer token expected")]
    [InlineData("Bearer    ", "API key is required")]
    [InlineData("Bearer unknown", "Invalid API key")]
    public async Task Bearer_rejects_bad_headers(string? header, string message)
    {
        var context = header is null ? Context() : Context((HeadersKeys.AuthorizationHeaderName, header));

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new BearerApiKeyAuthorizationFilter(_validator).OnAuthorizationAsync(context));

        Assert.Equal(message, ex.Message);
    }

    #endregion

    #region Project API key headers

    private static (string, string)[] ProjectHeaders(string? key = "k", string? alias = "proj", string? scope = "dev") =>
        new[] { (HeadersKeys.ApiKeyHeaderName, key), (HeadersKeys.ProjectAliasHeaderName, alias), (HeadersKeys.ScopeAliasHeaderName, scope) }
            .Where(h => h.Item2 is not null)
            .Select(h => (h.Item1, h.Item2!))
            .ToArray();

    [Fact]
    public async Task ProjectKey_valid_headers_pass()
    {
        _validator.ValidateApiKeyAsync("proj", "k", "dev").Returns(true);

        await new ProjectApiKeyAuthorizationFilter(_validator).OnAuthorizationAsync(Context(ProjectHeaders()));

        await _validator.Received(1).ValidateApiKeyAsync("proj", "k", "dev");
    }

    [Theory]
    [InlineData(null, "proj", "dev", "No API key provided")]
    [InlineData("k", null, "dev", "No project alias provided")]
    [InlineData("k", "proj", null, "No scope alias provided")]
    public async Task ProjectKey_missing_header_is_rejected(string? key, string? alias, string? scope, string message)
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new ProjectApiKeyAuthorizationFilter(_validator).OnAuthorizationAsync(Context(ProjectHeaders(key, alias, scope))));

        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public async Task ProjectKey_invalid_combination_is_rejected()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new ProjectApiKeyAuthorizationFilter(_validator).OnAuthorizationAsync(Context(ProjectHeaders())));
    }

    #endregion
}
