
namespace DotnetProcessBridge.Client
{
    public interface IProcessBridgeClient<TInterface> : IConnectionStatus, IAsyncDisposable
    {
        TInterface Dispatch { get; }
    }
}