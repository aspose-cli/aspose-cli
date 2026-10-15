using System.Runtime.CompilerServices;
using Aspose.Cli.TestKit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// The invariants run their command lines in warm CLI workers (<see cref="CliWorkers"/>); set
/// <c>ASPOSE_CLI_TEST_WORKERS=0</c> to run each as a child process instead.
/// </summary>
internal static class CliWorkerMode
{
    [ModuleInitializer]
    internal static void Enable() => CliWorkers.Enable();
}
