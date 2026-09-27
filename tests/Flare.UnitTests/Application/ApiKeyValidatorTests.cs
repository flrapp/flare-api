using Flare.Application.Services;
using Flare.Domain.Entities;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using NSubstitute;

namespace Flare.UnitTests.Application;

public class ApiKeyValidatorTests
{
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly ApiKeyValidator _sut;

    public ApiKeyValidatorTests()
    {
        _sut = new ApiKeyValidator(_projects);
        _projects.GetByAliasAsync("proj").Returns(new Project
        {
            Alias = "proj",
            ApiKey = "key-1",
            Scopes = [new Scope { Alias = "dev" }]
        });
    }

    [Theory]
    [InlineData("proj", "key-1", "dev", true)]
    [InlineData("proj", "wrong", "dev", false)]
    [InlineData("proj", "key-1", "prod", false)]
    [InlineData("missing", "key-1", "dev", false)]
    public async Task ValidateApiKey_requires_project_key_and_scope_match(string alias, string key, string scope, bool expected)
    {
        Assert.Equal(expected, await _sut.ValidateApiKeyAsync(alias, key, scope));
    }

    [Fact]
    public async Task ValidateApiKeyAndGetProject_returns_repository_result()
    {
        var project = new Project { Id = Guid.NewGuid() };
        _projects.GetByApiKeyAsync("abc").Returns(project);

        Assert.Same(project, await _sut.ValidateApiKeyAndGetProjectAsync("abc"));
        Assert.Null(await _sut.ValidateApiKeyAndGetProjectAsync("other"));
    }
}
