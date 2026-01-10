namespace Flare.Domain.Constants;

public static class CacheKeys
{
    /// <summary>
    /// feature_projectAlias_scopeAlias_featureFlagKey
    /// </summary>
    /// <param name="projectAlias"></param>
    /// <param name="scopeAlias"></param>
    /// <param name="featureFlagKey"></param>
    /// <returns></returns>
    public static string FeatureFlagCacheKey(string projectAlias, string scopeAlias, string featureFlagKey) 
        => $"feature_{projectAlias}_{scopeAlias}_{featureFlagKey}";

    /// <summary>
    /// tag_projectAlias_featureFlagKey
    /// </summary>
    /// <param name="projectAlias"></param>
    /// <param name="featureFlagKey"></param>
    /// <returns></returns>
    public static string FeatureFlagProjectCacheTag(string projectAlias,string featureFlagKey) 
        => $"tag_{projectAlias}_{featureFlagKey}";
    
    /// <summary>
    /// tag_projectAlias_scopeAlias
    /// </summary>
    /// <param name="projectAlias"></param>
    /// <param name="scopeAlias"></param>
    /// <returns></returns>
    public static string ProjectScopeCacheTag(string  projectAlias, string scopeAlias)
        => $"tag_{projectAlias}_{scopeAlias}";
    
    /// <summary>
    /// tag_projectAlias
    /// </summary>
    /// <param name="projectAlias"></param>
    /// <returns></returns>
    public static string ProjectCacheTag(string projectAlias)
        => $"tag_{projectAlias}";
}