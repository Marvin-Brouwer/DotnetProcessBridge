using DotnetProcessBridge.Messages;
using System.IO.Pipes;

namespace DotnetProcessBridge.Server
{
    public sealed class ProcessBridgeServer<TInterface> : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CancellationToken _cancellationToken;

        private readonly AnonymousPipeServerStream _readStream;
        private readonly AnonymousPipeServerStream _writeStream;
        private readonly MessageReceiver _messageReceiver;
        //private bool _firstByte = false;

        public string ReadHandle { get; }
        public string WriteHandle { get; }

        internal ProcessBridgeServer(TInterface handler, CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cancellationToken = _cancellationTokenSource.Token;

            // Create 2 anonymous pipes (read and write) for duplex communications (each pipe is one-way)
            _readStream = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            ReadHandle = _readStream.GetClientHandleAsString();
            _writeStream = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
            WriteHandle = _writeStream.GetClientHandleAsString();

            _messageReceiver = new MessageReceiver(_readStream, _writeStream, _cancellationToken, DelegateBuilder.BuildDelegates(handler));
            _messageReceiver.Listen();
        }

        /// <inheritdoc />
        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            await _cancellationTokenSource.CancelAsync();
            _readStream.DisposeLocalCopyOfClientHandle();
            _readStream.Close();
            _readStream.Dispose();
            _writeStream.DisposeLocalCopyOfClientHandle();
            _writeStream.Close();
            _writeStream.Dispose();
        }
    }
}