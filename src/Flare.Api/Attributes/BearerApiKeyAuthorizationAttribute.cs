using Flare.Api.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Flare.Api.Attributes;

/// <summary>
/// Authorization attribute for Bearer token (API key) authentication.
/// Used for OpenFeature-compatible SDK endpoints.
/// </summary>
public class BearerApiKeyAuthorizationAttribute()
    : ServiceFilterAttribute(typeof(BearerApiKeyAuthorizationFilter));
