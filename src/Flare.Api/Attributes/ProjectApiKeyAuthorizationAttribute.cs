using Flare.Api.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Flare.Api.Attributes;

public class ProjectApiKeyAuthorizationAttribute() 
    : ServiceFilterAttribute(typeof(ProjectApiKeyAuthorizationFilter));