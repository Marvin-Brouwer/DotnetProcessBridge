using DotnetProcessBridge.Constants;

using Newtonsoft.Json;

using System.IO.Pipes;
using System.Reflection;

using static DotnetProcessBridge.Server.DelegateBuilder;

namespace DotnetProcessBridge.Messages;

internal sealed class MessageReceiver
{
    private readonly PipeStream _readStream;
    private readonly PipeStream _writeStream;
    private readonly CancellationToken _cancellationToken;
    private readonly IDelegateMap _handlers;

    public MessageReceiver(PipeStream readStream, PipeStream writeStream, CancellationToken cancellationToken, IDelegateMap handlers)
    {
        _readStream = readStream;
        _writeStream = writeStream;
        _handlers = handlers;
        _cancellationToken = cancellationToken;
    }

    public void Listen() {
        var succesfullyQueued = ThreadPool.QueueUserWorkItem(async r => await r.ListenToStream(), this, false);

        // If we are out of threads just start a new Task in the scheduler
        if (!succesfullyQueued)
        {
            _ = Task.Factory.StartNew(_ => ListenToStream(), TaskCreationOptions.LongRunning, _cancellationToken);
        }
    }

    private async Task ListenToStream()
    {
        using var reader = new StreamReader(_readStream, leaveOpen: true);
        using var writer = new StreamWriter(_writeStream, leaveOpen: true);

        while (!_cancellationToken.IsCancellationRequested)
        {
            if (!_readStream.IsConnected) continue;
            if (!_writeStream.IsConnected) continue;
            if (!_readStream.CanRead) continue;


            var methodHandle = await reader.ReadMethodHandle(_cancellationToken);
			if (!methodHandle.HasValue) continue;
			var (id, methodName) = methodHandle.Value;

			if (string.IsNullOrEmpty(methodName)) continue;
            if (!_handlers.TryGetValue(methodName, out var delegateDefinition)) continue;

            var (handler, methodInfo) = delegateDefinition;
            await HandleMessage(reader, writer, id, handler, methodInfo);
        }
    }

    private async Task HandleMessage(StreamReader reader, StreamWriter writer, string id, BridgeDelegate handler, MethodInfo methodInfo)
    {
        var parameterInfo = methodInfo.GetParameters();
        var parameters = await reader.ReadParameters(parameterInfo, _cancellationToken);
		if (_cancellationToken.IsCancellationRequested) return;

		try
		{
			var returnValue = await handler(parameters);

			if (methodInfo.ReturnType == typeof(void))
			{
				await writer.WriteReturnValue(id, _cancellationToken);
				return;
			}

			if (methodInfo.ReturnType == typeof(Task))
			{
				await writer.WriteReturnValue(id, _cancellationToken);
				return;
			}

			if (methodInfo.ReturnType == typeof(ValueTask))
			{
				await writer.WriteReturnValue(id, _cancellationToken);
				return;
			}

			await writer.WriteReturnValue(id, returnValue, _cancellationToken);
		}
		catch (TargetInvocationException ex)
		{
			if (ex.InnerException is null)
				await writer.WriteException(id, ex, _cancellationToken);
			else
				await writer.WriteException(id, ex.InnerException, _cancellationToken);
		}
		finally
		{
#pragma warning disable CA1416 // Validate platform compatibility
			if (Environment.OSVersion.Platform == PlatformID.Win32NT) _writeStream.WaitForPipeDrain();
#pragma warning restore CA1416 // Validate platform compatibility
		}
	}
}
