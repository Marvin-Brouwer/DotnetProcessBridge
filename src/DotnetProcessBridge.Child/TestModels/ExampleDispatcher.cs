using DotnetProcessBridge.Client;
using DotnetProcessBridge.Messages;

using System.Reflection;
namespace DotnetProcessBridge.Child.TestModels;

/// <summary>
/// This is the simplest way to do this. <br />
/// Alternatively a DynamicObject can be generated that does this for us, however that's a lot of code and difficult to debug. <br />
/// As another alternative we could generate a proxy with reflection, it's better for exception debugging but more work. <br />
/// <br />
/// This is not a bad approach anyway, since it allows you to do some additional work before and after the dispatch.
/// </summary>
internal sealed class ExampleDispatcher : IDispatcher, IExample
{
    public IMessageSender Sender { set; private get; } = default!;

	string IExample.AppendGuid(string prefix, string postfix)
	{
		return Sender.Dispatch<string>(MethodBase.GetCurrentMethod()!, prefix, postfix);
	}

	void IExample.ThrowException()
	{
		Sender.Dispatch<string>(MethodBase.GetCurrentMethod()!);
	}

	Task<string> IExample.AsyncTest()
	{
		return Sender.DispatchTask<string>(MethodBase.GetCurrentMethod()!);
	}

	Task IExample.AsyncThrow()
	{
		return Sender.DispatchTask(MethodBase.GetCurrentMethod()!);
	}

	ValueTask<string> IExample.ValueTask(int number)
	{
		return Sender.DispatchValueTask<string>(MethodBase.GetCurrentMethod()!, number);
	}

	ValueTask IExample.EmptyValueTask()
	{
		return Sender.DispatchValueTask(MethodBase.GetCurrentMethod()!);
	}

	ValueTask IExample.ThrowingValueTask()
	{
		return Sender.DispatchValueTask(MethodBase.GetCurrentMethod()!);
	}

	Task IExample.RandomDelay()
	{
		return Sender.DispatchTask(MethodBase.GetCurrentMethod()!);
	}
}
