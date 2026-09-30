using System.Text.Json;
using Flare.Api.Middleware;
using Flare.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flare.UnitTests.Api;

public class GlobalExceptionHandlerTests
{
    private static async Task<(int Status, JsonElement Body)> Handle(Exception exception)
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/things";
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, doc.RootElement.Clone());
    }

    public static TheoryData<Exception, int, string> Mappings => new()
    {
        { new BadRequestException("bad"), 400, "Bad Request" },
        { new NotFoundException("missing"), 404, "Not Found" },
        { new UnauthorizedException("who"), 401, "Unauthorized" },
        { new ForbiddenException("no"), 403, "Forbidden" },
        { new ConflictException("taken"), 409, "Conflict" },
        { new UnauthorizedAccessException("key"), 401, "Unauthorized" },
        { new InvalidOperationException("boom"), 500, "Internal Server Error" }
    };

    [Theory]
    [MemberData(nameof(Mappings))]
    public async Task Maps_exception_to_problem_details(Exception exception, int status, string title)
    {
        var (actualStatus, body) = await Handle(exception);

        Assert.Equal(status, actualStatus);
        Assert.Equal(status, body.GetProperty("status").GetInt32());
        Assert.Equal(title, body.GetProperty("title").GetString());
        Assert.Equal(exception.Message, body.GetProperty("detail").GetString());
        Assert.Equal("/api/v1/things", body.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task Validation_exception_includes_errors()
    {
        var (status, body) = await Handle(new ValidationException(new Dictionary<string, string[]> { ["name"] = ["required"] }));

        Assert.Equal(400, status);
        Assert.Equal("Validation Error", body.GetProperty("title").GetString());
        Assert.Equal("required", body.GetProperty("errors").GetProperty("name")[0].GetString());
    }

    [Fact]
    public async Task Temporary_lock_includes_remaining_minutes()
    {
        var (status, body) = await Handle(new AccountLockedException(isPermanent: false, remainingMinutes: 5));

        Assert.Equal(401, status);
        Assert.Equal("Account Locked", body.GetProperty("title").GetString());
        Assert.False(body.GetProperty("isPermanent").GetBoolean());
        Assert.Equal(5, body.GetProperty("remainingMinutes").GetInt32());
    }

    [Fact]
    public async Task Permanent_lock_omits_remaining_minutes()
    {
        var (_, body) = await Handle(new AccountLockedException(isPermanent: true));

        Assert.True(body.GetProperty("isPermanent").GetBoolean());
        Assert.False(body.TryGetProperty("remainingMinutes", out _));
    }
}
