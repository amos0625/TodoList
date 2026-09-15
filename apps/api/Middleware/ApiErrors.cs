using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Todo.Api.Contracts;

namespace Todo.Api.Middleware;

public class ApiErrors(RequestDelegate next, ILogger<ApiErrors> logger)
{
    public static object Envelope(HttpContext context, ApiException error) => new
    {
        error = new { code = error.Code, message = error.Message, details = error.Details, requestId = context.TraceIdentifier }
    };

    public async Task InvokeAsync(HttpContext context)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            if (context.Request.Path.StartsWithSegments("/api/v1") &&
                !(HttpMethods.IsGet(context.Request.Method) && context.Request.Path.Value?.TrimEnd('/') == "/api/v1/todos") &&
                context.Request.Query.Count > 0)
                throw ApiException.Invalid("query", "此端點不接受查詢參數");
            if (context.Request.Method is "POST" or "PATCH" && context.Request.Path.StartsWithSegments("/api"))
            {
                if (!string.Equals(context.Request.ContentType?.Split(';')[0].Trim(), "application/json", StringComparison.OrdinalIgnoreCase))
                    throw new ApiException(415, "UNSUPPORTED_MEDIA_TYPE", "請使用 application/json");
                // Bound both chunked and Content-Length requests before MVC reads the body.
                context.Request.EnableBuffering();
                var buffer = new byte[4096];
                var size = 0;
                int count;
                while ((count = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
                {
                    size += count;
                    if (size > 32768) throw new ApiException(413, "PAYLOAD_TOO_LARGE", "內容超過 32 KB");
                }
                context.Request.Body.Position = 0;
            }
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception ex)
        {
            var error = ex switch
            {
                ApiException known => known,
                JsonException => new ApiException(400, "INVALID_JSON", "JSON 格式錯誤"),
                BadHttpRequestException bad when bad.StatusCode == 413 => new ApiException(413, "PAYLOAD_TOO_LARGE", "內容超過 32 KB"),
                SqliteException or DbUpdateException => new ApiException(503, "SERVICE_UNAVAILABLE", "資料庫暫時無法使用，請稍後重試"),
                _ => new ApiException(500, "INTERNAL_ERROR", "服務發生錯誤，請稍後重試")
            };
            if (error.Status >= 500) logger.LogError("Request {RequestId} failed: {ExceptionType}", context.TraceIdentifier, ex.GetType().Name);
            context.Response.StatusCode = error.Status;
            await context.Response.WriteAsJsonAsync(Envelope(context, error));
        }
        finally
        {
            logger.LogInformation("{RequestId} {Method} {Path} {Status} {ElapsedMs}ms", context.TraceIdentifier, context.Request.Method, context.Request.Path, context.Response.StatusCode, watch.ElapsedMilliseconds);
        }
    }
}
