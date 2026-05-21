using System.Text.Json;
using SIMF.Common;

namespace SIMF.Api.Middleware;

/// <summary>
/// The first middleware in the pipeline (SIMF-Sprint1 plan section 7). It wraps
/// every request, catches any unhandled exception, logs it, and returns the
/// standard <see cref="ApiResult{T}"/> error envelope — so no exception ever
/// reaches the client as a raw stack trace.
/// </summary>
/// <remarks>
/// Mapping of specific exception types (DataValidationException, domain
/// exceptions) to their error codes is added with the features that introduce
/// those exceptions; this scaffold handles the catch-all case.
/// </remarks>
public sealed class ErrorHandlingMiddleware(
    RequestDelegate next,
    ILogger<ErrorHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unhandled exception for {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var result = ApiResult<object>.Fail(new ApiError
            {
                Code = ErrorCodes.InternalError,
                Message = "An unexpected error occurred.",
            });

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(result, JsonOptions));
        }
    }
}
