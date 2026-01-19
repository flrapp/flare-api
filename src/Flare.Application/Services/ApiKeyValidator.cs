using Flare.Application.Interfaces;
using Flare.Domain.Entities;
using Flare.Infrastructure.Data.Repositories.Interfaces;

namespace Flare.Application.Services;

public class ApiKeyValidator : IApiKeyValidator
{
    private readonly IProjectRepository _projectRepository;

    public ApiKeyValidator(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<bool> ValidateApiKeyAsync(string projectAlias, string apiKey, string scopeAlias)
    {
        var project = await _projectRepository.GetByAliasAsync(projectAlias);

        return project != null && project.ApiKey == apiKey && project.Scopes.Any(s => s.Alias == scopeAlias);
    }

    public async Task<Project?> ValidateApiKeyAndGetProjectAsync(string apiKey)
    {
        return await _projectRepository.GetByApiKeyAsync(apiKey);
    }
}