namespace Flare.Application.Audit;

/// <summary>
/// Abstraction for writing structured audit log entries for significant user actions.
/// Each method call is self-contained: all required fields are passed as explicit
/// parameters so no field can be accidentally missing or leak from ambient log context
/// into unrelated application log entries.
///
/// Two source contexts are used for independent filtering and routing in Seq, Loki,
/// and other OpenTelemetry-compatible backends:
///   - Flare.Audit.Project — project-scoped actions (flags, scopes, permissions)
///   - Flare.Audit.User   — system-level user actions (created, deleted, role changed)
///
/// Change events have dedicated overloads that require OldValue and NewValue so the
/// compiler enforces their presence for mutating operations.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Logs a project-scoped audit event (no state change).
    /// Writes using the "Flare.Audit.Project" source context.
    /// </summary>
    void LogProjectAudit(
        string projectAlias,
        string username,
        string entityType,
        string? scope,
        string action);

    /// <summary>
    /// Logs a project-scoped change event with before/after state.
    /// OldValue and NewValue are serialised as structured objects to preserve
    /// field-level queryability in Seq and Loki.
    /// Writes using the "Flare.Audit.Project" source context.
    /// </summary>
    void LogProjectAudit(
        string projectAlias,
        string username,
        string entityType,
        string? scope,
        string action,
        object oldValue,
        object newValue);

    /// <summary>
    /// Logs a system-level user audit event (no state change).
    /// Writes using the "Flare.Audit.User" source context.
    /// </summary>
    void LogUserAudit(
        string username,
        string entityType,
        string? scope,
        string action);

    /// <summary>
    /// Logs a system-level user change event with before/after state.
    /// OldValue and NewValue are serialised as structured objects to preserve
    /// field-level queryability in Seq and Loki.
    /// Writes using the "Flare.Audit.User" source context.
    /// </summary>
    void LogUserAudit(
        string username,
        string entityType,
        string? scope,
        string action,
        object oldValue,
        object newValue);
}
