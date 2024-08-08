using DotnetProcessBridge.Child.TestModels;
using DotnetProcessBridge.Tests.ExternalInvocation;

using FluentAssertions;

namespace DotnetProcessBridge.Tests.Tests;

public class UsageTests
{
	/// <summary>
	/// The reason we have a "direct" and a "non-direct" version is because the purpose of this library is to share an interface between processes.
	/// So, the "non-direct" version. <br />
	/// However, this is utterly un-debuggable, so we reflect out an entry point and fake the process invocation.
	/// </summary>
	[Theory, InlineData(true), InlineData(false)]
	public async Task Example(bool direct)
	{
		var assemblyToTest = typeof(IExample).Assembly;

		// Create a server pipe bound to the interface
		await using var server = ProcessBridge.CreateServer<IExample, TestExample>();

		var resultOutput = direct
			? assemblyToTest.InvokeDirect(server.Handle)
			: await assemblyToTest.InvokeProcess(server.Handle);

		var results = resultOutput.Split(Environment.NewLine);
		results[0].Should().BeEquivalentTo("AAA acf70d64-60c9-4e8c-a716-99e831d26e78 BBB");
		results[1].Should().BeEquivalentTo("AccessViolationException");
		results[2].Should().BeEquivalentTo("This is a test");
	}
}

internal sealed class TestExample : IExample
{
	public string AppendGuid(string prefix, string postfix)
	{
		return string.Join(" ", prefix, "acf70d64-60c9-4e8c-a716-99e831d26e78", postfix);
	}

	public string ThrowException(string message)
	{
		throw new AccessViolationException(message);
	}
}