using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;

namespace DotnetProcessBridge.Messages;

internal sealed class MessageSender : IMessageSender
{
    private readonly PipeStream _readStream;
    private readonly PipeStream _writeStream;
    private readonly CancellationToken _cancellationToken;

    public MessageSender(PipeStream readStream, PipeStream writeStream, CancellationToken cancellationToken)
    {
        _readStream = readStream;
        _writeStream = writeStream;
        _cancellationToken = cancellationToken;
    }

	public ValueTask<TReturn> DispatchValueTask<TReturn>(MethodBase method, object?[] parameters)
	{
		return new ValueTask<TReturn>(DispatchTask<TReturn>(method, parameters));
	}
	public ValueTask DispatchValueTask(MethodBase method, object?[] parameters)
	{
		return new ValueTask(DispatchTask(method, parameters));
	}

	public Task<TReturn> DispatchTask<TReturn>(MethodBase method, object?[] parameters)
    {
		try
		{
			var result = Dispatch<TReturn>(method, parameters);
			return Task.FromResult(result);
		}
		catch (Exception ex)
		{
			return Task.FromException<TReturn>(ex);
		}
	}
    public Task DispatchTask(MethodBase method, object?[] parameters)
	{
		try
		{
			Dispatch(method, parameters);
			return Task.CompletedTask;
		}
		catch (Exception ex)
		{
			return Task.FromException(ex);
		}
	}

    public TReturn Dispatch<TReturn>(MethodBase method, object?[] parameters)
    {
        return Dispatch<TReturn>(method, typeof(TReturn), parameters);
    }
    public void Dispatch(MethodBase method, object?[] parameters)
    {
        Dispatch<object?>(method, typeof(void), parameters);
    }

    private TReturn Dispatch<TReturn>(MethodBase method, Type returnType, object?[] parameters)
    {
        using var writer = new StreamWriter(_writeStream, leaveOpen: true);
        // This method base comes with the full type name, as opposed to regular reflection.
        var methodName = method.Name;
		var ticks = new DateTime(2016, 1, 1).Ticks;
		var timeId = DateTime.Now.Ticks - ticks;

		if (_cancellationToken.IsCancellationRequested) return default!;
		writer.WriteMethodCall(timeId, methodName, parameters, _cancellationToken);
#pragma warning disable CA1416 // Validate platform compatibility
        if (Environment.OSVersion.Platform == PlatformID.Win32NT) _writeStream.WaitForPipeDrain();
#pragma warning restore CA1416 // Validate platform compatibility

        if (_cancellationToken.IsCancellationRequested) return default!;

        using var reader = new StreamReader(_readStream, leaveOpen: true);
        while (!_cancellationToken.IsCancellationRequested)
        {
            if (!_readStream.CanRead) continue;
			var (isResult, returnvalue) = reader.ReadResult<TReturn>(timeId, returnType, _cancellationToken);

			if (!isResult) continue;
			if (returnType == typeof(void)) return default!;
			if (returnType == typeof(Task)) return default!;
			if (returnType == typeof(ValueTask)) return default!;
			return returnvalue;
        }

        throw new UnreachableException();
    }
}
