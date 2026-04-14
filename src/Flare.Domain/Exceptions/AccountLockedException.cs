namespace Flare.Domain.Exceptions;

public class AccountLockedException : DomainException
{
    public bool IsPermanent { get; }
    public int? RemainingMinutes { get; }

    public AccountLockedException(bool isPermanent, int? remainingMinutes = null)
        : base(BuildMessage(isPermanent, remainingMinutes))
    {
        IsPermanent = isPermanent;
        RemainingMinutes = remainingMinutes;
    }

    private static string BuildMessage(bool isPermanent, int? remainingMinutes) =>
        isPermanent
            ? "Account is locked. Contact your administrator."
            : $"Account is temporarily locked. Try again in {remainingMinutes} minute(s).";
}
