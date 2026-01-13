using Flare.Application.DTOs;

namespace Flare.Application.Interfaces;

public interface IProjectService
{
    Task<ProjectDetailResponseDto> CreateAsync(CreateProjectDto dto, Guid creatorUserId);
    Task<ProjectDetailResponseDto> UpdateAsync(Guid projectId, UpdateProjectDto dto, Guid currentUserId);
    Task DeleteAsync(Guid projectId, Guid currentUserId);
    Task<ProjectDetailResponseDto> GetByIdAsync(Guid projectId, Guid currentUserId);
    Task<List<ProjectResponseDto>> GetUserProjectsAsync(Guid userId);
    Task<RegenerateApiKeyResponseDto> RegenerateApiKeyAsync(Guid projectId, Guid currentUserId);
    Task ArchiveAsync(Guid projectId, Guid currentUserId);
    Task UnarchiveAsync(Guid projectId, Guid currentUserId);
    Task<MyPermissionsResponseDto> GetMyPermissionsAsync(Guid projectId, Guid currentUserId);
}
