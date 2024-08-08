using DotnetProcessBridge.Messages;

namespace DotnetProcessBridge.Client
{
    public interface IDispatcher
    {
        IMessageSender Sender { set; }
    }
}
