using System.Collections;
using System.Globalization;
using System.IO.Pipes;
using System.Reflection;
using System.Text;

#pragma warning disable CA1050 // The runtime finds a startup hook only as StartupHook in no namespace.

/// <summary>
/// The startup hook (<c>DOTNET_STARTUP_HOOKS</c>) that turns a started <c>aspose-cli</c> into a
/// warm worker of <see cref="Aspose.Cli.TestKit.CliWorkers"/>. A process started without the
/// worker's pipe variable, such as a child the CLI starts itself, runs its own command line. The
/// run's own startup hooks run before this one, and the hooks variable gets back their value, so
/// no process the worker starts inherits this hook and every one inherits the run's.
/// </summary>
public static class StartupHook
{
    public static void Initialize()
    {
        string? pipe = Environment.GetEnvironmentVariable(Aspose.Cli.TestKit.CliWorkerHost.PipeVariable);
        string? hooks = Environment.GetEnvironmentVariable(Aspose.Cli.TestKit.CliWorkerHost.HooksBeforeVariable);
        Environment.SetEnvironmentVariable(Aspose.Cli.TestKit.CliWorkerHost.HookVariable, hooks);
        Environment.SetEnvironmentVariable(Aspose.Cli.TestKit.CliWorkerHost.HooksBeforeVariable, null);
        Environment.SetEnvironmentVariable(Aspose.Cli.TestKit.CliWorkerHost.PipeVariable, null);
        if (pipe is not null)
        {
            Environment.Exit(Aspose.Cli.TestKit.CliWorkerHost.Serve(pipe));
        }
    }
}

#pragma warning restore CA1050

namespace Aspose.Cli.TestKit
{
    /// <summary>
    /// The loop a worker runs inside <c>aspose-cli</c>: it reads one command line at a time from
    /// the test process, runs the executable's own <c>Main</c> with the working directory, the
    /// environment, standard input, output and error and the culture a child process would start
    /// with, sends back the exit code and the bytes the command wrote, and restores the worker's
    /// own state before the next command. It uses only the base class library, since it runs in
    /// the CLI's process with the CLI's dependencies.
    /// </summary>
    internal static class CliWorkerHost
    {
        /// <summary>The variable that names the pipe of a worker.</summary>
        internal const string PipeVariable = "ASPOSE_CLI_TEST_WORKER_PIPE";

        /// <summary>The runtime's startup hook variable.</summary>
        internal const string HookVariable = "DOTNET_STARTUP_HOOKS";

        /// <summary>The startup hooks variable of the run, which the worker's hook puts back.</summary>
        internal const string HooksBeforeVariable = "ASPOSE_CLI_TEST_WORKER_HOOKS";

        /// <summary>The first field of every request and response.</summary>
        internal const int Magic = 0x41435752;

        /// <summary>The encoding of the protocol's strings and of the console streams a command writes.</summary>
        internal static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

        internal static int Serve(string pipeName)
        {
            MethodInfo entry = Assembly.GetEntryAssembly()?.EntryPoint
                ?? throw new InvalidOperationException("The worker has no entry point to run.");
            var main = entry.CreateDelegate<Func<string[], int>>();
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            pipe.Connect(60_000);
            using var reader = new BinaryReader(pipe, Utf8, leaveOpen: true);
            using var writer = new BinaryWriter(pipe, Utf8, leaveOpen: true);

            string home = Directory.GetCurrentDirectory();
            Dictionary<string, string> baseline = Variables();
            TextWriter output = Console.Out;
            TextWriter error = Console.Error;
            TextReader input = Console.In;
            CultureInfo culture = CultureInfo.CurrentCulture;
            CultureInfo uiCulture = CultureInfo.CurrentUICulture;
            CultureInfo? threadCulture = CultureInfo.DefaultThreadCurrentCulture;
            CultureInfo? threadUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
            ExecutionContext clean = ExecutionContext.Capture()
                ?? throw new InvalidOperationException("The worker's execution context cannot be captured.");
            while (true)
            {
                int magic;
                try
                {
                    magic = reader.ReadInt32();
                }
                catch (Exception exception) when (exception is EndOfStreamException or IOException)
                {
                    return 0; // The test process closed the pipe or ended.
                }
                if (magic != Magic)
                {
                    return 3;
                }
                string directory = reader.ReadString();
                string[] args = new string[reader.ReadInt32()];
                for (int index = 0; index < args.Length; index++)
                {
                    args[index] = reader.ReadString();
                }
                var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int count = reader.ReadInt32(); count > 0; count--)
                {
                    string name = reader.ReadString();
                    variables[name] = reader.ReadString();
                }

                using var stdout = new MemoryStream();
                using var stderr = new MemoryStream();
                int exitCode = 0;
                Replace(variables);
                Directory.SetCurrentDirectory(directory);
                Console.SetOut(ConsoleWriter(stdout));
                Console.SetError(ConsoleWriter(stderr));
                // A child process the tests start reads an empty, closed standard input.
                Console.SetIn(TextReader.Synchronized(new StringReader(string.Empty)));
                try
                {
                    // A fresh copy of the worker's first context per command, so no async-local
                    // value a command sets reaches the next one.
                    ExecutionContext.Run(clean.CreateCopy(), _ => exitCode = main(args), null);
                }
                finally
                {
                    Console.Out.Flush();
                    Console.Error.Flush();
                    Console.SetOut(output);
                    Console.SetError(error);
                    Console.SetIn(input);
                    CultureInfo.DefaultThreadCurrentCulture = threadCulture;
                    CultureInfo.DefaultThreadCurrentUICulture = threadUiCulture;
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = uiCulture;
                    Directory.SetCurrentDirectory(home);
                    Replace(baseline);
                }

                writer.Write(Magic);
                writer.Write(exitCode);
                WriteBytes(writer, stdout);
                WriteBytes(writer, stderr);
                writer.Flush();
            }
        }

        /// <summary>A writer like the console's for a redirected stream: UTF-8 without a preamble, flushed on every write.</summary>
        private static TextWriter ConsoleWriter(Stream stream) =>
            TextWriter.Synchronized(new StreamWriter(stream, Utf8, bufferSize: 256, leaveOpen: true) { AutoFlush = true });

        private static void WriteBytes(BinaryWriter writer, MemoryStream stream)
        {
            writer.Write(checked((int)stream.Length));
            writer.Write(stream.GetBuffer(), 0, (int)stream.Length);
        }

        private static Dictionary<string, string> Variables()
        {
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                variables[(string)entry.Key] = (string?)entry.Value ?? string.Empty;
            }
            return variables;
        }

        /// <summary>Makes the process environment exactly <paramref name="variables"/>.</summary>
        private static void Replace(Dictionary<string, string> variables)
        {
            foreach (string name in Variables().Keys.Where(name => !variables.ContainsKey(name)))
            {
                Environment.SetEnvironmentVariable(name, null);
            }
            foreach ((string name, string value) in variables)
            {
                if (Environment.GetEnvironmentVariable(name) != value)
                {
                    Environment.SetEnvironmentVariable(name, value);
                }
            }
        }
    }
}
