using DotnetProcessBridge;
using DotnetProcessBridge.Child.TestModels;

var readHandle = args[0];
var writeHandle = args[1];

await using var client = ProcessBridge.CreateClient<IExample, ExampleDispatcher>(readHandle, writeHandle);
await client.WaitForConnection();

var result1 = client.Dispatch.AppendGuid("AAA", "BBB");
Console.WriteLine(result1);

try
{
	client.Dispatch.ThrowException("This is a test");
}
catch (Exception ex)
{
	var result2 = ex.GetType().Name;
	Console.WriteLine(result2);
	var result3 = ex.Message;
	Console.WriteLine(result3);
}