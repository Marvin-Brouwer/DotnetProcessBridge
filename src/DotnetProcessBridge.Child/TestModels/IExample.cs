namespace DotnetProcessBridge.Child.TestModels;

public interface IExample
{
    string AppendGuid(string prefix, string postfix);
	void ThrowException();
	ValueTask<string> ValueTask(int number);
	ValueTask EmptyValueTask();
	ValueTask ThrowingValueTask();

	Task<string> AsyncTest();
	Task AsyncThrow();
}
