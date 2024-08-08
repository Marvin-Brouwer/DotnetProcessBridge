using Ardalis.GuardClauses;
using DotnetProcessBridge.Client;
using DotnetProcessBridge.Server;

namespace DotnetProcessBridge
{
    public static class ProcessBridge
    {
        public static ProcessBridgeServer<TInterface> CreateServer<TInterface, THandler>(CancellationToken ? cancellationToken = default) 
            where THandler : TInterface, new()
        {
            Guard.Against.InvalidInput(typeof(TInterface), nameof(TInterface), (type) => type.IsInterface);

            var handler = new THandler();

            return new ProcessBridgeServer<TInterface>(handler, cancellationToken ?? CancellationToken.None);
        }

        public static IProcessBridgeClient<TInterface> CreateClient<TInterface, TDispatcher>(string readHandle, string writeHandle, CancellationToken? cancellationToken = default)
            where TDispatcher : TInterface, IDispatcher, new()
        {
            Guard.Against.NullOrWhiteSpace(readHandle);
            Guard.Against.NullOrWhiteSpace(writeHandle);
            Guard.Against.InvalidInput(typeof(TInterface), nameof(TInterface), (type) => type.IsInterface);

            var dispatcher = new TDispatcher();

            return new ProcessBridgeClient<TInterface, TDispatcher>(readHandle, writeHandle, dispatcher, cancellationToken ?? CancellationToken.None);
        }
    }
}
