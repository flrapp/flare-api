using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Flare.UnitTests.TestSupport;

internal static class Caches
{
    /// Real in-memory HybridCache, for tests that exercise GetOrCreateAsync.
    public static HybridCache CreateReal()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
}
