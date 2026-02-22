using Flare.Application.Interfaces;
using Flare.Domain.Constants;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Flare.Api.Filters;

/// <summary>
/// Authorization filter that validates Bearer token (API key) and resolves the project.
/// Used for OpenFeature-compatible SDK endpoints where scope is provided in the request body.
/// </summary>
public class BearerApiKeyAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly IApiKeyValidator _apiKeyValidator;

    public BearerApiKeyAuthorizationFilter(IApiKeyValidator apiKeyValidator)
    {
        _apiKeyValidator = apiKeyValidator;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        string? authorizationHeader = context.HttpContext.Request.Headers[HeadersKeys.AuthorizationHeaderName];

        if (string.IsNullOrEmpty(authorizationHeader))
            throw new UnauthorizedAccessException("Authorization header is required");

        if (!authorizationHeader.StartsWith(HeadersKeys.BearerPrefix, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Invalid authorization scheme. Bearer token expected");

        var apiKey = authorizationHeader[HeadersKeys.BearerPrefix.Length..].Trim();

        if (string.IsNullOrEmpty(apiKey))
            throw new UnauthorizedAccessException("API key is required");

        var project = await _apiKeyValidator.ValidateApiKeyAndGetProjectAsync(apiKey);

        if (project == null)
            throw new UnauthorizedAccessException("Invalid API key");

        context.HttpContext.Items[HttpContextKeys.ProjectId] = project.Id;
        context.HttpContext.Items[HttpContextKeys.ProjectAlias] = project.Alias;
    }
}
