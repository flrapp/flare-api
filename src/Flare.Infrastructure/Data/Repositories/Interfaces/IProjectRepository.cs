using Flare.Domain.Entities;

namespace Flare.Infrastructure.Data.Repositories.Interfaces;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid projectId);
    Task<Project?> GetByIdWithDetailsAsync(Guid projectId);
    Task<Project?> GetByAliasAsync(string alias);
    Task<Project?> GetByApiKeyAsync(string apiKey);
    Task<List<Project>> GetByUserIdAsync(Guid userId);
    Task<List<Project>> GetAllAsync(bool includeArchived = false);
    Task<Project> AddAsync(Project project);
    Task UpdateAsync(Project project);
    Task DeleteAsync(Guid projectId);
    Task<bool> ExistsByIdAsync(Guid projectId);
    Task<bool> ExistsByAliasAsync(string alias);
    Task<bool> ExistsByAliasExcludingIdAsync(string alias, Guid projectId);
    Task<bool> ExistsByApiKeyAsync(string apiKey);
}
