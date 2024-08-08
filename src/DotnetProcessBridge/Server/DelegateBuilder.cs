using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotnetProcessBridge.Server;

internal sealed class DelegateBuilder
{
	internal delegate Task<object?> BridgeDelegate(params object?[] parameters);

	internal interface IDelegateMap : IReadOnlyDictionary<string, (BridgeDelegate, MethodInfo)>;
    private sealed class DelegateMap : Dictionary<string, (BridgeDelegate, MethodInfo)>, IDelegateMap { }

	internal static IDelegateMap BuildDelegates<TInterface>(TInterface handler)
    {
        var delegates = new DelegateMap();
        var interfaceType = typeof(TInterface);
        foreach (var method in interfaceType.GetMethods())
		{
			var key = interfaceType.FullName + '.' + method.Name;

			if (method.ReturnType.GetInterface(nameof(IAsyncResult)) is not null)
			{
				BridgeDelegate delegateTask = (parameters) => {
					var result = method.Invoke(handler, parameters);
					if (result is null) return Task.FromResult<object?>(null);
					// At runtime this is some generated AsyncBox type thing, it has the same properties as a Task<T>
					return Unsafe.As<object, Task<object?>>(ref result);
				};
				delegates.Add(key, (delegateTask, method));
				continue;
			}
			if (method.ReturnType == typeof(ValueTask))
			{
				BridgeDelegate delegateTask = async (parameters) => {
					var result = method.Invoke(handler, parameters);
					if (result is null) throw new UnreachableException();
					var task = result.GetType().GetMethod(nameof(ValueTask.AsTask))!.Invoke(result, Array.Empty<object>())!;
					await Unsafe.As<object, Task>(ref task);
					return default!;
				};
				delegates.Add(key, (delegateTask, method));
				continue;
			}
			if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
			{
				BridgeDelegate delegateTask = (parameters) => {
					var result = method.Invoke(handler, parameters);
					if (result is null) throw new UnreachableException();
					// Unpack the ValueTask using reflection
					var task = result.GetType().GetMethod(nameof(ValueTask<object>.AsTask))!.Invoke(result, Array.Empty<object>())!;
					return Unsafe.As<object, Task<object?>>(ref task);
				};
				delegates.Add(key, (delegateTask, method));
				continue;
			}

			BridgeDelegate delegateFunc = (parameters) => Task.FromResult(method.Invoke(handler, parameters));
            delegates.Add(key, (delegateFunc, method));
        }

        return delegates;
    }
}