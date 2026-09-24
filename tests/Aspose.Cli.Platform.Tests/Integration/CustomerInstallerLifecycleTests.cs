using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Updating;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Update replay, status reporting, uninstall and recovery decisions of the customer installer.</summary>
public sealed partial class CustomerInstallerPowerShellTests
{
    [Fact]
    public void Update_ReplaysTheChoicesRecordedByTheInstallation()
    {
        Requires.Windows();
        string install = Path.Combine(_root, "update-replay");
        string skills = Path.Combine(_root, "update-replay-skills");
        UserState before = CaptureUserState();
        _package.CopySkillsInstallation(install, skills);
        string choices = MarkerChoices(install);
        string stale = AddManagedStaleFile(Path.Combine(skills, "aspose-cli-cells"));

        // Only -Update: PATH, Skills and MCP choices come from the marker.
        PowerShellResult updated = RunInstaller(_package.Path, install, arguments: ["-Update"],
            skipPath: false, skipSkills: false, skipMcp: false);

        Assert.True(updated.ExitCode == 0, updated.StdErr + updated.StdOut);
        Assert.Equal(choices, MarkerChoices(install));
        Assert.False(File.Exists(stale));
        AssertBundledSkills(skills);
        Assert.DoesNotContain("MCP host", Flat(updated), StringComparison.Ordinal);
        Assert.DoesNotContain("user PATH", Flat(updated), StringComparison.Ordinal);
        Assert.Equal(before, CaptureUserState());
    }

    [Fact]
    public void Update_RequiresAnInstallationAndRejectsChoiceSwitches()
    {
        Requires.Windows();
        string install = Path.Combine(_root, "update-requirements");
        PowerShellResult missing = RunInstaller(_package.Path, install, arguments: ["-Update"],
            skipPath: false, skipSkills: false, skipMcp: false);
        Assert.NotEqual(0, missing.ExitCode);
        Assert.Contains("-Update requires one", Flat(missing), StringComparison.Ordinal);
        Assert.False(Directory.Exists(install));

        _package.CopyInstallation(install);
        string before = Snapshot(install);
        foreach (string[] conflict in new[] { new[] { "-SkipPath" }, new[] { "-SkillsRoot", Path.Combine(_root, "other") } })
        {
            PowerShellResult rejected = RunInstaller(_package.Path, install, arguments: ["-Update", .. conflict],
                skipPath: false, skipSkills: false, skipMcp: false);
            Assert.NotEqual(0, rejected.ExitCode);
            Assert.Contains("cannot be combined with " + conflict[0], Flat(rejected), StringComparison.Ordinal);
        }
        Assert.Equal(before, Snapshot(install));
    }

    [Fact]
    public void InstallDirectorySpellings_ShareOneTransaction()
    {
        Requires.Windows();
        string install = Path.Combine(_root, "one-spelling");
        // The helper derives the configuration directory from the install path's last segment.
        var configuration = new Dictionary<string, string?> { ["ASPOSE_CLI_CONFIG_DIR"] = Path.Combine(_root, ".config-one-spelling") };
        _package.CopyInstallation(install);
        string before = Snapshot(install);

        PowerShellResult interrupted = RunInstaller(_package.Path, install,
            new Dictionary<string, string?>(configuration) { ["ASPOSE_CLI_INSTALL_CRASH"] = "oldMoved" });
        Assert.Equal(97, interrupted.ExitCode);
        // The next run, with nobody to answer a prompt and the spelling with a trailing
        // separator, finds the same journal, recovers and completes its own installation.
        PowerShellResult recovered = RunInstaller(_package.Path, install + Path.DirectorySeparatorChar, configuration);
        Assert.True(recovered.ExitCode == 0, recovered.StdErr + recovered.StdOut);
        AssertInstall(install);
        Assert.Equal(before, Snapshot(install));
        Assert.Empty(Directory.GetFiles(_root, ".aspose-cli-transaction-*.json"));
        Assert.Empty(Directory.GetDirectories(_root, ".aspose-cli-backup-*"));

        PowerShellResult keys = RunInstallerFunctions(
            $"(Get-TransactionKey (Resolve-InstallRoot {PowerShellLiteral(install + "\\")})) -ceq (Get-TransactionKey (Resolve-InstallRoot {PowerShellLiteral(install)}))");
        Assert.Equal("True", keys.StdOut.Trim());
    }

    [Fact]
    public void StatusFile_ReportsEveryOutcomeToTheCli()
    {
        Requires.Windows();
        string install = Path.Combine(_root, "status-install");
        string status = Path.Combine(_root, "status-" + Guid.NewGuid().ToString("N") + ".json");
        _package.CopyInstallation(install);
        string before = Snapshot(install);

        PowerShellResult succeeded = RunInstaller(_package.Path, install, arguments: ["-Update", "-StatusPath", status],
            skipPath: false, skipSkills: false, skipMcp: false);
        Assert.True(succeeded.ExitCode == 0, succeeded.StdErr + succeeded.StdOut);
        Assert.Equal("succeeded", JsonNode.Parse(File.ReadAllText(status))!["state"]!.GetValue<string>());
        Assert.Null(UpdateStatus.ReadWarning(status));
        // The same build replaying the same choices publishes an identical tree...
        Assert.Equal(before, Snapshot(install));

        // ...so the rollback finds the original already in place and removes the verified
        // duplicate backup.
        PowerShellResult failed = RunInstaller(_package.Path, install,
            new Dictionary<string, string?> { ["ASPOSE_CLI_INSTALL_FAULT"] = "newPublished" },
            arguments: ["-Update", "-StatusPath", status], skipPath: false, skipSkills: false, skipMcp: false);
        Assert.NotEqual(0, failed.ExitCode);
        Assert.Equal(before, Snapshot(install));
        Assert.Empty(Directory.GetFiles(_root, ".aspose-cli-transaction-*.json"));
        Assert.Empty(Directory.GetDirectories(_root, ".aspose-cli-backup-*"));
        JsonNode record = JsonNode.Parse(File.ReadAllText(status))!;
        Assert.Equal("failed", record["state"]!.GetValue<string>());
        string log = record["log"]!.GetValue<string>();
        Assert.Equal(Path.ChangeExtension(status, ".log"), log);
        Assert.Contains("Injected installer failure at 'newPublished'", File.ReadAllText(log), StringComparison.Ordinal);
        Warning warning = UpdateStatus.ReadWarning(status)!;
        Assert.Equal(UpdateStatus.FailedWarningCode, warning.Code);
        Assert.Contains("Injected installer failure", warning.Message, StringComparison.Ordinal);

        PowerShellResult crashed = RunInstaller(_package.Path, install,
            new Dictionary<string, string?> { ["ASPOSE_CLI_INSTALL_CRASH"] = "oldMoved" },
            arguments: ["-Update", "-StatusPath", status], skipPath: false, skipSkills: false, skipMcp: false);
        Assert.Equal(97, crashed.ExitCode);
        Assert.Equal("running", JsonNode.Parse(File.ReadAllText(status))!["state"]!.GetValue<string>());
        Assert.Contains("stopped before", UpdateStatus.ReadWarning(status)!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Uninstall_RemovesTheInstallationAndOnlyPristineOwnedSkills()
    {
        Requires.Windows();
        string install = Path.Combine(_root, "uninstall");
        string skills = Path.Combine(_root, "uninstall-skills");
        UserState before = CaptureUserState();
        _package.CopySkillsInstallation(install, skills);
        string customized = Path.Combine(skills, "aspose-cli-pdf", "customer-notes.md");
        File.WriteAllText(customized, "preserve me", Encoding.UTF8);
        string unrelated = Path.Combine(skills, "customer-skill", "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(unrelated)!);
        File.WriteAllText(unrelated, "preserve me", Encoding.UTF8);

        PowerShellResult removed = RunUninstaller(install);

        Assert.True(removed.ExitCode == 0, removed.StdErr + removed.StdOut);
        Assert.False(Directory.Exists(install));
        Assert.Equal(
            new[] { "aspose-cli-pdf", "customer-skill" },
            Directory.GetDirectories(skills).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal("preserve me", File.ReadAllText(customized, Encoding.UTF8));
        Assert.Contains("Kept customized or unmanaged Skill 'aspose-cli-pdf'", Flat(removed), StringComparison.Ordinal);
        AssertNoTransactionLeftovers();
        Assert.Equal(before, CaptureUserState());

        PowerShellResult again = RunUninstaller(install);
        Assert.True(again.ExitCode == 0, again.StdErr + again.StdOut);
        Assert.Contains("No Aspose CLI installation exists", Flat(again), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("prepared")]
    [InlineData("oldMoved")]
    [InlineData("skillPrepared")]
    [InlineData("skillUpdated")]
    [InlineData("committedCleanup")]
    public void Uninstall_FailureRestoresTheInstallationAndSkills(string phase)
    {
        Requires.Windows();
        string install = Path.Combine(_root, "uninstall-fault-" + phase);
        string skills = Path.Combine(_root, "uninstall-fault-skills-" + phase);
        _package.CopySkillsInstallation(install, skills);
        string beforeInstall = Snapshot(install);
        string beforeSkills = Snapshot(skills);

        PowerShellResult result = RunUninstaller(install,
            new Dictionary<string, string?> { ["ASPOSE_CLI_INSTALL_FAULT"] = phase });

        if (phase == "committedCleanup")
        {
            // After the commit point nothing is rolled back. The committed journal and the
            // verified backups stay exactly as a process killed at the commit point leaves
            // them, and the next run finishes them.
            Assert.True(result.ExitCode == 0, result.StdErr + result.StdOut);
            Assert.Contains("remains pending", Flat(result), StringComparison.Ordinal);
            Assert.False(Directory.Exists(install));
            Assert.Single(Directory.GetFiles(_root, ".aspose-cli-transaction-*.json"));
            PowerShellResult finished = RunUninstaller(install);
            Assert.True(finished.ExitCode == 0, finished.StdErr + finished.StdOut);
            Assert.False(Directory.Exists(install));
            Assert.Empty(Directory.GetDirectories(skills));
            AssertNoTransactionLeftovers();
            return;
        }
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(beforeInstall, Snapshot(install));
        Assert.Equal(beforeSkills, Snapshot(skills));
        AssertNoTransactionLeftovers();
    }

    [Fact]
    public void Uninstall_InterruptedRunIsCompletedByTheNextRun()
    {
        Requires.Windows();
        string install = Path.Combine(_root, "uninstall-crash");
        string skills = Path.Combine(_root, "uninstall-crash-skills");
        _package.CopySkillsInstallation(install, skills);

        // Killed with the installation and one Skill moved to their backups: the next run
        // restores both from the journal, then removes them again as its own transaction.
        PowerShellResult interrupted = RunUninstaller(install,
            new Dictionary<string, string?> { ["ASPOSE_CLI_INSTALL_CRASH"] = "skillUpdated" });
        Assert.Equal(97, interrupted.ExitCode);

        PowerShellResult finished = RunUninstaller(install);
        Assert.True(finished.ExitCode == 0, finished.StdErr + finished.StdOut);
        Assert.False(Directory.Exists(install));
        Assert.Empty(Directory.GetDirectories(skills));
        AssertNoTransactionLeftovers();
    }

    [Fact]
    public void Install_WhileAnotherProcessHoldsTheDirectoryLock_ChangesNothingUntilItIsReleased()
    {
        Requires.Windows();
        string install = Path.Combine(_root, "contended");
        string key = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(install.ToUpperInvariant())))[..16];
        string lockPath = Path.Combine(_root, $".aspose-cli-install-{key}.lock");

        PowerShellResult refused;
        using (new FileStream(lockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            refused = RunInstaller(_package.Path, install);
        }
        File.Delete(lockPath);

        Assert.NotEqual(0, refused.ExitCode);
        Assert.Contains($"Another installation is using '{install}'", Flat(refused), StringComparison.Ordinal);
        Assert.False(Directory.Exists(install));
        AssertNoTransactionLeftovers();
        Assert.Equal(0, RunInstaller(_package.Path, install).ExitCode);
        Assert.True(File.Exists(Path.Combine(install, "aspose-cli.exe")));
        AssertNoTransactionLeftovers();
    }

    [Fact]
    public void Uninstall_RemovesConfigurationOnlyOnRequestAndOnlyWhenOwned()
    {
        Requires.Windows();
        string install = Path.Combine(_root, "uninstall-config");
        string configuration = Path.Combine(_root, ".config-uninstall-config");
        _package.CopyInstallation(install);
        Directory.CreateDirectory(configuration);
        string license = Path.Combine(configuration, "licenses", "Aspose.Total.lic");
        Directory.CreateDirectory(Path.GetDirectoryName(license)!);
        File.WriteAllText(license, "license", Encoding.UTF8);

        Assert.Equal(0, RunUninstaller(install).ExitCode);
        Assert.True(File.Exists(license));

        Assert.Equal(0, RunInstaller(_package.Path, install).ExitCode);
        // The CLI claims its configuration directory on first use; take that claim away.
        string ownerMarker = Path.Combine(configuration, ".aspose-cli-config.json");
        File.Delete(ownerMarker);
        PowerShellResult unowned = RunUninstaller(install, arguments: ["-RemoveConfiguration"]);
        Assert.NotEqual(0, unowned.ExitCode);
        Assert.Contains("is not marked as owned", Flat(unowned), StringComparison.Ordinal);
        Assert.False(Directory.Exists(install));
        Assert.True(File.Exists(license));

        File.WriteAllText(ownerMarker, "{\"schemaVersion\":1,\"productId\":\"aspose-cli\"}", new UTF8Encoding(false));
        PowerShellResult owned = RunUninstaller(install, arguments: ["-RemoveConfiguration"]);
        Assert.True(owned.ExitCode == 0, owned.StdErr + owned.StdOut);
        Assert.False(Directory.Exists(configuration));
    }

    [Theory]
    [InlineData(@"C:\A", @"C:\A;{root}", @"C:\A;{root};C:\B", @"C:\A;C:\B")]
    [InlineData(@"C:\A;{root}", @"C:\A", @"C:\A;C:\B", @"C:\A;C:\B;{root}")]
    [InlineData(@"C:\A", @"C:\A;{root}", @"C:\X", @"C:\X")]
    public void PathRecovery_KeepsAnExternalChangeAndRestoresOnlyTheInstallEntry(
        string original, string applied, string external, string expected)
    {
        Requires.Windows();
        const string root = @"C:\Isolated\Aspose CLI";
        static string Expand(string value) => value.Replace("{root}", root, StringComparison.Ordinal);
        PowerShellResult result = RunInstallerFunctions(
            "$script:testPath = " + PowerShellLiteral(Expand(external)) + "; "
            + "function Get-UserPath { $script:testPath }; function Set-UserPath { param([AllowNull()]$Value) $script:testPath = $Value }; "
            + "$key = '0123456789abcdef'; "
            + "$journal = [pscustomobject]@{ pathState = 'applied'; originalPath = (Protect-PathValue " + PowerShellLiteral(Expand(original)) + " $key); "
            + "originalPathNull = $false; appliedPathSha256 = (Get-StringSha256 " + PowerShellLiteral(Expand(applied)) + ") }; "
            + "Restore-TransactionPath $journal $key " + PowerShellLiteral(root) + " 3>&1 | Out-Null; $script:testPath");
        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal(Expand(expected), result.StdOut.Trim());
    }

    [Fact]
    public void PathComposition_UninstallRemovesOnlyTheInstallEntry()
    {
        Requires.Windows();
        string installRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aspose", "CLI");
        string current = string.Join(';', @"%USERPROFILE%\AppData\Local\Microsoft\WindowsApps", installRoot + "\\", @"C:\Tools", @"%LOCALAPPDATA%\Aspose\CLI");
        PowerShellResult result = RunInstallerFunctions(
            $"Get-UserPathWithoutInstallRoot {PowerShellLiteral(current)} {PowerShellLiteral(installRoot)}");
        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal(@"%USERPROFILE%\AppData\Local\Microsoft\WindowsApps;C:\Tools", result.StdOut.Trim());
    }

    [Theory]
    [InlineData("1.0.0", "a", "older")]
    [InlineData("1.1.0-rc.1", "a", "older")]
    [InlineData("1.1.0", "b", "same")]
    [InlineData("1.1.0+build.2", "a", "same")]
    [InlineData("1.1.0", "a", "ok")]
    [InlineData("1.1.1", "b", "ok")]
    [InlineData("1.2.0-rc.1", "b", "ok")]
    public void VersionRule_RefusesDowngradesAndSameVersionRebuilds(string version, string revision, string expected)
    {
        Requires.Windows();
        PowerShellResult result = RunInstallerFunctions(
            "$installed = [pscustomobject]@{ CliVersion = '1.1.0'; SourceRevision = ('a' * 40) }; "
            + $"try {{ Assert-InstallationUpgrade $installed {PowerShellLiteral(version)} ({PowerShellLiteral(revision)} * 40); 'ok' }} "
            + "catch { if ($_.Exception.Message -match 'older') { 'older' } elseif ($_.Exception.Message -match 'same version precedence') { 'same' } else { throw } }");
        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal(expected, result.StdOut.Trim());
    }

    [Theory]
    [InlineData("powershell")]
    [InlineData("pwsh")]
    public void ClaudeVerification_ReadsLargeConfigurationsWithCaseDistinctKeys(string shell)
    {
        Requires.Windows();
        string executable = shell == "pwsh" ? ToolPath.Require("pwsh") : "powershell.exe";
        string configDirectory = Path.Combine(_root, "claude-large-" + shell);
        Directory.CreateDirectory(configDirectory);
        const string installation = @"C:\isolated path\aspose-cli.exe";
        var configuration = new JsonObject
        {
            ["projects"] = new JsonObject { ["history"] = new string('x', 3 * 1024 * 1024) },
            ["mcpServers"] = new JsonObject
            {
                ["aspose-cli"] = new JsonObject { ["type"] = "stdio", ["command"] = installation, ["args"] = new JsonArray("mcp", "serve") },
                ["ASPOSE-CLI"] = new JsonObject { ["type"] = "stdio", ["command"] = "other.exe", ["args"] = new JsonArray() },
            },
            ["MCPSERVERS"] = new JsonObject(),
        };
        File.WriteAllText(Path.Combine(configDirectory, ".claude.json"), configuration.ToJsonString(), new UTF8Encoding(false));
        string command = $". {PowerShellLiteral(Path.Combine(RepositoryPaths.Root, "install.ps1"))}; "
            + $"$env:CLAUDE_CONFIG_DIR = {PowerShellLiteral(configDirectory)}; "
            + "function Invoke-OfficialMcp { param([string] $Executable, [string[]] $Arguments, [hashtable] $Environment, [string] $WorkingDirectory) "
            + "[pscustomobject]@{ ExitCode = 0; StdErr = ''; StdOut = \"aspose-cli:`n  Scope: User config (available in all your projects)`n\" } }; "
            + $"$state = Get-McpRegistration 'claude' 'C:\\hosts\\claude.cmd' {PowerShellLiteral(installation)}; \"$($state.Exists)|$($state.Matches)\"";
        PowerShellResult result = RunExecutable(executable,
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command]);
        Assert.True(result.ExitCode == 0, result.StdErr + result.StdOut);
        Assert.Equal("True|True", result.StdOut.Trim());
    }

    [Fact]
    public void McpRegistration_RollsBackOrRecordsAnAddThatCannotBeVerified()
    {
        Requires.Windows();
        PowerShellResult result = RunInstallerFunctions("""
            $script:entries = @{}
            function Get-Command { [CmdletBinding()] param([string] $Name, [string] $CommandType) if ($Name -in @('codex','opencode')) { [pscustomobject]@{ Source = "C:\hosts\$Name.cmd" } } }
            function Get-McpRegistration { param([string] $HostName, [string] $Executable, [string] $InstallExecutable)
                [pscustomobject]@{ Exists = $script:entries.ContainsKey($HostName); Matches = $false } }
            function Invoke-OfficialMcp { param([string] $Executable, [string[]] $Arguments, [hashtable] $Environment, [string] $WorkingDirectory)
                $hostName = [IO.Path]::GetFileNameWithoutExtension($Executable)
                if ($Arguments[1] -ceq 'add') { $script:entries[$hostName] = $true }
                elseif ($Arguments[1] -ceq 'remove') { $script:entries.Remove($hostName) }
                [pscustomobject]@{ ExitCode = 0; StdOut = ''; StdErr = '' } }
            $registered = @(Register-OwnedMcp 'C:\isolated\aspose-cli.exe' @() 3>&1 | ForEach-Object { if ($_ -is [Management.Automation.WarningRecord]) { Write-Host "WARNING: $_" } else { $_ } })
            "registered=$($registered -join ',');left=$(@($script:entries.Keys | Sort-Object) -join ',')"
            """);
        Assert.True(result.ExitCode == 0, result.StdErr + result.StdOut);
        // Codex can remove what it added; OpenCode cannot, so its unverified entry stays owned.
        Assert.Contains("registered=opencode;left=opencode", Flat(result), StringComparison.Ordinal);
        Assert.Contains("'codex' accepted the registration, but it could not be verified, so it was removed again", Flat(result), StringComparison.Ordinal);
        Assert.Contains("recorded as installer-owned", Flat(result), StringComparison.Ordinal);
    }

    [Fact]
    public void McpUnregistration_RemovesOnlyOwnedEntriesThatStillPointAtTheInstallation()
    {
        Requires.Windows();
        PowerShellResult result = RunInstallerFunctions("""
            $script:entries = @{ codex = $true; claude = $false; opencode = $true }
            $script:removed = @()
            function Get-Command { [CmdletBinding()] param([string] $Name, [string] $CommandType) [pscustomobject]@{ Source = "C:\hosts\$Name.cmd" } }
            function Get-McpRegistration { param([string] $HostName, [string] $Executable, [string] $InstallExecutable)
                [pscustomobject]@{ Exists = $script:entries.ContainsKey($HostName); Matches = [bool]$script:entries[$HostName] } }
            function Invoke-OfficialMcp { param([string] $Executable, [string[]] $Arguments, [hashtable] $Environment, [string] $WorkingDirectory)
                $hostName = [IO.Path]::GetFileNameWithoutExtension($Executable)
                if ($Arguments[1] -cne 'remove') { throw 'Unexpected host command.' }
                $script:removed += $hostName; $script:entries.Remove($hostName)
                [pscustomobject]@{ ExitCode = 0; StdOut = ''; StdErr = '' } }
            Unregister-OwnedMcp 'C:\isolated\aspose-cli.exe' @('codex','claude','opencode') 3>&1 | ForEach-Object { "$_" }
            "removed=$($script:removed -join ',');left=$(@($script:entries.Keys | Sort-Object) -join ',')"
            """);
        Assert.True(result.ExitCode == 0, result.StdErr + result.StdOut);
        Assert.Contains("removed=codex;left=claude,opencode", Flat(result), StringComparison.Ordinal);
        Assert.Contains("no longer points at this installation; it was preserved", Flat(result), StringComparison.Ordinal);
        Assert.Contains("OpenCode global configuration", Flat(result), StringComparison.Ordinal);
    }

    private PowerShellResult RunUninstaller(
        string install,
        IReadOnlyDictionary<string, string?>? environment = null,
        IReadOnlyList<string>? arguments = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["DOTNET_EnableCrashReport"] = "0",
            ["ASPOSE_CLI_CONFIG_DIR"] = Path.Combine(Path.GetDirectoryName(install)!, ".config-" + Path.GetFileName(install)),
        };
        foreach ((string name, string? value) in environment ?? new Dictionary<string, string?>())
        {
            values[name] = value;
        }
        return RunExecutable("powershell.exe",
        [
            "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
            "-File", Path.Combine(RepositoryPaths.Root, "install.ps1"),
            "-InstallDirectory", install, "-Uninstall", "-DevelopmentPackage",
            .. arguments ?? [],
        ], values);
    }

    private static PowerShellResult RunInstallerFunctions(string script) =>
        RunExecutable("powershell.exe",
        [
            "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command",
            $". {PowerShellLiteral(Path.Combine(RepositoryPaths.Root, "install.ps1"))}; {script}",
        ]);

    // What every install, update and uninstall run does first under its locks, without the
    // package verification and the new transaction that follow.
    private static PowerShellResult RunPendingRecovery(string install) =>
        RunInstallerFunctions(
            $"$lock = Enter-InstallLock (Resolve-InstallRoot {PowerShellLiteral(install)}); "
            + "try { Invoke-PendingRecovery $lock } finally { Exit-InstallLock $lock }");

    private void AssertNoTransactionLeftovers()
    {
        foreach (string pattern in new[] { ".aspose-cli-transaction-*", ".aspose-cli-install-*.lock" })
        {
            Assert.Empty(Directory.GetFiles(_root, pattern));
        }
        foreach (string pattern in new[] { ".aspose-cli-backup-*", ".aspose-cli-stage-*" })
        {
            Assert.Empty(Directory.GetDirectories(_root, pattern));
        }
        Assert.Empty(Directory.GetDirectories(_root, ".aspose-skill-install-backup-*", SearchOption.AllDirectories));
    }

    // Windows PowerShell wraps redirected warnings and errors at the console width.
    private static string Flat(PowerShellResult result) =>
        (result.StdErr + result.StdOut).Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);

    private static string MarkerChoices(string install) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(install, ".aspose-cli-install.json")))!["choices"]!.ToJsonString();
}
