using Flare.Domain.Entities;

namespace Flare.Application.Interfaces;

public interface IApiKeyValidator
{
    Task<bool> ValidateApiKeyAsync(string projectAlias, string apiKey, string scopeAlias);

    /// <summary>
    /// Validates API key and returns the associated project.
    /// Used for Bearer token authentication where project is resolved from API key only.
    /// </summary>
    Task<Project?> ValidateApiKeyAndGetProjectAsync(string apiKey);
}