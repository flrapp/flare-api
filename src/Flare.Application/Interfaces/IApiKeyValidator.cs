namespace Flare.Application.Interfaces;

public interface IApiKeyValidator
{
    Task<bool> ValidateApiKeyAsync(string projectAlias, string apiKey, string scopeAlias);
}