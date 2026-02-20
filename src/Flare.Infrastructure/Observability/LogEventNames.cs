namespace Flare.Infrastructure.Observability;

/// <summary>
/// Strongly-typed constants for consistent log event naming across the application.
/// </summary>
public static class LogEventNames
{
    public static class Http
    {
        public static class Request
        {
            public const string Completed = "Http.Request.Completed";
            public const string Failed = "Http.Request.Failed";
        }
    }

    public static class Auth
    {
        public static class ApiKey
        {
            public const string ValidationSucceeded = "Auth.ApiKey.ValidationSucceeded";
            public const string ValidationFailed = "Auth.ApiKey.ValidationFailed";
        }

        public static class BearerToken
        {
            public const string ValidationSucceeded = "Auth.BearerToken.ValidationSucceeded";
            public const string ValidationFailed = "Auth.BearerToken.ValidationFailed";
        }

        public static class Permission
        {
            public const string CheckSucceeded = "Auth.Permission.CheckSucceeded";
            public const string CheckFailed = "Auth.Permission.CheckFailed";
        }
    }

    public static class FeatureFlag
    {
        public static class Evaluation
        {
            public const string Completed = "FeatureFlag.Evaluation.Completed";
            public const string Failed = "FeatureFlag.Evaluation.Failed";
        }
    }

    public static class Exception
    {
        public const string Unhandled = "Exception.Unhandled";
        public const string Validation = "Exception.Validation";
    }
}
