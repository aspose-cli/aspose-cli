using System.Runtime.InteropServices;
using System.Text.Json;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Host.Updating;

/// <summary>Owns the transfer from a terminable preparation worker to an independent installer.</summary>
internal static class UpdateInstaller
{
    internal const string PreparationCommand = "_prepare";

    internal static UpdateResult Install(HostContext host, CommandContext context, string feed)
    {
        if (!OperatingSystem.IsWindows() || RuntimeInformation.RuntimeIdentifier != "win-x64")
        {
            throw CliErrors.OptionInvalid("update install", "the customer updater currently supports Windows x64 only",
                "Run the win-x64 distribution on Windows, or use update check for feed verification.");
        }
        string powerShell = WindowsPowerShell.TryResolve()
            ?? throw ReleaseErrors.VerificationFailed("the trusted Windows PowerShell executable is unavailable");
        string statusPath = UpdateStatus.PathFor(AppContext.BaseDirectory);
        // Read the outcome of the previous run before this run replaces it.
        Warning? previous = UpdateStatus.ReadWarning(statusPath);
        IReadOnlyList<Warning>? warnings = previous is null ? null : [previous];
        string parent = PrivateUserStorage.EnsureDirectory(Path.Combine(PrivateUserStorage.TemporaryRoot(), "updates"));
        // install.ps1 accepts only a <distribution id>-update-* cleanup root below the temporary directory.
        string target = Path.Combine(parent, DistributionInfo.Id + "-update-" + Guid.NewGuid().ToString("N"));
        string source = !Path.IsPathRooted(feed) && Uri.TryCreate(feed, UriKind.Absolute, out _)
            ? feed : Path.GetFullPath(feed, context.Paths.BaseDirectory);
        string[] args = ["update", PreparationCommand, source, target, "--output", "json", "--quiet"];
        ParsedInvocation invocation = host.Parser.Parse(args);
        bool ownsPackage = false;
        bool handedOff = false;
        try
        {
            InvocationProcessResult process = TimeoutWorkerSupervisor.ExecuteAsync(host, args, invocation, context.Deadline,
                () => SelfProcessLauncher.CreateBackground("update", "Run from the installed aspose-cli executable."))
                .GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                if (process.ExitCode == 130) { throw new OperationCanceledException(); }
                throw LocalServiceChildError.TryRead(process.Stderr, process.ExitCode)
                    ?? ReleaseErrors.VerificationFailed("the update preparation worker failed");
            }
            ownsPackage = Directory.Exists(target);
            UpdateResult prepared = JsonSerializer.Deserialize(process.Stdout, SdkJsonContext.Default.UpdateResult)
                ?? throw ReleaseErrors.VerificationFailed("the preparation worker returned no result");
            if (prepared.Edition != DistributionInfo.Edition || prepared.CurrentVersion != VersionInfo.ArtifactVersion
                || prepared.Feed != source || prepared.ProcessId is not null
                || prepared.Status is not ("available" or "up-to-date")
                || (prepared.Status == "available") != ownsPackage)
            { throw ReleaseErrors.VerificationFailed("the preparation worker returned an inconsistent result"); }
            context.Deadline.ThrowIfExpired("update-handoff");
            if (!ownsPackage) { return prepared with { Feed = feed, Warnings = warnings }; }
            PrivateUserStorage.ValidateDirectory(target);
            int pid = UpdateClient.HandoffToInstaller(powerShell, target, AppContext.BaseDirectory, statusPath, context.Deadline);
            handedOff = true;
            // Starting the independent installer is the commit point. It waits for this parent PID
            // before it records its own progress, so this pending record cannot overwrite it.
            try { UpdateStatus.WritePending(statusPath, pid, VersionInfo.ArtifactVersion, prepared.AvailableVersion); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            return prepared with { Status = "pending", Feed = feed, ProcessId = pid, Warnings = warnings };
        }
        finally
        {
            if (ownsPackage && !handedOff) { PrivateUserStorage.TryDeleteTree(target); }
        }
    }
}
