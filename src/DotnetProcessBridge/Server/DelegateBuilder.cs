
using System.Reflection;

namespace DotnetProcessBridge.Server
{
    public sealed class DelegateBuilder
    {
        public delegate object? BridgeDelegate(params object?[] parameters);

        public interface IDelegateMap : IReadOnlyDictionary<string, (BridgeDelegate, MethodInfo)>;
        private sealed class DelegateMap : Dictionary<string, (BridgeDelegate, MethodInfo)>, IDelegateMap { }

        public static IDelegateMap BuildDelegates<TInterface>(TInterface handler)
        {
            var delegates = new DelegateMap();
            var interfaceType = typeof(TInterface);
            foreach (var method in interfaceType.GetMethods())
            {
                var key = interfaceType.FullName + '.' + method.Name;
                BridgeDelegate delegateFunc = (parameters) => method.Invoke(handler, parameters);
                delegates.Add(key, (delegateFunc, method));
            }

            return delegates;
        }
    }
}