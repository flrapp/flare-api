using Microsoft.Extensions.Logging;

namespace Flare.Application.Audit;

/// <summary>
/// Serilog-backed implementation of <see cref="IAuditLogger"/>.
///
/// Two named loggers are created via <see cref="ILoggerFactory"/> so that Serilog
/// routes entries under the correct SourceContext, enabling independent sink routing,
/// Seq filters, and Loki label selectors per audit domain:
///   - "Flare.Audit.Project" — project-scoped actions
///   - "Flare.Audit.User"    — system-level user actions
///
/// All audit fields are embedded directly in the structured message template as named
/// holes. Serilog captures each hole as a first-class log record property, making every
/// field indexable in Seq and exportable as an OTel log record attribute for Loki and
/// other backends — without any ambient LogContext state that could pollute unrelated
/// log entries.
///
/// Change-event overloads use the {@Identifier} destructuring hint so OldValue and
/// NewValue are stored as structured objects rather than opaque strings, preserving
/// sub-field queryability in downstream log stores.
/// </summary>
public sealed class SerilogAuditLogger : IAuditLogger
{
    // Category names become the Serilog SourceContext for each audit domain.
    private const string ProjectContext = "Flare.Audit.Project";
    private const string UserContext = "Flare.Audit.User";

    private readonly ILogger _projectLogger;
    private readonly ILogger _userLogger;

    public SerilogAuditLogger(ILoggerFactory loggerFactory)
    {
        _projectLogger = loggerFactory.CreateLogger(ProjectContext);
        _userLogger = loggerFactory.CreateLogger(UserContext);
    }

    /// <inheritdoc/>
    public void LogProjectAudit(
        string projectAlias,
        string username,
        string entityType,
        string? scope,
        string action)
    {
        _projectLogger.LogInformation(
            "Audit: {Action} on {EntityType} by {Username} [ProjectAlias={ProjectAlias} Scope={Scope}]",
            action, entityType, username, projectAlias, scope);
    }

    /// <inheritdoc/>
    public void LogProjectAudit(
        string projectAlias,
        string username,
        string entityType,
        string? scope,
        string action,
        object oldValue,
        object newValue)
    {
        _projectLogger.LogInformation(
            "Audit: {Action} on {EntityType} by {Username} [ProjectAlias={ProjectAlias} Scope={Scope} OldValue={@OldValue} NewValue={@NewValue}]",
            action, entityType, username, projectAlias, scope, oldValue, newValue);
    }

    /// <inheritdoc/>
    public void LogUserAudit(
        string subjectUsername,
        string actorUsername,
        string entityType,
        string? scope,
        string action)
    {
        _userLogger.LogInformation(
            "Audit: {Action} on {EntityType} [{SubjectUsername}] by {ActorUsername} [Scope={Scope}]",
            action, entityType, subjectUsername, actorUsername, scope);
    }

    /// <inheritdoc/>
    public void LogUserAudit(
        string subjectUsername,
        string actorUsername,
        string entityType,
        string? scope,
        string action,
        object oldValue,
        object newValue)
    {
        _userLogger.LogInformation(
            "Audit: {Action} on {EntityType} [{SubjectUsername}] by {ActorUsername} [Scope={Scope} OldValue={@OldValue} NewValue={@NewValue}]",
            action, entityType, subjectUsername, actorUsername, scope, oldValue, newValue);
    }
}
