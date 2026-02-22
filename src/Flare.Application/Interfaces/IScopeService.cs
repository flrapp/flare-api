using Flare.Application.DTOs;

namespace Flare.Application.Interfaces;

public interface IScopeService
{
    Task<ScopeResponseDto> CreateAsync(Guid projectId, CreateScopeDto dto, Guid currentUserId, string actorUsername);
    Task<ScopeResponseDto> UpdateAsync(Guid scopeId, UpdateScopeDto dto, Guid currentUserId, string actorUsername);
    Task DeleteAsync(Guid scopeId, Guid currentUserId, string actorUsername);
    Task<List<ScopeResponseDto>> GetByProjectIdAsync(Guid projectId, Guid currentUserId);
}
