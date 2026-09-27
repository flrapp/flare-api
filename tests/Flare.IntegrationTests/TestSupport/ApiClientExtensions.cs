using System.Net;
using System.Net.Http.Json;
using Flare.Application.DTOs;

namespace Flare.IntegrationTests.TestSupport;

internal static class ApiClientExtensions
{
    public static async Task<HttpClient> LoginAsync(this FlareApiFactory factory, string username, string password)
    {
        var client = factory.CreateHttpsClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginDto { Username = username, Password = password });
        await response.EnsureStatusAsync(HttpStatusCode.OK);
        return client;
    }

    public static Task<HttpClient> LoginAsAdminAsync(this FlareApiFactory factory) =>
        factory.LoginAsync(FlareApiFactory.AdminUsername, FlareApiFactory.AdminPassword);

    /// Creates a regular user through the admin API and returns a client logged in as that user.
    public static async Task<(HttpClient Client, UserResponseDto User)> CreateUserAndLoginAsync(
        this FlareApiFactory factory, HttpClient admin, string username)
    {
        const string password = "UserPass123";
        var response = await admin.PostAsJsonAsync("/api/v1/users",
            new CreateUserDto { Username = username, FullName = $"{username} Test", TemporaryPassword = password });
        await response.EnsureStatusAsync(HttpStatusCode.Created);
        var user = (await response.Content.ReadFromJsonAsync<UserResponseDto>())!;
        return (await factory.LoginAsync(username, password), user);
    }

    public static async Task EnsureStatusAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected {(int)expected} {expected} but got {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
    }

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        await response.EnsureStatusAsync(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
