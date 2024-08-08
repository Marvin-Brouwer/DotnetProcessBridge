using DotnetProcessBridge.Child.TestModels;

using FluentAssertions;
using System.Diagnostics;
using System.Reflection;

namespace DotnetProcessBridge.Tests
{
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

            var result = direct
                ? InvokeDirect(assemblyToTest, [server.ReadHandle, server.WriteHandle])
                : await InvokeProcess(assemblyToTest, [server.ReadHandle, server.WriteHandle]);

            result.Should().BeEquivalentTo("AAA acf70d64-60c9-4e8c-a716-99e831d26e78 BBB" + Environment.NewLine);
        }

        private static string InvokeDirect(Assembly assembly, params string[] arguments)
        {
            using var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);

            var resultTask = assembly.EntryPoint!.Invoke(assembly.EntryPoint, [arguments]);

            return stringWriter.ToString();
        }

        private static async Task<string> InvokeProcess(Assembly assembly, params string[] arguments)
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
                    RedirectStandardError = true,
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

    internal sealed class TestExample : IExample
    {
        public string AppendGuid(string prefix, string postfix)
        {
            return string.Join(" ", prefix, "acf70d64-60c9-4e8c-a716-99e831d26e78", postfix);
        }
    }
}