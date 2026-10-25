using Dsw2025Tpi.Application.Exceptions;
using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dsw2025Tpi.Api.Middlewares
{
    public class ExceptionHandler
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandler> _logger;

        public ExceptionHandler(RequestDelegate next, ILogger<ExceptionHandler> logger)
        {
            _next = next;
            _logger = logger;
        }

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception e)
            {
                // Once the body has started, the status code can no longer be changed.
                if (context.Response.HasStarted) throw;
                await HandleExceptionAsync(context, e);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception e)
        {
            // Order matters: specific application exceptions before their ApplicationException base.
            var (statusCode, message) = e switch
            {
                EntityNotFoundException => (HttpStatusCode.NotFound, e.Message),
                DuplicatedEntityException => (HttpStatusCode.Conflict, e.Message),
                Dsw2025Tpi.Application.Exceptions.ApplicationException => (HttpStatusCode.BadRequest, e.Message),
                InvalidCredentialsException => (HttpStatusCode.BadRequest, "Invalid username or password."),
                InvalidCredentialException => (HttpStatusCode.Unauthorized, "Unauthorized access."),
                // Unexpected errors never expose internal details to the client.
                _ => (HttpStatusCode.InternalServerError, "Ocurrió un error interno en el servidor.")
            };

            if (statusCode == HttpStatusCode.InternalServerError)
                _logger.LogError(e, "Unhandled exception: {ExceptionType}", e.GetType().Name);
            else
                _logger.LogWarning("Request rejected with {StatusCode}: {ExceptionType}", (int)statusCode, e.GetType().Name);

            var response = new ErrorResponse(
                (int)statusCode,
                message,
                (e as EntityValidationException)?.Errors);

            context.Response.Clear();
            context.Response.StatusCode = (int)statusCode;
            await context.Response.WriteAsJsonAsync(response, JsonOptions);
        }

        private sealed record ErrorResponse(int StatusCode, string Message, IReadOnlyList<string>? Errors);
    }
}