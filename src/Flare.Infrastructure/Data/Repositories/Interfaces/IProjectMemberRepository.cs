using Flare.Domain.Entities;

namespace Flare.Infrastructure.Data.Repositories.Interfaces;

public interface IProjectMemberRepository
{
    Task<ProjectUser?> GetByUserAndProjectAsync(Guid userId, Guid projectId);
    Task<bool> ExistsAsync(Guid userId, Guid projectId);
}
