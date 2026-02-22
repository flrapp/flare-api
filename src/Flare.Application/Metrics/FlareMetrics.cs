using System.Diagnostics.Metrics;

namespace Flare.Application.Metrics;

/// <summary>
/// Centralized metrics facade for the Flare API, backed by System.Diagnostics.Metrics.
///
/// Registered as a singleton so a single <see cref="Meter"/> instance is shared across
/// all requests, avoiding repeated meter creation overhead.
///
/// The meter is named "Flare.Api" so it can be targeted by OpenTelemetry metric pipelines
/// and Prometheus exporters independently of application logs. Consumers add the meter name
/// to their <c>WithMetrics(b => b.AddMeter("Flare.Api"))</c> configuration.
///
/// All dimensions are recorded as metric tags, enabling Grafana / Prometheus dashboards
/// to slice and group by project, feature flag key, and scope independently.
/// </summary>
public sealed class FlareMetrics
{
    private readonly Counter<long> _flagEvaluations;

    public FlareMetrics(IMeterFactory meterFactory)
    {
        // Create the meter once; IMeterFactory manages its lifetime as a singleton.
        var meter = meterFactory.Create("Flare.Api");

        _flagEvaluations = meter.CreateCounter<long>(
            "flare.flag.evaluations",
            description: "Total number of feature flag evaluation requests received by the SDK endpoints.");
    }

    /// <summary>
    /// Increments the evaluation counter for a single flag evaluation request.
    /// All three dimensions are included so dashboards can filter by any combination.
    /// </summary>
    /// <param name="projectAlias">The alias of the project owning the flag.</param>
    /// <param name="flagKey">The key of the evaluated feature flag.</param>
    /// <param name="scope">The scope (environment) alias used in the evaluation context.</param>
    public void RecordEvaluation(string projectAlias, string flagKey, string scope)
    {
        _flagEvaluations.Add(1,
            new KeyValuePair<string, object?>("project.alias", projectAlias),
            new KeyValuePair<string, object?>("flag.key", flagKey),
            new KeyValuePair<string, object?>("scope", scope));
    }

    /// <summary>
    /// Increments the evaluation counter for a bulk evaluation request.
    /// No <c>flag.key</c> tag is recorded because the request evaluates all flags at once;
    /// adding a per-flag tag would require one increment per flag and misrepresent request volume.
    /// </summary>
    /// <param name="projectAlias">The alias of the project owning the flags.</param>
    /// <param name="scope">The scope (environment) alias used in the evaluation context.</param>
    public void RecordBulkEvaluation(string projectAlias, string scope)
    {
        _flagEvaluations.Add(1,
            new KeyValuePair<string, object?>("project.alias", projectAlias),
            new KeyValuePair<string, object?>("scope", scope));
    }
}
