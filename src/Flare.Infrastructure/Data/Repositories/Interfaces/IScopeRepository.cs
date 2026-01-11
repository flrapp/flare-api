using Flare.Domain.Entities;

namespace Flare.Infrastructure.Data.Repositories.Interfaces;

public interface IScopeRepository
{
    Task<Scope?> GetByIdAsync(Guid scopeId);
    Task<Scope?> GetByIdWithDetailsAsync(Guid scopeId);
    Task<Scope?> GetByProjectAndAliasAsync(Guid projectId, string alias);
    Task<List<Scope>> GetByProjectIdAsync(Guid projectId);
    Task<Scope> AddAsync(Scope scope);
    Task UpdateAsync(Scope scope);
    Task DeleteAsync(Guid scopeId);
    Task<bool> ExistsByIdAsync(Guid scopeId);
    Task<bool> ExistsByProjectAndAliasAsync(Guid projectId, string alias);
    Task<bool> ExistsByProjectAndAliasExcludingIdAsync(Guid projectId, string alias, Guid scopeId);
    Task<Scope?> GetByIdWithProjectAsync(Guid scopeId);
}
