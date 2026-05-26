using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Tyresoles.Data.Features.WindowsServices;
using Tyresoles.Web;

namespace Tyresoles.Web.Features.WindowsServices;

public static class WindowsServiceEndpoints
{
    public static IEndpointRouteBuilder MapWindowsServiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/windows-services")
            .RequireAuthorization()
            .WithTags("WindowsServices");

        group.MapGet("/", async (
                HttpContext http,
                [FromServices] IWindowsServiceManager manager,
                [FromServices] ILoggerFactory loggerFactory,
                CancellationToken ct) =>
            {
                if (!AdminAuthorization.IsAdministrator(http.User))
                    return Results.Forbid();

                try
                {
                    var statuses = await manager.GetAllStatusesAsync(ct).ConfigureAwait(false);
                    LogAudit(loggerFactory, http.User, "list", null);
                    return Results.Ok(statuses);
                }
                catch (WindowsServiceException ex)
                {
                    return ToErrorResult(ex);
                }
            })
            .WithName("GetWindowsServices");

        group.MapGet("/{name}", async (
                string name,
                HttpContext http,
                [FromServices] IWindowsServiceManager manager,
                [FromServices] ILoggerFactory loggerFactory,
                CancellationToken ct) =>
            {
                if (!AdminAuthorization.IsAdministrator(http.User))
                    return Results.Forbid();

                try
                {
                    var status = await manager.GetStatusAsync(name, ct).ConfigureAwait(false);
                    LogAudit(loggerFactory, http.User, "status", name);
                    return Results.Ok(status);
                }
                catch (WindowsServiceException ex)
                {
                    return ToErrorResult(ex);
                }
            })
            .WithName("GetWindowsServiceStatus");

        group.MapPost("/{name}/start", (string name, HttpContext http, IWindowsServiceManager manager, ILoggerFactory loggerFactory, CancellationToken ct) =>
                ExecuteActionAsync(http, manager, loggerFactory, name, "start",
                    (m, n, token) => m.StartAsync(n, token), ct))
            .WithName("StartWindowsService");

        group.MapPost("/{name}/stop", (string name, HttpContext http, IWindowsServiceManager manager, ILoggerFactory loggerFactory, CancellationToken ct) =>
                ExecuteActionAsync(http, manager, loggerFactory, name, "stop",
                    (m, n, token) => m.StopAsync(n, token), ct))
            .WithName("StopWindowsService");

        group.MapPost("/{name}/restart", (string name, HttpContext http, IWindowsServiceManager manager, ILoggerFactory loggerFactory, CancellationToken ct) =>
                ExecuteActionAsync(http, manager, loggerFactory, name, "restart",
                    (m, n, token) => m.RestartAsync(n, token), ct))
            .WithName("RestartWindowsService");

        return app;
    }

    private static async Task<IResult> ExecuteActionAsync(
        HttpContext http,
        IWindowsServiceManager manager,
        ILoggerFactory loggerFactory,
        string serviceName,
        string action,
        Func<IWindowsServiceManager, string, CancellationToken, Task<WindowsServiceStatusDto>> execute,
        CancellationToken cancellationToken)
    {
        if (!AdminAuthorization.IsAdministrator(http.User))
            return Results.Forbid();

        try
        {
            var status = await execute(manager, serviceName, cancellationToken).ConfigureAwait(false);
            LogAudit(loggerFactory, http.User, action, serviceName);
            return Results.Ok(status);
        }
        catch (WindowsServiceException ex)
        {
            return ToErrorResult(ex);
        }
    }

    private static void LogAudit(ILoggerFactory loggerFactory, ClaimsPrincipal user, string action, string? serviceName)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? user.FindFirstValue("sub")
                     ?? "unknown";
        var logger = loggerFactory.CreateLogger("Tyresoles.WindowsServices");
        logger.LogInformation(
            "Windows service {Action} by admin {UserId} for service {ServiceName}",
            action,
            userId,
            serviceName ?? "(all)");
    }

    private static IResult ToErrorResult(WindowsServiceException ex) => ex switch
    {
        WindowsServiceNotSupportedException =>
            Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status501NotImplemented),
        WindowsServiceFeatureDisabledException =>
            Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable),
        WindowsServiceNotAllowedException =>
            Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden),
        WindowsServiceOperationException =>
            Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status409Conflict),
        _ =>
            Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status500InternalServerError)
    };
}
