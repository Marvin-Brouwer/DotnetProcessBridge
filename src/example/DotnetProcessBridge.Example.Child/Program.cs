using DotnetProcessBridge;
using DotnetProcessBridge.Example.Child.Models;

using System.Diagnostics;

// This is just in case the process hangs
var ctx = Debugger.IsAttached
	? new CancellationTokenSource()
	: new CancellationTokenSource(TimeSpan.FromMinutes(1));

var handle = args[0];

await using var client = ProcessBridge.CreateClient<IExample, ExampleDispatcher>(handle, ctx.Token);
await client.WaitForConnection();

var result1 = client.Dispatch.AppendGuid("AAA", "BBB");
Console.WriteLine(result1);

try
{
	client.Dispatch.ThrowException();
}
catch (Exception ex)
{
	var result2 = ex.GetType().Name;
	Console.WriteLine(result2);
	var result3 = ex.Message;
	Console.WriteLine(result3);
}

var result4 = await client.Dispatch.AsyncTest();
Console.WriteLine(result4);

try
{
	await client.Dispatch.AsyncThrow();
}
catch (Exception ex)
{
	var result5 = ex.GetType().Name;
	Console.WriteLine(result5);
	var result6 = ex.Message;
	Console.WriteLine(result6);
}

var result7 = await client.Dispatch.ValueTask(420);
Console.WriteLine(result7);

try
{
	await client.Dispatch.EmptyValueTask();
	Console.WriteLine(string.Empty);
}
catch (Exception ex)
{
	Console.WriteLine(ex.Message);
}

try
{
	await client.Dispatch.ThrowingValueTask();
	Console.WriteLine(string.Empty);
}
catch (Exception ex)
{
	Console.WriteLine(ex.Message);
}

// This is to test how async results mess with the system
await Task.WhenAll(
	client.Dispatch.RandomDelay(),
	client.Dispatch.RandomDelay(),
	client.Dispatch.RandomDelay(),
	client.Dispatch.RandomDelay(),
	client.Dispatch.RandomDelay()
);