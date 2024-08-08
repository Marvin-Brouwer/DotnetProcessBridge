namespace DotnetProcessBridge;

// TODO CSDOC
public interface IConnectionStatus
{
    bool ConnectionSuccessful { get; }
    void ThrowIfConnectionFailed();
    ValueTask WaitForConnection(TimeSpan? timeout = null, bool throwOnTimeout = true);
}