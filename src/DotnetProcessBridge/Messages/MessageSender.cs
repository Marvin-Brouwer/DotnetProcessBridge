using DotnetProcessBridge.Constants;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;

namespace DotnetProcessBridge.Messages
{
    /// <summary>
    /// TODO: Create a message format IE:
    /// <code>
    /// { 
    ///     success: true,
    ///     result: "Return value",
    ///     error: null,
    /// }
    /// </code>
    /// The current implementation expects a line by line format, which will break horribly if a method ever is requested which isn't delegated.
    /// Also this doesn't support exception.
    /// Alternatively, we could prepend IE:
    /// <code>
    /// Invoke: InterfaceName.MethodName
    /// Param: "AAA"
    /// Param: "BBB"
    /// 
    /// Result: "AAA acf70d64-60c9-4e8c-a716-99e831d26e78 BBB"
    /// 
    /// Invoke: InterfaceName.MethodName
    /// Param: 123
    /// 
    /// Exception: System.Exception { Message: ..... }
    /// </code>
    /// </summary>
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

        public Task<TReturn> DispatchAsync<TReturn>(MethodBase method, object?[] parameters)
        {
            return Task.FromResult(Dispatch<TReturn>(method, parameters));
        }
        public Task DispatchAsync(MethodBase method, object?[] parameters)
        {
            Dispatch(method, parameters);
            return Task.CompletedTask;
        }

        public TReturn Dispatch<TReturn>(MethodBase method, object?[] parameters)
        {
            return Dispatch<TReturn>(method, typeof(TReturn), parameters);
        }
        public void Dispatch(MethodBase method, object?[] parameters)
        {
            Dispatch<object?>(method, typeof(void), parameters);
        }

        // TODO LOCK?
        private TReturn Dispatch<TReturn>(MethodBase method, Type returnType, object?[] parameters)
        {
            using var writer = new StreamWriter(_writeStream, leaveOpen: true);
            // This method base comes with the full type name, as opposed to regular reflection.
            var methodName = method.Name;

            if (_cancellationToken.IsCancellationRequested) return default!;
            writer.WriteLine(methodName);
            foreach (var parameter in parameters)
            {
                if (_cancellationToken.IsCancellationRequested) break;
                var parameterString = JsonConvert.SerializeObject(parameter, SerializationConstants.JsonSettings).AsMemory();
                writer.WriteLine(parameterString);
            }
            writer.Flush();

            if (_cancellationToken.IsCancellationRequested) return default!;
#pragma warning disable CA1416 // Validate platform compatibility
            if (Environment.OSVersion.Platform == PlatformID.Win32NT) _writeStream.WaitForPipeDrain();
#pragma warning restore CA1416 // Validate platform compatibility


            if (returnType == typeof(void)) return default!;
            if (_cancellationToken.IsCancellationRequested) return default!;

            using var reader = new StreamReader(_readStream, leaveOpen: true);
            while (!_cancellationToken.IsCancellationRequested)
            {
                if (!_readStream.CanRead) continue;

                var returnString = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(returnString)) continue;

                var returnValue = JsonConvert.DeserializeObject<TReturn>(returnString, SerializationConstants.JsonSettings)!;
                return returnValue;
            }

            throw new UnreachableException();
        }
    }
}
