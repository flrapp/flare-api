using Flare.Domain.Entities;
using Flare.Domain.Enums;

namespace Flare.Infrastructure.Data.Repositories.Interfaces;

public interface IProjectMemberRepository
{
    Task<ProjectMember?> GetByUserAndProjectAsync(Guid userId, Guid projectId);
    Task<ProjectRole?> GetUserProjectRoleAsync(Guid userId, Guid projectId);
    Task<bool> ExistsAsync(Guid userId, Guid projectId);
}
