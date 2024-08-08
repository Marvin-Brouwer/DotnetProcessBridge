namespace DotnetProcessBridge.Child.TestModels;

public interface IExample
{
    public string AppendGuid(string prefix, string postfix);
	string ThrowException(string message);
}
