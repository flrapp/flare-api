using Asp.Versioning;
using Flare.Api.Attributes;
using Flare.Application.DTOs;
using Flare.Application.Interfaces;
using Flare.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Flare.Api.Controllers.Sdk;

[ApiController]
[ApiVersion("1.0")]
[Route("sdk/v{version:apiVersion}/features")]
public class FeaturesController : ControllerBase
{
    private readonly IFeatureFlagService _featureFlagService;

    public FeaturesController(IFeatureFlagService  featureFlagService)
    {
        _featureFlagService = featureFlagService;
    }
    
    [HttpGet]
    [ProjectApiKeyAuthorization]
    public async Task<GetFeatureFlagValueDto> GetFeatureFlagValueAsync(string featureFlagAlias)
    {
        //Nullability of project and scope aliases is checked in api key attribute 
        string projectAlias = HttpContext.Request.Headers[HeadersKeys.ProjectAliasHeaderName]!;
        string scopeAlias = HttpContext.Request.Headers[HeadersKeys.ScopeAliasHeaderName]!;
        return await _featureFlagService.GetFeatureFlagValueAsync(projectAlias, scopeAlias, featureFlagAlias);
    }
}