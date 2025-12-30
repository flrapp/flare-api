namespace Flare.Application.Authorization;

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";

    // Project-level permissions
    public const string ManageProjectSettings = "ManageProjectSettings";
    public const string ManageUsers = "ManageUsers";
    public const string ManageScopes = "ManageScopes";
    public const string ManageFeatureFlags = "ManageFeatureFlags";
    public const string ViewApiKey = "ViewApiKey";
    public const string RegenerateApiKey = "RegenerateApiKey";
    public const string DeleteProject = "DeleteProject";

    // Scope-level permissions
    public const string ReadFeatureFlags = "ReadFeatureFlags";
    public const string UpdateFeatureFlags = "UpdateFeatureFlags";
}
