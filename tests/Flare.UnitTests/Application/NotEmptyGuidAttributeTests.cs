using System.ComponentModel.DataAnnotations;
using Flare.Application.DTOs;
using Flare.Application.Validation;

namespace Flare.UnitTests.Application;

public class NotEmptyGuidAttributeTests
{
    [Fact]
    public void Accepts_non_empty_guid()
    {
        Assert.True(new NotEmptyGuidAttribute().IsValid(Guid.NewGuid()));
    }

    [Fact]
    public void Rejects_empty_guid_null_and_other_types()
    {
        var attribute = new NotEmptyGuidAttribute();

        Assert.False(attribute.IsValid(Guid.Empty));
        Assert.False(attribute.IsValid(null));
        Assert.False(attribute.IsValid("not-a-guid"));
    }

    public static TheoryData<object, string> DtosWithEmptyIds => new()
    {
        { new UpdateFeatureFlagValueDto(), nameof(UpdateFeatureFlagValueDto.ScopeId) },
        { new InviteUserDto(), nameof(InviteUserDto.UserId) },
        { new AssignScopePermissionsDto(), nameof(AssignScopePermissionsDto.ScopeId) }
    };

    [Theory]
    [MemberData(nameof(DtosWithEmptyIds))]
    public void Dto_with_empty_id_fails_validation(object dto, string member)
    {
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        Assert.False(valid);
        var error = Assert.Single(results);
        Assert.Contains(member, error.MemberNames);
    }
}
