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
				// TODO verify valueTask
				BridgeDelegate delegateTask = (parameters) => {
					var result = method.Invoke(handler, parameters);
					if (result is null) return Task.FromResult<object?>(null);
					// At runtime this is some generated AsyncBox type thing, it has the same properties as a Task<T>
					return Unsafe.As<object, Task<object?>>(ref result);
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