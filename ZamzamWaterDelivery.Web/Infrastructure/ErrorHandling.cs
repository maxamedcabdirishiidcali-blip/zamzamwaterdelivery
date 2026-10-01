using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using ZamzamWaterDelivery.Application.Common;

namespace ZamzamWaterDelivery.Web.Infrastructure;

/// <summary>Turns exceptions from /api requests into ApiResponse JSON with the right status code.</summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, message, errors) = Map(exception);
        if (status >= 500)
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning("Request failed with {Status}: {Message}", status, exception.Message);

        // The middleware has already rewritten Request.Path to the error page, so use the original path.
        // Non-API requests fall through to the /Home/Error page.
        var originalPath = context.Features.Get<IExceptionHandlerPathFeature>()?.Path ?? context.Request.Path.Value;
        if (originalPath is null || !originalPath.StartsWith("/api", StringComparison.OrdinalIgnoreCase)) return false;

        if (status >= 500 && _environment.IsDevelopment()) errors = new[] { exception.Message };
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(message, errors), ct);
        return true;
    }

    public static (int Status, string Message, IEnumerable<string>? Errors) Map(Exception exception) => exception switch
    {
        NotFoundException e => (StatusCodes.Status404NotFound, e.Message, null),
        BusinessRuleException e => (StatusCodes.Status400BadRequest, e.Message, e.Errors),
        AuthenticationFailedException e => (StatusCodes.Status401Unauthorized, e.Message, null),
        ForbiddenAccessException e => (StatusCodes.Status403Forbidden, e.Message, null),
        _ => (StatusCodes.Status500InternalServerError, "Something went wrong. Please try again later.", null)
    };
}

/// <summary>
/// For MVC pages: shows expected application errors as a friendly alert on the previous page
/// (or JSON for AJAX calls) instead of an error page.
/// </summary>
public class AppExceptionFilter : IExceptionFilter
{
    private readonly ITempDataDictionaryFactory _tempData;

    public AppExceptionFilter(ITempDataDictionaryFactory tempData) => _tempData = tempData;

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AppException exception) return;
        if (context.ActionDescriptor.EndpointMetadata.OfType<ApiControllerAttribute>().Any()) return;

        var request = context.HttpContext.Request;
        var (status, message, errors) = GlobalExceptionHandler.Map(exception);
        var details = errors?.ToList() ?? new List<string>();

        if (request.IsAjax())
        {
            context.Result = new JsonResult(new { success = false, message, errors = details }) { StatusCode = status };
        }
        else if (exception is NotFoundException)
        {
            context.Result = new ViewResult { ViewName = "~/Views/Shared/NotFound.cshtml", StatusCode = StatusCodes.Status404NotFound };
        }
        else
        {
            var tempData = _tempData.GetTempData(context.HttpContext);
            tempData[Alerts.Error] = details.Count > 0 ? $"{message} {string.Join(" ", details)}" : message;
            context.Result = new RedirectResult(SafeReturnUrl(request));
        }
        context.ExceptionHandled = true;
    }

    private static string SafeReturnUrl(HttpRequest request)
    {
        var referer = request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase))
            return uri.PathAndQuery;
        return "/";
    }
}

public static class HttpRequestExtensions
{
    public static bool IsAjax(this HttpRequest request) =>
        request.Headers.XRequestedWith == "XMLHttpRequest" ||
        request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
}

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
        return _next(context);
    }
}
