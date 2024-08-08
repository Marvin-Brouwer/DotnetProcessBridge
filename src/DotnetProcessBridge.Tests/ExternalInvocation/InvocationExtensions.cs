using System.Diagnostics;
using System.Reflection;

namespace DotnetProcessBridge.Tests.ExternalInvocation;

internal static class InvocationExtensions
{
	/// <summary>
	/// Invoke the EntryPoint of the assembly, pretending to invoke a process.
	/// </summary>
	internal static string InvokeDirect(this Assembly assembly, params string[] arguments)
	{
		using var stringWriter = new StringWriter();
		Console.SetOut(stringWriter);

		var resultTask = assembly.EntryPoint!.Invoke(assembly.EntryPoint, [arguments]);

		return stringWriter.ToString();
	}

	/// <summary>
	/// Invoke a process associated with this assembly
	/// </summary>
	/// <param name="assembly"></param>
	/// <param name="arguments"></param>
	/// <returns></returns>
	internal static async Task<string> InvokeProcess(this Assembly assembly, params string[] arguments)
	{
		var tcs = new TaskCompletionSource<string>();

		var process = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = assembly.Location.Replace(".dll", ".exe"),
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			}
		};


		foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);

		await Task.Run(async () =>
		{
			try
			{
				process.Start();
				process.WaitForExit();

				if (process.ExitCode != 0)
					tcs.SetException(new Exception(await process.StandardError.ReadToEndAsync()));
				else
					tcs.SetResult(await process.StandardOutput.ReadToEndAsync());
			}
			catch (Exception ex)
			{
				tcs.SetException(ex);
			}
		});

		return await tcs.Task;
	}
}
