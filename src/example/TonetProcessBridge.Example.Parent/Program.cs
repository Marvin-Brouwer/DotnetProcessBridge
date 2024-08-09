using DotnetProcessBridge.Example.Child.Models;
using DotnetProcessBridge.Tests.ExternalInvocation;

using FluentAssertions;

using System.Diagnostics;
using System.Reflection;

using TonetProcessBridge.Example.Parent.Models;

namespace DotnetProcessBridge.Tests.Tests;

public class Program
{
	private static readonly Assembly AssemblyToTest = typeof(IExample).Assembly;
	private readonly CancellationToken _cancellationToken;

	public Program()
	{
		// This is just in case the process hangs
		var ctx = Debugger.IsAttached
			? new CancellationTokenSource()
			: new CancellationTokenSource(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));

		_cancellationToken = ctx.Token;
	}
	/// <summary>
	/// Invoking the Process isn't really testable, however, invoking direct stopped working when not attached after we added true async support.
	/// So we'll just check for being attached.
	/// </summary>
	[Fact]
	public async Task Example()
    {
		// Create a server pipe bound to the interface
		await using var server = ProcessBridge.CreateServer<IExample, ExampleImplementation>(_cancellationToken);

		var resultOutput = Debugger.IsAttached
            ? await AssemblyToTest.InvokeDirect(server.Handle)
			: await AssemblyToTest.InvokeProcess(server.Handle);

		_cancellationToken.ThrowIfCancellationRequested();

		var results = resultOutput.Split(Environment.NewLine);
		results[0].Should().BeEquivalentTo("AAA acf70d64-60c9-4e8c-a716-99e831d26e78 BBB");
		results[1].Should().BeEquivalentTo("AccessViolationException");
		results[2].Should().BeEquivalentTo("This is a test");
		results[3].Should().BeEquivalentTo("Async rules!");
		results[4].Should().BeEquivalentTo("AccessViolationException");
		results[5].Should().BeEquivalentTo("This is a test");
		results[6].Should().BeEquivalentTo("420");
		results[7].Should().BeEquivalentTo("");
		results[8].Should().BeEquivalentTo("This is a test");
	}
}
