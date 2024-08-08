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


            var methodQualifiedName = await reader.ReadLineAsync(_cancellationToken);
            if (string.IsNullOrEmpty(methodQualifiedName)) continue;
            if (!_handlers.TryGetValue(methodQualifiedName, out var delegateDefinition)) continue;

            var (handler, methodInfo) = delegateDefinition;
            HandleMessage(reader, writer, handler, methodInfo);
        }
    }

    private async void HandleMessage(StreamReader reader, StreamWriter writer, BridgeDelegate handler, MethodInfo methodInfo)
    {
        var parameterInfo = methodInfo.GetParameters();

        var parameters = await ReadParameters(reader, parameterInfo, _cancellationToken);
        var returnValue = handler(parameters);

        if (_cancellationToken.IsCancellationRequested) return;

        if (methodInfo.ReturnType != typeof(void))
        {
            var returnString = JsonConvert.SerializeObject(returnValue, SerializationConstants.JsonSettings).AsMemory();
            await writer.WriteLineAsync(returnString, _cancellationToken);
            await writer.FlushAsync(_cancellationToken);

#pragma warning disable CA1416 // Validate platform compatibility
            if (Environment.OSVersion.Platform == PlatformID.Win32NT) _writeStream.WaitForPipeDrain();
#pragma warning restore CA1416 // Validate platform compatibility
        }
    }

    public async Task<object?[]> ReadParameters(StreamReader reader, ParameterInfo[] parameterInfos, CancellationToken cancellationToken)
    {
        if (parameterInfos.Length == 0) return Array.Empty<object?>();

        var parameters = new object?[parameterInfos.Length];
        for (var i = 0; i < parameterInfos.Length; i++)
        {
            if (cancellationToken.IsCancellationRequested) return parameters;

            var parameterInfo = parameterInfos[i];

            var parameterString = await reader.ReadLineAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested) return parameters;

            // TODO throw?
            if (string.IsNullOrWhiteSpace(parameterString)) continue;
            parameters[i] = JsonConvert.DeserializeObject(parameterString, parameterInfo.ParameterType, SerializationConstants.JsonSettings);
        }

        return parameters;
    }
}
