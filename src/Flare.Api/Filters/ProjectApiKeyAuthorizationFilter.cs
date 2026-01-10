using Flare.Application.Interfaces;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Flare.Api.Filters;

public class ProjectApiKeyAuthorizationFilter : IAsyncAuthorizationFilter
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private const string ProjectAliasHeaderName = "x-project-alias";
    private const string ScopeAliasHeaderName = "x-scope-alias";
    private readonly IApiKeyValidator _apiKeyValidator;

    public ProjectApiKeyAuthorizationFilter(IApiKeyValidator apiKeyValidator)
    {
        _apiKeyValidator = apiKeyValidator;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        string? apiKey = context.HttpContext.Request.Headers[ApiKeyHeaderName];
        if(string.IsNullOrEmpty(apiKey))
            throw new UnauthorizedAccessException("No API key provided");
        string? projectAlias = context.HttpContext.Request.Headers[ProjectAliasHeaderName];
        if(string.IsNullOrEmpty(projectAlias))
            throw new UnauthorizedAccessException("No project alias provided");
        string? scopeAlias = context.HttpContext.Request.Headers[ScopeAliasHeaderName];
        if(string.IsNullOrEmpty(scopeAlias))
            throw new UnauthorizedAccessException("No scope alias provided");
        
        var result = await _apiKeyValidator.ValidateApiKeyAsync(projectAlias, apiKey, scopeAlias);
        if(!result)
            throw new UnauthorizedAccessException();
    }
}