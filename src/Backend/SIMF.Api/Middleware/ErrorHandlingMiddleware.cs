using System.Text.Json;
using SIMF.Common;

namespace SIMF.Api.Middleware;

/// <summary>
/// The first middleware in the pipeline (SIMF-Sprint1 plan section 7). It wraps
/// every request: an <see cref="ApiException"/> becomes its declared
/// <see cref="ApiResult{T}"/> failure with the matching HTTP status; any other
/// exception becomes a 500. Every error carries both English and Arabic
/// messages (D-030).
/// </summary>
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
        catch (ApiException ex)
        {
            logger.LogWarning(
                "Request failed for {Method} {Path}: {Code} ({Status})",
                context.Request.Method,
                context.Request.Path,
                ex.Code,
                ex.StatusCode);

            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteAsync(context, ex.StatusCode, new ApiError
            {
                Code = ex.Code,
                Message = ex.Message,
                MessageArabic = ex.MessageArabic,
                Details = ex.Details,
            });
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

            await WriteAsync(context, StatusCodes.Status500InternalServerError, new ApiError
            {
                Code = ErrorCodes.InternalError,
                Message = "An unexpected error occurred.",
                MessageArabic = "حدث خطأ غير متوقع.",
            });
        }
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, ApiError error)
    {
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        // A5-12 (NCA App-Sec Standard) — declare the charset explicitly.
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(ApiResult<object>.Fail(error), JsonOptions));
    }
}
