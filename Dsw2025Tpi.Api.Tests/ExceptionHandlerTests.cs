using System.Text.Json;
using Dsw2025Tpi.Api.Middlewares;
using Dsw2025Tpi.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dsw2025Tpi.Api.Tests;

public class ExceptionHandlerTests
{
    private static async Task<(int Status, JsonElement Body)> Handle(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new ExceptionHandler(_ => throw exception, NullLogger<ExceptionHandler>.Instance);

        await handler.Invoke(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.StartsWith("application/json", context.Response.ContentType);
        return (context.Response.StatusCode, document.RootElement.Clone());
    }

    [Fact]
    public async Task UnexpectedExceptionReturnsGenericInternalErrorWithoutDetails()
    {
        var (status, body) = await Handle(new InvalidOperationException("secret connection detail"));

        Assert.Equal(500, status);
        Assert.Equal(500, body.GetProperty("statusCode").GetInt32());
        Assert.Equal("Ocurrió un error interno en el servidor.", body.GetProperty("message").GetString());
        Assert.DoesNotContain("secret", body.GetRawText());
    }

    [Theory]
    [InlineData(typeof(EntityNotFoundException), 404)]
    [InlineData(typeof(DuplicatedEntityException), 409)]
    [InlineData(typeof(Dsw2025Tpi.Application.Exceptions.ApplicationException), 400)]
    public async Task ApplicationExceptionsMapToClientErrors(Type type, int expected)
    {
        var (status, body) = await Handle((Exception)Activator.CreateInstance(type, "mensaje claro")!);

        Assert.Equal(expected, status);
        Assert.Equal("mensaje claro", body.GetProperty("message").GetString());
        Assert.False(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task ValidationExceptionListsEveryError()
    {
        var (status, body) = await Handle(new EntityValidationException(new[] { "Error uno.", "Error dos." }));

        Assert.Equal(400, status);
        Assert.Equal("Error uno. Error dos.", body.GetProperty("message").GetString());
        Assert.Equal(new[] { "Error uno.", "Error dos." },
            body.GetProperty("errors").EnumerateArray().Select(e => e.GetString()));
    }
}
