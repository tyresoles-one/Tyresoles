using System.Collections.Concurrent;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tyresoles.Data.Features.WindowsServices;

public sealed class WindowsServiceManager : IWindowsServiceManager
{
    private const int PollIntervalMs = 500;
    private const int MaxPollAttempts = 120; // 60 seconds total

    private readonly WindowsServiceOptions _options;
    private readonly ILogger<WindowsServiceManager> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _serviceLocks = new(StringComparer.OrdinalIgnoreCase);

    public WindowsServiceManager(
        IOptions<WindowsServiceOptions> options,
        ILogger<WindowsServiceManager> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<IReadOnlyList<WindowsServiceStatusDto>> GetAllStatusesAsync(CancellationToken cancellationToken = default)
    {
        EnsureAvailable();

        var results = new List<WindowsServiceStatusDto>(_options.Services.Count);
        foreach (var entry in _options.Services)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(QueryStatus(entry));
        }

        return Task.FromResult<IReadOnlyList<WindowsServiceStatusDto>>(results);
    }

    public Task<WindowsServiceStatusDto> GetStatusAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        cancellationToken.ThrowIfCancellationRequested();
        var entry = ResolveEntry(serviceName);
        return Task.FromResult(QueryStatus(entry));
    }

    public async Task<WindowsServiceStatusDto> StartAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        var entry = ResolveEntry(serviceName);
        if (!entry.CanStart)
            throw new WindowsServiceNotAllowedException($"Starting service '{entry.Name}' is not permitted.");

        var serviceLock = _serviceLocks.GetOrAdd(entry.Name, _ => new SemaphoreSlim(1, 1));
        await serviceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Starting Windows service {ServiceName}", entry.Name);

            await Task.Run(() =>
            {
                using var controller = CreateController(entry.Name);
                if (controller.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
                    return;

                try
                {
                    controller.Start();
                }
                catch (InvalidOperationException ex)
                {
                    controller.Refresh();
                    if (controller.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
                        return;

                    throw new WindowsServiceOperationException(
                        $"Failed to start service '{entry.Name}': {ex.Message}");
                }
            }, cancellationToken).ConfigureAwait(false);

            return await PollForStateAsync(entry, ServiceControllerStatus.Running, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            serviceLock.Release();
        }
    }

    public async Task<WindowsServiceStatusDto> StopAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        var entry = ResolveEntry(serviceName);
        if (!entry.CanStop)
            throw new WindowsServiceNotAllowedException($"Stopping service '{entry.Name}' is not permitted.");

        var serviceLock = _serviceLocks.GetOrAdd(entry.Name, _ => new SemaphoreSlim(1, 1));
        await serviceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Stopping Windows service {ServiceName}", entry.Name);

            await Task.Run(() =>
            {
                using var controller = CreateController(entry.Name);
                if (controller.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
                    return;

                try
                {
                    controller.Stop();
                }
                catch (InvalidOperationException ex)
                {
                    controller.Refresh();
                    if (controller.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
                        return;

                    throw new WindowsServiceOperationException(
                        $"Failed to stop service '{entry.Name}': {ex.Message}");
                }
            }, cancellationToken).ConfigureAwait(false);

            return await PollForStateAsync(entry, ServiceControllerStatus.Stopped, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            serviceLock.Release();
        }
    }

    public async Task<WindowsServiceStatusDto> RestartAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        var entry = ResolveEntry(serviceName);
        if (!entry.CanStart || !entry.CanStop)
            throw new WindowsServiceNotAllowedException($"Restarting service '{entry.Name}' is not permitted.");

        _logger.LogInformation("Restarting Windows service {ServiceName}", entry.Name);

        try
        {
            await StopAsync(serviceName, cancellationToken).ConfigureAwait(false);
        }
        catch (WindowsServiceOperationException)
        {
            // Service may already be stopped; continue to start.
        }

        return await StartAsync(serviceName, cancellationToken).ConfigureAwait(false);
    }

    private void EnsureAvailable()
    {
        if (!OperatingSystem.IsWindows())
            throw new WindowsServiceNotSupportedException();

        if (!_options.Enabled)
            throw new WindowsServiceFeatureDisabledException();
    }

    private WindowsServiceEntryOptions ResolveEntry(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new WindowsServiceNotAllowedException("Service name is required.");

        var entry = _options.Services.FirstOrDefault(s =>
            s.Name.Equals(serviceName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            throw new WindowsServiceNotAllowedException($"Service '{serviceName}' is not in the allowlist.");

        return entry;
    }

    private WindowsServiceStatusDto QueryStatus(WindowsServiceEntryOptions entry)
    {
        try
        {
            using var controller = CreateController(entry.Name);
            var state = MapStatus(controller.Status);
            return new WindowsServiceStatusDto
            {
                Name = entry.Name,
                DisplayName = string.IsNullOrWhiteSpace(controller.DisplayName)
                    ? entry.Name
                    : controller.DisplayName,
                State = state,
                IsRunning = controller.Status == ServiceControllerStatus.Running,
                CanStart = entry.CanStart,
                CanStop = entry.CanStop
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "Could not query Windows service {ServiceName}", entry.Name);
            return new WindowsServiceStatusDto
            {
                Name = entry.Name,
                DisplayName = entry.Name,
                State = "Unknown",
                IsRunning = false,
                CanStart = entry.CanStart,
                CanStop = entry.CanStop
            };
        }
    }

    private async Task<WindowsServiceStatusDto> PollForStateAsync(
        WindowsServiceEntryOptions entry,
        ServiceControllerStatus wantedStatus,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxPollAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(PollIntervalMs, cancellationToken).ConfigureAwait(false);

            var status = QueryStatus(entry);
            if (IsWantedState(status.State, wantedStatus))
                return status;
        }

        return QueryStatus(entry);
    }

    private static ServiceController CreateController(string serviceName) =>
        new(serviceName);

    private static bool IsWantedState(string state, ServiceControllerStatus wantedStatus) =>
        state == MapStatus(wantedStatus);

    private static string MapStatus(ServiceControllerStatus status) => status switch
    {
        ServiceControllerStatus.Running => "Running",
        ServiceControllerStatus.Stopped => "Stopped",
        ServiceControllerStatus.StartPending => "StartPending",
        ServiceControllerStatus.StopPending => "StopPending",
        ServiceControllerStatus.PausePending => "PausePending",
        ServiceControllerStatus.Paused => "Paused",
        ServiceControllerStatus.ContinuePending => "ContinuePending",
        _ => "Unknown"
    };
}
