using Flare.Application.DTOs;

namespace Flare.Application.Interfaces;

public interface IScopeService
{
    Task<ScopeResponseDto> CreateAsync(Guid projectId, CreateScopeDto dto, Guid currentUserId);
    Task<ScopeResponseDto> UpdateAsync(Guid scopeId, UpdateScopeDto dto, Guid currentUserId);
    Task DeleteAsync(Guid scopeId, Guid currentUserId);
    Task<ScopeResponseDto> GetByIdAsync(Guid scopeId, Guid currentUserId);
    Task<List<ScopeResponseDto>> GetByProjectIdAsync(Guid projectId, Guid currentUserId);
}
