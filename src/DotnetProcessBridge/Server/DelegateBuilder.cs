using System.Reflection;

namespace DotnetProcessBridge.Server;

internal sealed class DelegateBuilder
{
	internal delegate object? BridgeDelegate(params object?[] parameters);

	internal interface IDelegateMap : IReadOnlyDictionary<string, (BridgeDelegate, MethodInfo)>;
    private sealed class DelegateMap : Dictionary<string, (BridgeDelegate, MethodInfo)>, IDelegateMap { }

	internal static IDelegateMap BuildDelegates<TInterface>(TInterface handler)
    {
        var delegates = new DelegateMap();
        var interfaceType = typeof(TInterface);
        foreach (var method in interfaceType.GetMethods())
		{
			if (method.ReturnType == typeof(Task<>))
				throw new NotSupportedException("Not yet supported");
			if (method.ReturnType == typeof(Task))
				throw new NotSupportedException("Not yet supported");
			if (method.ReturnType == typeof(IAsyncResult))
				throw new NotSupportedException("Not yet supported");

			var key = interfaceType.FullName + '.' + method.Name;
            BridgeDelegate delegateFunc = (parameters) => method.Invoke(handler, parameters);
            delegates.Add(key, (delegateFunc, method));
        }

        return delegates;
    }
}