namespace Tyresoles.Data.Features.WindowsServices;

public abstract class WindowsServiceException : Exception
{
    protected WindowsServiceException(string message) : base(message) { }
}

public sealed class WindowsServiceNotSupportedException : WindowsServiceException
{
    public WindowsServiceNotSupportedException()
        : base("Windows service management is only supported on Windows.") { }
}

public sealed class WindowsServiceFeatureDisabledException : WindowsServiceException
{
    public WindowsServiceFeatureDisabledException()
        : base("Windows service management is disabled.") { }
}

public sealed class WindowsServiceNotAllowedException : WindowsServiceException
{
    public WindowsServiceNotAllowedException(string message) : base(message) { }
}

public sealed class WindowsServiceOperationException : WindowsServiceException
{
    public WindowsServiceOperationException(string message) : base(message) { }
}
