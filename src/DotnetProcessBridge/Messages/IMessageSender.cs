using System.Reflection;

namespace DotnetProcessBridge.Messages
{
    public interface IMessageSender
    {
        void Dispatch(MethodBase method, params object?[] parameters);
        TReturn Dispatch<TReturn>(MethodBase method, params object?[] parameters);
    }
}