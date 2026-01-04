using Flare.Domain.Entities;

namespace Flare.Infrastructure.Data.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid userId);
    Task<User?> GetByUsernameAsync(string username);
    Task<bool> ExistsByUsernameAsync(string username);
    Task<User> AddAsync(User user);
    Task UpdateAsync(User user);
    Task<User?> GetActiveByIdAsync(Guid userId);
    Task<User?> GetActiveByUsernameAsync(string username);
    Task<List<User>> GetAllAsync();
    Task<List<User>> GetAllActiveUsersAsync();
}
