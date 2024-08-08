using DotnetProcessBridge.Constants;
using DotnetProcessBridge.Messages;

using System.Data;
using System.IO.Pipes;

namespace DotnetProcessBridge.Client;

public sealed class ProcessBridgeClient<TInterface, TDispatch> : IProcessBridgeClient<TInterface> 
    where TDispatch : TInterface, IDispatcher, new()
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly CancellationToken _cancellationToken;

    private readonly AnonymousPipeClientStream _readStream;
    private readonly AnonymousPipeClientStream _writeStream;

    internal ProcessBridgeClient(string readHandle, string writeHandle, TDispatch dispatcher, CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationToken = _cancellationTokenSource.Token;

        // Create 2 anonymous pipes (read and write) for duplex communications (each pipe is one-way)
        // Handles are mirrored to the server
        // TODO see if this can be a single InOut direction
        _readStream = new AnonymousPipeClientStream(PipeDirection.In, writeHandle);
        _writeStream = new AnonymousPipeClientStream(PipeDirection.Out, readHandle);

        var messageSender = new MessageSender(_readStream, _writeStream, _cancellationToken);
        dispatcher.Sender = messageSender;
        Dispatch = dispatcher;
    }

    public TInterface Dispatch { get; }

    /// <inheritdoc />
    public bool ConnectionSuccessful => _readStream.IsConnected && _writeStream.IsConnected;

    /// <inheritdoc />
    public void ThrowIfConnectionFailed()
    {
        if (_cancellationToken.IsCancellationRequested) return;
        if (!ConnectionSuccessful)
            throw new DataException("Connection failed to establish");
    }

    /// <inheritdoc />
    public ValueTask WaitForConnection(TimeSpan? timeout = null, bool throwOnTimeout = true)
    {
        var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        waitCancellation.CancelAfter(timeout ?? ConnectionConstants.ConnectionWaitTimeOut);

        return new ValueTask(Task.Run(WaitForConnectionTask, waitCancellation.Token));

        void WaitForConnectionTask()
        {
            try
            {
                // Run until canceled or connection is successful
                while (!waitCancellation.IsCancellationRequested)
                {
                    if (ConnectionSuccessful) return;
                }
                if (throwOnTimeout) ThrowIfConnectionFailed();
            }
            finally
            {
                waitCancellation.Dispose();
            }
        }
    }

    /// <inheritdoc />
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await _cancellationTokenSource.CancelAsync();
        _readStream.Dispose();
        _writeStream.Dispose();
    }
}