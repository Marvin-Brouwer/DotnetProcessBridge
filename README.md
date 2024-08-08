> [!CAUTION]  
> This project is an experiment, it's meant to illustrate the possibility.  
>
> - It has not been tested on thread safety (unplanned)
> - It does not support generics (currently)
> - It does not handle Exceptions (currently)

<span></span>

> [!TIP]  
> This is probably not the best way to do things anyways.  
> If you find yourself in the situation where you'd need to share functionality between two processes, perhaps it's worth to re-evaluate your solution.

# DotnetProcessBridge

A quick and easy way to share method calls between two dotnet processes

## Usage

Using the bridge requires 5 parts:
`The shared interface`, `A dispatch class on the client application`, `A handler class on the server application`, `Server registration`, and `Client registration`.

### The shared interface

The goal of this library is to provide a single interface so the client application interfaces with an interface and the server implements that interface.  
What you put in this interface is up to you.

### A dispatch class on the client application

We did not see the immediate value to implement a proxy at this point. This complicates the code significantly.  
To create a dispatcher you'll need to create a class with an empty constructor implementing your interface and `DotnetProcessBridge.Client.IDispatcher`.  
This gives you access to a dispatcher property, an implementation might look something like this:  

```csharp
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
}
```

This is a bit of boilerplate you'll have to implement, in theory this could be handled by a proxy at a later stage.  

### A handler class on the server application

This is simply an implementation on the server side of things.  
This might look something like this:  

```csharp
internal sealed class TestExample : IExample
{
	public string AppendGuid(string prefix, string postfix)
	{
		return string.Join(" ", prefix, "acf70d64-60c9-4e8c-a716-99e831d26e78", postfix);
	}

	public void ThrowException()
	{
		throw new AccessViolationException(message);
	}
}
```

### Server registration

Now you have your types on both ends, you'll need to host an Anonymous pipe for the processes to communicate through.
This looks like this:

```csharp
await using var server = ProcessBridge.CreateServer<IExample, TestExample>();

// This is where you start the other process, pass it server.Handle in the parameters

```

### Client registration

The client application requires a similar setup.
You register your types and wait for a connection, this will setup a connection and return a Dispatch class, which is basically the class you created.  
However, as far as your application is concerned, now you have an interface to use.

```csharp
var handle = args[0];

await using var client = ProcessBridge.CreateClient<IExample, ExampleDispatcher>(handle);
await client.WaitForConnection();

// Give this to your IOC container
var example = client.Dispatch;

var result = example.AppendGuid("AAA", "BBB");
Console.WriteLine(result);
```
