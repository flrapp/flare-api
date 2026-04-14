namespace Flare.Domain.Constants;

public static class AuthConstants
{
    public const int MaxFailedAttempts = 3;
    public const int PermanentLockThreshold = 4;
    public static readonly TimeSpan TemporaryLockDuration = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan AttemptResetWindow = TimeSpan.FromHours(2);
}
