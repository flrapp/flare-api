using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Flare.Infrastructure.Initialization;

public class DatabaseInitializer
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<DatabaseInitializer> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        var pendingMigrations = await _dbContext.Database.GetPendingMigrationsAsync();
        var migrations = pendingMigrations.ToList();
        if (migrations.Count != 0)
        {
            _logger.LogInformation("Applying {Count} pending migrations...", migrations.Count());
            await _dbContext.Database.MigrateAsync();
            _logger.LogInformation("Migrations applied successfully");
        }
        
        var usersExist = await _dbContext.Users.AnyAsync();

        if (usersExist)
        {
            _logger.LogDebug("Database already initialized, skipping admin creation");
            return;
        }

        _logger.LogInformation("No users found, initializing database...");

        var adminUsername = _configuration["ADMIN_USERNAME"];
        var adminPassword = _configuration["ADMIN_PASSWORD"];
        var adminFullName = _configuration["ADMIN_FULLNAME"] ?? "System Administrator";

        if (string.IsNullOrWhiteSpace(adminUsername) || string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException("No admin credentials configured for initialization");
        }

        if (!IsPasswordValid(adminPassword))
        {
            throw new InvalidOperationException(
                "Admin password does not meet security requirements. " +
                "Password must be at least 8 characters and contain uppercase, lowercase, and digits.");
        }

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Username = adminUsername,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword, workFactor: 12),
            FullName = adminFullName,
            GlobalRole = GlobalRole.Admin,
            IsActive = true,
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(adminUser);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Initial admin user created: {Username}", adminUsername);
    }

    private static bool IsPasswordValid(string password)
    {
        if (password.Length < 8)
            return false;

        var hasUppercase = password.Any(char.IsUpper);
        var hasLowercase = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);

        return hasUppercase && hasLowercase && hasDigit;
    }
}
