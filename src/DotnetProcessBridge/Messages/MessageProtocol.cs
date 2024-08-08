using DotnetProcessBridge.Constants;

using Newtonsoft.Json;

using System.Diagnostics;
using System.Reflection;

namespace DotnetProcessBridge.Messages;

/// <summary>
/// Very basic protocol to serialize method calls
/// It uses an Id (basically a fixed length timestamp) to cross reference for async support
/// Currently not implemented
/// The idea is quite simple, every "line" in the stream starts with a marker char.
/// This char determines whether the current location in code can handle the line.
/// Sending is syncronous and therefore sequential
/// Receiving is asyncronous and therefore requires the id.
/// </summary>
internal static class MessageProtocol
{
	private const int IdLength = 13; // 3af3c14996e54
	private const string IdFormat = "X13";

	private static class Marker {
		internal const char Method = (char)1;
		internal const char ParamStart = (char)2;
		internal const char Result = (char)3;
		internal const char Exception = (char)4;
		internal const char Spacer = (char)0;
	}

	/// <summary>
	/// Writes
	/// <code>
	/// {Method}<paramref name="id"/><paramref name="methodName"/>{NewLine}
	/// {ParamStart}<paramref name="args"/>[n]{NewLine}...
	/// </code>
	/// </summary>
	public static void WriteMethodCall(this StreamWriter writer, long id, string methodName, object?[] parameters, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested) return;

		writer.Write(Marker.Method);
		writer.Write(id.ToString(IdFormat));

		writer.WriteLine(methodName);
		foreach (var parameter in parameters)
		{
			if (cancellationToken.IsCancellationRequested) break;
			var parameterString = JsonConvert.SerializeObject(parameter, SerializationConstants.JsonSettings).AsMemory();
			writer.Write(Marker.ParamStart);
			writer.WriteLine(parameterString);
		}
		writer.Flush();
	}

	/// <summary>
	/// Reads the request to invoke a method to match to a delegate
	/// <code>
	/// {Method}<paramref name="id"/><paramref name="methodName"/>{NewLine}.
	/// </code>
	/// </summary>
	public static async Task<(string id, string methodName)?> ReadMethodHandle(this StreamReader reader, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested) return null;

		var nextByte = reader.Peek();
		if (nextByte == -1) return null;

		if ((char)nextByte != Marker.Method) return null;
		_ = reader.Read();

		var id = ReadId(reader);
		var methodName = await reader.ReadLineAsync(cancellationToken);

		return (id, methodName!);
	}

	/// <summary>
	/// Reads a single parameter line
	/// <code>
	/// {ParamStart}<paramref name="args"/>[n]{NewLine}
	/// </code>
	/// </summary>
	private static async Task<(bool isParam, object? value)> ReadParameter(this StreamReader reader, Type parameterType, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested) return (false, default!);

		var nextByte = reader.Peek();
		if (nextByte == -1) return (false, default!);

		if ((char)nextByte != Marker.ParamStart) return (false, default!);
		_ = reader.Read();

		// TODO From this point on we really should throw
		var parameterString = await reader.ReadLineAsync(cancellationToken);
		if (cancellationToken.IsCancellationRequested) return (false, default!); 

		if (string.IsNullOrWhiteSpace(parameterString)) return (false, default!);
		var parameterValue = JsonConvert.DeserializeObject(parameterString, parameterType, SerializationConstants.JsonSettings);

		return (true, parameterValue);
	}

	/// <summary>
	/// Reads parameter lines based on the <paramref name="parameterInfos"/>
	/// <code>
	/// {ParamStart}<paramref name="args"/>[n]{NewLine}...
	/// </code>
	/// </summary>
	public static async Task<object?[]> ReadParameters(this StreamReader reader, ParameterInfo[] parameterInfos, CancellationToken cancellationToken)
	{
		if (parameterInfos.Length == 0) return Array.Empty<object?>();

		var parameters = new object?[parameterInfos.Length];
		for (var i = 0; i < parameterInfos.Length; i++)
		{
			if (cancellationToken.IsCancellationRequested) return parameters;

			var parameterInfo = parameterInfos[i];
			var (isParam, parameterValue) = await ReadParameter(reader, parameterInfo.ParameterType, cancellationToken);
			if (!isParam) continue;

			parameters[i] = parameterValue;
		}

		return parameters;
	}

	/// <summary>
	/// Writes a serialized result
	/// <code>
	/// {Result}<paramref name="id"/>{Spacer}<paramref name="returnValue"/>{NewLine}
	/// </code>
	/// </summary>
	public static async Task WriteReturnValue(this StreamWriter writer, string id, object? returnValue, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested) return;

		writer.Write(Marker.Result);
		await writer.WriteAsync(id.AsMemory(), cancellationToken);
		writer.Write(Marker.Spacer);

		var returnString = JsonConvert.SerializeObject(returnValue, SerializationConstants.JsonSettings).AsMemory();
		await writer.WriteLineAsync(returnString, cancellationToken);
		await writer.FlushAsync(cancellationToken);
	}

	/// <summary>
	/// Writes a serialized result
	/// <code>
	/// {Result}<paramref name="id"/>{Spacer}{NewLine}
	/// </code>
	/// </summary>
	public static async Task WriteReturnValue(this StreamWriter writer, string id, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested) return;

		writer.Write(Marker.Result);
		await writer.WriteAsync(id.AsMemory(), cancellationToken);
		writer.WriteLine(Marker.Spacer);
		await writer.FlushAsync(cancellationToken);
	}

	/// <summary>
	/// Writes a serialized exception
	/// <code>
	/// {Exception}<paramref name="id"/>{Spacer}<typeparamref name="TValue"/>{Spacer}<paramref name="exception"/>{NewLine}
	/// </code>
	/// </summary>
	public static async Task WriteException<TValue>(this StreamWriter writer, string id, TValue exception, CancellationToken cancellationToken)
		where TValue : Exception
	{
		if (cancellationToken.IsCancellationRequested) return;

		await writer.FlushAsync(cancellationToken);
		writer.Write(Marker.Exception);
		await writer.WriteAsync(id.AsMemory(), cancellationToken);
		writer.Write(Marker.Spacer);

		await writer.WriteAsync(exception.GetType().AssemblyQualifiedName!.AsMemory(), cancellationToken);
		writer.Write(Marker.Spacer);

		var returnString = JsonConvert.SerializeObject(exception, SerializationConstants.JsonSettings).AsMemory();
		await writer.WriteLineAsync(returnString, cancellationToken);
		await writer.FlushAsync(cancellationToken);
	}

	public static (bool isResult, TReturn result) ReadResult<TReturn>(this StreamReader reader, long expectedId, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested) return (false, default!);

		var nextByte = reader.Peek();
		if (nextByte == -1) return (false, default!);

		if ((char)nextByte != Marker.Result && (byte)nextByte != Marker.Exception) return (false, default!);
		_ = reader.Read();

		var id = ReadId(reader);
		if (id != expectedId.ToString(IdFormat))
		{
			// TODO read into concurrent dictionary and unref by id instead of reading sequentially
			throw new NotImplementedException("TODO read into concurrent dictionary and unref by id instead of reading sequentially");
		}

		// Skip spacer
		_ = reader.Read();

		var returnString = reader.ReadLine();
		// TODO throw
		if (string.IsNullOrWhiteSpace(returnString)) return (false, default!);

		if ((char)nextByte == Marker.Result)
		{
			if (typeof(TReturn) == typeof(void)) return (true, default!);

			var returnValue = JsonConvert.DeserializeObject<TReturn>(returnString, SerializationConstants.JsonSettings)!;
			return (true, returnValue);
		}
		if ((char)nextByte == Marker.Exception)
		{
			// Find the first index of this, so we don't need to do string escape gymnastics in the serialized result.
			var spacerPosition = returnString.IndexOf(Marker.Spacer);
			var exceptionTypeName = returnString[new Range(0, spacerPosition)]!;
			var exceptionData = returnString[new Range(spacerPosition + 1, returnString.Length)]!;
			var exceptionType = Type.GetType(exceptionTypeName)!;

			// TODO is it worth cleaning up the exception stack?
			var exception = (Exception)JsonConvert.DeserializeObject(exceptionData, exceptionType, SerializationConstants.JsonSettings)!;
			throw exception;
		}

		throw new UnreachableException();
	}

	private static string ReadId(StreamReader reader)
	{
		var idChars = new char[IdLength];
		reader.ReadBlock(idChars, 0, IdLength);
		var id = new String(idChars, 0, idChars.Length);
		return id;
	}
}
