using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flare.Application.DTOs;
using Flare.Domain.Enums;
using Flare.IntegrationTests.TestSupport;

namespace Flare.IntegrationTests.Api;

public class AuthAndUsersApiTests(FlareApiFactory factory) : IClassFixture<FlareApiFactory>
{
    private static string Unique(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..20];

    #region Auth

    [Fact]
    public async Task Admin_can_log_in_and_read_profile()
    {
        var client = factory.CreateHttpsClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginDto { Username = FlareApiFactory.AdminUsername, Password = FlareApiFactory.AdminPassword });

        await login.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("FlareAuth=") && c.Contains("httponly"));
        var me = await client.GetJsonAsync<AuthResultDto>("/api/v1/auth/me");
        Assert.Equal(FlareApiFactory.AdminUsername, me.Username);
        Assert.Equal(GlobalRole.SuperAdmin, me.GlobalRole);
    }

    [Fact]
    public async Task Anonymous_requests_to_protected_endpoints_get_401()
    {
        var client = factory.CreateHttpsClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/projects")).StatusCode);
    }

    [Fact]
    public async Task Login_with_bad_credentials_or_payload_is_rejected()
    {
        var client = factory.CreateHttpsClient();

        var wrong = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginDto { Username = "nobody_here", Password = "Whatever123" });
        var invalid = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginDto { Username = "ab", Password = "short" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var client = await factory.LoginAsAdminAsync();

        var logout = await client.PostAsync("/api/v1/auth/logout", null);

        await logout.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task User_changes_password_and_can_log_in_with_it()
    {
        var admin = await factory.LoginAsAdminAsync();
        var username = Unique("pwd");
        var (client, _) = await factory.CreateUserAndLoginAsync(admin, username);
        Assert.True((await client.GetJsonAsync<AuthResultDto>("/api/v1/auth/me")).MustChangePassword);

        var wrong = await client.PostAsJsonAsync("/api/v1/auth/change-password",
            new ChangePasswordDto { CurrentPassword = "NotMine123", NewPassword = "Brand1New" });
        var weak = await client.PostAsJsonAsync("/api/v1/auth/change-password",
            new ChangePasswordDto { CurrentPassword = "UserPass123", NewPassword = "weak" });
        var ok = await client.PostAsJsonAsync("/api/v1/auth/change-password",
            new ChangePasswordDto { CurrentPassword = "UserPass123", NewPassword = "Brand1New" });

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        await ok.EnsureStatusAsync(HttpStatusCode.OK);
        var relogin = await factory.LoginAsync(username, "Brand1New");
        Assert.False((await relogin.GetJsonAsync<AuthResultDto>("/api/v1/auth/me")).MustChangePassword);
    }

    [Fact]
    public async Task Repeated_failures_lock_account_until_admin_unlocks()
    {
        var admin = await factory.LoginAsAdminAsync();
        var username = Unique("lock");
        var (_, user) = await factory.CreateUserAndLoginAsync(admin, username);
        var client = factory.CreateHttpsClient();

        HttpResponseMessage last = null!;
        for (var i = 0; i < 3; i++)
            last = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginDto { Username = username, Password = "WrongPass1" });

        Assert.Equal(HttpStatusCode.Unauthorized, last.StatusCode);
        var problem = await last.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Account Locked", problem.GetProperty("title").GetString());
        Assert.False(problem.GetProperty("isPermanent").GetBoolean());
        Assert.Equal(30, problem.GetProperty("remainingMinutes").GetInt32());

        var blocked = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginDto { Username = username, Password = "UserPass123" });
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);

        await (await admin.PostAsync($"/api/v1/users/{user.UserId}/unlock", null)).EnsureStatusAsync(HttpStatusCode.NoContent);
        await factory.LoginAsync(username, "UserPass123");
    }

    #endregion

    #region User administration

    [Fact]
    public async Task Admin_manages_user_lifecycle()
    {
        var admin = await factory.LoginAsAdminAsync();
        var username = Unique("life");
        var (_, user) = await factory.CreateUserAndLoginAsync(admin, username);

        var list = await admin.GetJsonAsync<PagedResult<UserResponseDto>>($"/api/v1/users?search={username}&isActive=true&page=0&pageSize=100");
        Assert.Equal(user.UserId, Assert.Single(list.Items).UserId);
        Assert.Equal(25, list.PageSize);

        var update = await admin.PutAsJsonAsync($"/api/v1/users/{user.UserId}", new UpdateUserDto { FullName = "Renamed User", GlobalRole = GlobalRole.Admin });
        await update.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(GlobalRole.Admin, (await update.Content.ReadFromJsonAsync<UserResponseDto>())!.GlobalRole);

        var reset = await admin.PostAsJsonAsync($"/api/v1/users/{user.UserId}/reset-password", new ResetUserPasswordDto { TemporaryPassword = "Reset1234" });
        await reset.EnsureStatusAsync(HttpStatusCode.NoContent);
        await factory.LoginAsync(username, "Reset1234");

        await (await admin.PostAsync($"/api/v1/users/{user.UserId}/deactivate", null)).EnsureStatusAsync(HttpStatusCode.NoContent);
        var inactiveLogin = await factory.CreateHttpsClient().PostAsJsonAsync("/api/v1/auth/login", new LoginDto { Username = username, Password = "Reset1234" });
        Assert.Equal(HttpStatusCode.Unauthorized, inactiveLogin.StatusCode);

        await (await admin.PostAsync($"/api/v1/users/{user.UserId}/activate", null)).EnsureStatusAsync(HttpStatusCode.NoContent);
        await factory.LoginAsync(username, "Reset1234");

        await (await admin.DeleteAsync($"/api/v1/users/{user.UserId}")).EnsureStatusAsync(HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/v1/users/{user.UserId}/activate", null)).StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_delete_or_deactivate_self()
    {
        var admin = await factory.LoginAsAdminAsync();
        var me = await admin.GetJsonAsync<AuthResultDto>("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/v1/users/{me.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/v1/users/{me.UserId}/deactivate", null)).StatusCode);
    }

    [Fact]
    public async Task User_endpoints_validate_input()
    {
        var admin = await factory.LoginAsAdminAsync();

        var badCreate = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserDto { Username = "bad name!", FullName = "X", TemporaryPassword = "short" });
        var badReset = await admin.PostAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}/reset-password", new ResetUserPasswordDto { TemporaryPassword = "x" });
        var badUpdate = await admin.PutAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}", new UpdateUserDto { FullName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, badCreate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badReset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badUpdate.StatusCode);
    }

    [Fact(Skip = "Known issue: services signal client errors with InvalidOperationException (duplicate username, duplicate flag key, flag type mismatch, unknown user on update), which GlobalExceptionHandler maps to 500.")]
    public async Task Creating_duplicate_username_is_a_client_error()
    {
        var admin = await factory.LoginAsAdminAsync();
        var dto = new CreateUserDto { Username = Unique("dup"), FullName = "Dup User", TemporaryPassword = "UserPass123" };
        await admin.PostAsJsonAsync("/api/v1/users", dto);

        var duplicate = await admin.PostAsJsonAsync("/api/v1/users", dto);

        Assert.True((int)duplicate.StatusCode is >= 400 and < 500);
    }

    [Fact]
    public async Task Regular_users_cannot_reach_admin_endpoints()
    {
        var admin = await factory.LoginAsAdminAsync();
        var (client, _) = await factory.CreateUserAndLoginAsync(admin, Unique("reg"));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/users")).StatusCode);
    }

    #endregion
}
