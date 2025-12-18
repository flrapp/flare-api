namespace Flare.Application.Authorization;

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string ProjectViewer = "ProjectViewer";
    public const string ProjectEditor = "ProjectEditor";
    public const string ProjectOwner = "ProjectOwner";
}
