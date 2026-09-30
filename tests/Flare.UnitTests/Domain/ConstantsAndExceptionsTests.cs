using Flare.Domain.Constants;
using Flare.Domain.Exceptions;

namespace Flare.UnitTests.Domain;

public class ConstantsAndExceptionsTests
{
    [Fact]
    public void CacheKeys_follow_documented_format()
    {
        Assert.Equal("feature_proj_dev_flag", CacheKeys.FeatureFlagCacheKey("proj", "dev", "flag"));
        Assert.Equal("tag_proj_flag", CacheKeys.FeatureFlagProjectCacheTag("proj", "flag"));
        Assert.Equal("tag_proj_dev", CacheKeys.ProjectScopeCacheTag("proj", "dev"));
        Assert.Equal("tag_proj", CacheKeys.ProjectCacheTag("proj"));
    }

    [Fact]
    public void AccountLockedException_permanent_message()
    {
        var ex = new AccountLockedException(isPermanent: true);

        Assert.True(ex.IsPermanent);
        Assert.Null(ex.RemainingMinutes);
        Assert.Contains("Contact your administrator", ex.Message);
    }

    [Fact]
    public void AccountLockedException_temporary_message_includes_minutes()
    {
        var ex = new AccountLockedException(isPermanent: false, remainingMinutes: 12);

        Assert.False(ex.IsPermanent);
        Assert.Equal(12, ex.RemainingMinutes);
        Assert.Contains("12 minute(s)", ex.Message);
    }

    [Fact]
    public void NotFoundException_formats_entity_and_key()
    {
        var id = Guid.NewGuid();

        Assert.Equal($"Entity 'User' ({id}) was not found.", new NotFoundException("User", id).Message);
        Assert.Equal("plain", new NotFoundException("plain").Message);
    }

    [Fact]
    public void Simple_exceptions_keep_message()
    {
        Assert.Equal("a", new BadRequestException("a").Message);
        Assert.Equal("b", new ForbiddenException("b").Message);
        Assert.Equal("c", new UnauthorizedException("c").Message);
        Assert.Equal("d", new ConflictException("d").Message);
    }

    [Fact]
    public void ValidationException_exposes_errors()
    {
        var errors = new Dictionary<string, string[]> { ["field"] = ["bad"] };

        var ex = new ValidationException(errors);

        Assert.Same(errors, ex.Errors);
        Assert.IsAssignableFrom<DomainException>(ex);
    }
}
