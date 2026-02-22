using Flare.Application.DTOs;

namespace Flare.Application.Interfaces;

public interface IProjectService
{
    Task<ProjectDetailResponseDto> CreateAsync(CreateProjectDto dto, Guid creatorUserId, string actorUsername);
    Task<ProjectDetailResponseDto> UpdateAsync(Guid projectId, UpdateProjectDto dto, Guid currentUserId, string actorUsername);
    Task DeleteAsync(Guid projectId, Guid currentUserId, string actorUsername);
    Task<ProjectDetailResponseDto> GetByIdAsync(Guid projectId, Guid currentUserId);
    Task<List<ProjectResponseDto>> GetUserProjectsAsync(Guid userId);
    Task<RegenerateApiKeyResponseDto> RegenerateApiKeyAsync(Guid projectId, Guid currentUserId, string actorUsername);
    Task ArchiveAsync(Guid projectId, Guid currentUserId, string actorUsername);
    Task UnarchiveAsync(Guid projectId, Guid currentUserId, string actorUsername);
    Task<MyPermissionsResponseDto> GetMyPermissionsAsync(Guid projectId, Guid currentUserId);
}
