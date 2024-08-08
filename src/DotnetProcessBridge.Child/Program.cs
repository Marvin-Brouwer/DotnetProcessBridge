using DotnetProcessBridge;
using DotnetProcessBridge.Child.TestModels;

var readHandle = args[0];
var writeHandle = args[1];

await using var client = ProcessBridge.CreateClient<IExample, ExampleDispatcher>(readHandle, writeHandle);
await client.WaitForConnection();

var result = client.Dispatch.AppendGuid("AAA", "BBB");
Console.WriteLine(result);