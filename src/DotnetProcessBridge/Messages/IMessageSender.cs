using System.Reflection;

namespace DotnetProcessBridge.Messages;

public interface IMessageSender
{
    void Dispatch(MethodBase method, params object?[] parameters);
    TReturn Dispatch<TReturn>(MethodBase method, params object?[] parameters);

	Task DispatchTask(MethodBase method, params object?[] parameters);
	Task<TReturn> DispatchTask<TReturn>(MethodBase method, params object?[] parameters);

	ValueTask DispatchValueTask(MethodBase method, params object?[] parameters);
	ValueTask<TReturn> DispatchValueTask<TReturn>(MethodBase method, params object?[] parameters);
}