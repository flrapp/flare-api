using Flare.Application.Interfaces;
using Flare.Domain.Constants;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Flare.Api.Filters;

public class ProjectApiKeyAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly IApiKeyValidator _apiKeyValidator;

    public ProjectApiKeyAuthorizationFilter(IApiKeyValidator apiKeyValidator)
    {
        _apiKeyValidator = apiKeyValidator;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        string? apiKey = context.HttpContext.Request.Headers[HeadersKeys.ApiKeyHeaderName];
        if(string.IsNullOrEmpty(apiKey))
            throw new UnauthorizedAccessException("No API key provided");
        string? projectAlias = context.HttpContext.Request.Headers[HeadersKeys.ProjectAliasHeaderName];
        if(string.IsNullOrEmpty(projectAlias))
            throw new UnauthorizedAccessException("No project alias provided");
        string? scopeAlias = context.HttpContext.Request.Headers[HeadersKeys.ScopeAliasHeaderName];
        if(string.IsNullOrEmpty(scopeAlias))
            throw new UnauthorizedAccessException("No scope alias provided");
        
        var result = await _apiKeyValidator.ValidateApiKeyAsync(projectAlias, apiKey, scopeAlias);
        if(!result)
            throw new UnauthorizedAccessException();
    }
}