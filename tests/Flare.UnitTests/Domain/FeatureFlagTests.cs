using Flare.Domain.Entities;
using Flare.Domain.Enums;

namespace Flare.UnitTests.Domain;

public class FeatureFlagTests
{
    [Theory]
    [InlineData(FeatureFlagType.Boolean)]
    [InlineData(FeatureFlagType.String)]
    [InlineData(FeatureFlagType.Number)]
    [InlineData(FeatureFlagType.Json)]
    public void CreateValueForScope_creates_empty_value_linked_to_flag_and_scope(FeatureFlagType type)
    {
        var flag = new FeatureFlag { Id = Guid.NewGuid(), Type = type };
        var scopeId = Guid.NewGuid();

        var value = flag.CreateValueForScope(scopeId);

        Assert.Equal(flag.Id, value.FeatureFlagId);
        Assert.Equal(scopeId, value.ScopeId);
        Assert.False(value.IsEnabled);
        Assert.Null(value.DefaultStringValue);
        Assert.Null(value.DefaultNumberValue);
        Assert.Null(value.DefaultJsonValue);
    }

    [Fact]
    public void CreateValueForScope_throws_for_unknown_type()
    {
        var flag = new FeatureFlag { Id = Guid.NewGuid(), Type = (FeatureFlagType)42 };

        Assert.Throws<ArgumentOutOfRangeException>(() => flag.CreateValueForScope(Guid.NewGuid()));
    }
}
