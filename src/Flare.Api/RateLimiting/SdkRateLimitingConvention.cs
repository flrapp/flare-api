using Flare.Api.Controllers.Sdk;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.RateLimiting;

namespace Flare.Api.RateLimiting;

internal sealed class SdkRateLimitingConvention : IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        if (action.Controller.ControllerType != typeof(FlagsController))
            return;

        foreach (var selector in action.Selectors)
            selector.EndpointMetadata.Add(new EnableRateLimitingAttribute(RateLimitingOptions.SdkEvaluationPolicyName));
    }
}
