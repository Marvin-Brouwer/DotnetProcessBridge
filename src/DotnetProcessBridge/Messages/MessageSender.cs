using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotnetProcessBridge.Messages;

internal sealed class MessageSender : IMessageSender
{
    private readonly PipeStream _readStream;
    private readonly PipeStream _writeStream;
    private readonly CancellationToken _cancellationToken;

	private readonly static ConcurrentDictionary<string, Func<Type, object?>> _results = new();

	public MessageSender(PipeStream readStream, PipeStream writeStream, CancellationToken cancellationToken)
    {
        _readStream = readStream;
        _writeStream = writeStream;
        _cancellationToken = cancellationToken;
	}

	public void Listen()
	{
		var succesfullyQueued = ThreadPool.QueueUserWorkItem(ListenToStream, this, false);

		// If we are out of threads just start a new Task in the scheduler
		if (!succesfullyQueued)
		{
			_ = Task.Factory.StartNew(ListenToStream, null, _cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
		}
	}

	// TODO this can be async now
	private void ListenToStream<T>(T? _ = default)
	{
		using var reader = new StreamReader(_readStream, leaveOpen: true);

		while (!_cancellationToken.IsCancellationRequested)
		{
			if (!_readStream.IsConnected) continue;
			if (!_writeStream.IsConnected) continue;
			if (!_readStream.CanRead) continue;


			TryAddStreamedFunc(reader);
		}
	}

	public async ValueTask<TReturn> DispatchValueTask<TReturn>(MethodBase method, object?[] parameters)
	{
		return await DispatchTask<TReturn>(method, parameters);
	}
	public async ValueTask DispatchValueTask(MethodBase method, object?[] parameters)
	{
		await DispatchTask(method, parameters);
	}

	public Task<TReturn> DispatchTask<TReturn>(MethodBase method, object?[] parameters)
	{
		var tcs = new TaskCompletionSource<TReturn>();
		var succesfullyQueued = ThreadPool.QueueUserWorkItem(RunDispatch, false);

		// If we are out of threads just start a new Task in the scheduler
		if (!succesfullyQueued)
		{
			_ = Task.Factory.StartNew(RunDispatch, null, _cancellationToken, TaskCreationOptions.None, TaskScheduler.Current);
		}
		return tcs.Task;

		void RunDispatch(object? _)
		{
			try
			{
				var result = Dispatch<TReturn>(method, parameters);
				tcs.SetResult(result);
			}
			catch (Exception ex)
			{
				tcs.SetException(ex);
			}
		}
	}
    public Task DispatchTask(MethodBase method, object?[] parameters)
	{
		var tcs = new TaskCompletionSource();
		var succesfullyQueued = ThreadPool.QueueUserWorkItem(RunDispatch, false);

		// If we are out of threads just start a new Task in the scheduler
		if (!succesfullyQueued)
		{
			_ = Task.Factory.StartNew(RunDispatch, null, _cancellationToken, TaskCreationOptions.None, TaskScheduler.Current);
		}

		return tcs.Task;

		void RunDispatch(object? _)
		{
			try
			{
				Dispatch(method, parameters);
				tcs.SetResult();
			}
			catch (Exception ex)
			{
				tcs.SetException(ex);
			}
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
		var timeId = MessageProtocol.CreateId();

		if (_cancellationToken.IsCancellationRequested) return default!;
		writer.WriteMethodCall(timeId, methodName, parameters, _cancellationToken);
#pragma warning disable CA1416 // Validate platform compatibility
        if (Environment.OSVersion.Platform == PlatformID.Win32NT) _writeStream.WaitForPipeDrain();
#pragma warning restore CA1416 // Validate platform compatibility

        if (_cancellationToken.IsCancellationRequested) return default!;

        using var reader = new StreamReader(_readStream, leaveOpen: true);
        while (!_cancellationToken.IsCancellationRequested)
		{
			var resultFunc = TryGetStoredFunc<TReturn>(timeId);
			if (resultFunc is null) continue;

			var returnValue = resultFunc();

			if (returnType == typeof(void)) return default!;
			if (returnType == typeof(Task)) return default!;
			if (returnType == typeof(ValueTask)) return default!;

			return returnValue;
		}

		if (_cancellationToken.IsCancellationRequested) return default!;
		throw new UnreachableException();
    }

	private void TryAddStreamedFunc(StreamReader reader)
	{
		if (!_readStream.CanRead) return;
		var result = reader.ReadResult(_cancellationToken);
		if (result is null) return;
		var (id, resultFunc) = result.Value;

		_results.TryAdd(id, resultFunc);
	}

	private Func<TReturn>? TryGetStoredFunc<TReturn>(string timeId)
	{
		if (!_results.TryRemove(timeId, out var storedResultFunc)) return null;
		return Unsafe.As<Func<TReturn>>(() => storedResultFunc(typeof(TReturn)));
	}
}
