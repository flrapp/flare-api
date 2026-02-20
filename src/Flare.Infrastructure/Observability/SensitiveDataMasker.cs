using System.Text.RegularExpressions;

namespace Flare.Infrastructure.Observability;

/// <summary>
/// Utility for masking sensitive data in logs.
/// </summary>
public static partial class SensitiveDataMasker
{
    private static readonly string[] SensitiveFieldNames =
    [
        "password",
        "secret",
        "apikey",
        "api_key",
        "token",
        "authorization",
        "credential",
        "bearer"
    ];

    /// <summary>
    /// Masks an API key showing only the first 4 and last 4 characters.
    /// </summary>
    public static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return "***empty***";

        if (apiKey.Length <= 8)
            return "***masked***";

        return $"{apiKey[..4]}...{apiKey[^4..]}";
    }

    /// <summary>
    /// Masks a Bearer token.
    /// </summary>
    public static string MaskBearerToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return "***empty***";

        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return "Bearer ***masked***";

        return "***masked***";
    }

    /// <summary>
    /// Masks sensitive fields in a JSON string.
    /// </summary>
    public static string MaskSensitiveFields(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return string.Empty;

        var result = json;

        // Mask patterns like "password": "value" or "password":"value"
        foreach (var field in SensitiveFieldNames)
        {
            result = Regex.Replace(
                result,
                $@"(""{field}""\s*:\s*)""[^""]*""",
                "$1\"***masked***\"",
                RegexOptions.IgnoreCase);
        }

        return result;
    }

    /// <summary>
    /// Checks if a field name is sensitive.
    /// </summary>
    public static bool IsSensitiveField(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName))
            return false;

        var lowerFieldName = fieldName.ToLowerInvariant();
        return SensitiveFieldNames.Any(sensitive => lowerFieldName.Contains(sensitive));
    }
}
