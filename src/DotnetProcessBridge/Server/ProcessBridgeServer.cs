using DotnetProcessBridge.Messages;

using System.Diagnostics;
using System.IO.Pipes;

namespace DotnetProcessBridge.Server;

public sealed class ProcessBridgeServer<TInterface> : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly CancellationToken _cancellationToken;

    private readonly AnonymousPipeServerStream _readStream;
    private readonly AnonymousPipeServerStream _writeStream;
    private readonly MessageReceiver _messageReceiver;

    public string Handle { get; }

    internal ProcessBridgeServer(TInterface handler, CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationToken = _cancellationTokenSource.Token;

		// Create 2 anonymous pipes (read and write) for duplex communications (each pipe is one-way)
		_readStream = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        _writeStream = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);

		var readHandle = _readStream.GetClientHandleAsString();
		var writeHandle = _writeStream.GetClientHandleAsString();
		Handle = string.Join("+", readHandle, writeHandle);

        _messageReceiver = new MessageReceiver(_readStream, _writeStream, _cancellationToken, DelegateBuilder.BuildDelegates(handler));
        _messageReceiver.Listen();
    }

    /// <inheritdoc />
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
		try
		{
			await _cancellationTokenSource.CancelAsync();

			if (_readStream.IsConnected)
				_readStream.Close();
			if (!_readStream.ClientSafePipeHandle.IsClosed)
				_readStream.DisposeLocalCopyOfClientHandle();
			await _readStream.DisposeAsync();

			if (_writeStream.IsConnected)
				_writeStream.Close();
			if (!_writeStream.ClientSafePipeHandle.IsClosed)
				_writeStream.DisposeLocalCopyOfClientHandle();
			await _writeStream.DisposeAsync();
		}
		catch (System.Runtime.InteropServices.SEHException)
		{
			// For some reason the closing of the pipes seems to fail when the debugger is attached.
			if (Debugger.IsAttached) return;
			throw;
		}
    }
}