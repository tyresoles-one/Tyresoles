namespace Tyresoles.Data.Features.WindowsServices;

public interface IWindowsServiceManager
{
    Task<IReadOnlyList<WindowsServiceStatusDto>> GetAllStatusesAsync(CancellationToken cancellationToken = default);

    Task<WindowsServiceStatusDto> GetStatusAsync(string serviceName, CancellationToken cancellationToken = default);

    Task<WindowsServiceStatusDto> StartAsync(string serviceName, CancellationToken cancellationToken = default);

    Task<WindowsServiceStatusDto> StopAsync(string serviceName, CancellationToken cancellationToken = default);

    Task<WindowsServiceStatusDto> RestartAsync(string serviceName, CancellationToken cancellationToken = default);
}
