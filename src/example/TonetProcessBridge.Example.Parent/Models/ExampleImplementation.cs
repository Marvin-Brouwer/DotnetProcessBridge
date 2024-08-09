using DotnetProcessBridge.Example.Child.Models;

namespace TonetProcessBridge.Example.Parent.Models;

internal sealed class ExampleImplementation : IExample
{
	public string AppendGuid(string prefix, string postfix)
	{
		return string.Join(" ", prefix, "acf70d64-60c9-4e8c-a716-99e831d26e78", postfix);
	}

	public async Task<string> AsyncTest()
	{
		await Task.Delay(10);
		return "Async rules!";
	}

	public void ThrowException()
	{
		throw new AccessViolationException("This is a test");
	}

	public Task AsyncThrow()
	{
		throw new AccessViolationException("This is a test");
	}

	public ValueTask<string> ValueTask(int number)
	{
		return new ValueTask<string>(Task.FromResult(number.ToString()));
	}

	public async ValueTask EmptyValueTask()
	{
		await Task.Delay(10);
	}

	public ValueTask ThrowingValueTask()
	{
		throw new AccessViolationException("This is a test");
	}

	public Task RandomDelay()
	{
		return Task.Delay(Random.Shared.Next(100));
	}
}