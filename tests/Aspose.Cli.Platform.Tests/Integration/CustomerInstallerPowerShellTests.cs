using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

[CollectionDefinition("Customer installer user state", DisableParallelization = true)]
public sealed class CustomerInstallerUserStateCollection;

/// <summary>Real Windows PowerShell black-box coverage for customer installation ownership and recovery.</summary>
[Category(TestCategory.Installer)]
[Collection("Customer installer user state")]
public sealed partial class CustomerInstallerPowerShellTests : IDisposable, IClassFixture<CustomerInstallerPackageFixture>
{
    private readonly string _root = Directory.CreateTempSubdirectory("aspose-installer-tests-").FullName;
    private readonly CustomerInstallerPackageFixture _package;

    public CustomerInstallerPowerShellTests(CustomerInstallerPackageFixture package) =>
        _package = package;

    public void Dispose()
    {
        DeleteDirectoryWithRetry(_root);
    }

    [Theory]
    [InlineData(0, false, false, "exact")]
    [InlineData(3, false, false, "exact")]
    [InlineData(3, true, false, "exact")]
    [InlineData(3, true, true, "exact")]
    [InlineData(3, true, true, "command")]
    [InlineData(3, true, true, "args")]
    [InlineData(3, true, true, "joined-args")]
    [InlineData(3, true, true, "transport")]
    public void McpRegistration_SelectsOneExecutableAndPreservesOwnership(int availableHosts, bool existing, bool owned, string variation)
    {
        Requires.Windows();
        string installer = Path.Combine(RepositoryPaths.Root, "install.ps1");
        string command = $$"""
            $ErrorActionPreference = 'Stop'
            . {{PowerShellLiteral(installer)}}
            $available = @(@('codex','claude','opencode') | Select-Object -First {{availableHosts}})
            $script:registrations = @{}
            $script:adds = 0
            $installation = 'C:\isolated path\aspose-cli.exe'
            $variation = '{{variation}}'
            $configuredCommand = if ($variation -ceq 'command') { $installation + '.other' } else { $installation }
            $configuredArgs = @(if ($variation -ceq 'args') { 'mcp'; 'other' } elseif ($variation -ceq 'joined-args') { 'mcp serve' } else { 'mcp'; 'serve' })
            $configuredType = if ($variation -ceq 'transport') { 'http' } else { 'stdio' }
            $env:CLAUDE_CONFIG_DIR = {{PowerShellLiteral(Path.Combine(_root, "claude-config"))}}
            [IO.Directory]::CreateDirectory($env:CLAUDE_CONFIG_DIR) | Out-Null
            if (${{existing.ToString().ToLowerInvariant()}}) {
                foreach ($hostName in $available) { $script:registrations[$hostName] = $true }
            }
            function Get-Command {
                [CmdletBinding()] param([string] $Name, [string] $CommandType)
                if ($Name -in $available) {
                    [pscustomobject]@{ Source = "C:\hosts\$Name.cmd" }
                    [pscustomobject]@{ Source = 'C:\hosts\must-not-run.exe' }
                }
            }
            function Invoke-OfficialMcp {
                param([string] $Executable, [string[]] $Arguments, [hashtable] $Environment, [string] $WorkingDirectory)
                $hostName = [IO.Path]::GetFileNameWithoutExtension($Executable)
                if ($hostName -notin $available) { throw 'Unexpected executable selection.' }
                $joined = $Arguments -join '|'
                if ($hostName -ceq 'opencode' -and $joined -ceq 'debug|config') {
                    if ($Environment.OPENCODE_DISABLE_PROJECT_CONFIG -cne 'true') { throw 'Project configuration was not excluded.' }
                    $mcp = @{}
                    if ($script:registrations.ContainsKey($hostName)) { $mcp['aspose-cli'] = @{ type=$(if ($variation -ceq 'transport') {'remote'} else {'local'}); command=(@($configuredCommand)+$configuredArgs) } }
                    return [pscustomobject]@{ ExitCode=0; StdOut=(@{ mcp=$mcp; provider=@{ secret='synthetic-provider-secret' } } | ConvertTo-Json -Depth 5); StdErr='' }
                }
                $entry = @{ type=$configuredType; command=$configuredCommand; args=$configuredArgs }
                if ($hostName -ceq 'codex' -and $joined -ceq 'mcp|get|aspose-cli|--json') {
                    return [pscustomobject]@{ ExitCode=$(if ($script:registrations.ContainsKey($hostName)) {0} else {1}); StdOut=(@{ transport=$entry } | ConvertTo-Json -Depth 5); StdErr='' }
                }
                if ($hostName -ceq 'claude' -and $joined -ceq 'mcp|get|aspose-cli') {
                    [IO.File]::WriteAllText((Join-Path $env:CLAUDE_CONFIG_DIR '.claude.json'), (@{ mcpServers=@{ 'aspose-cli'=$entry }; secret='synthetic-provider-secret' } | ConvertTo-Json -Depth 5))
                    return [pscustomobject]@{ ExitCode=$(if ($script:registrations.ContainsKey($hostName)) {0} else {1}); StdOut="aspose-cli:`n  Scope: User config (available in all your projects)`n  Type: $configuredType`n  Command: $configuredCommand`n  Args: $($configuredArgs -join ' ')"; StdErr='' }
                }
                $expected = @('mcp','add','aspose-cli')
                if ($hostName -ceq 'claude') { $expected += @('--scope','user') }
                $expected += @('--',$installation,'mcp','serve')
                if ($joined -cne ($expected -join '|')) { throw 'Unsupported host command.' }
                if ($script:registrations.ContainsKey($hostName)) { throw 'Existing registration was overwritten.' }
                $script:registrations[$hostName] = $true
                $script:adds++
                return [pscustomobject]@{ ExitCode=0; StdOut=''; StdErr='' }
            }
            $previous = @(if (${{owned.ToString().ToLowerInvariant()}}) { $available })
            $registered = @(Register-OwnedMcp $installation $previous)
            $expected = if (${{existing.ToString().ToLowerInvariant()}} -and (-not ${{owned.ToString().ToLowerInvariant()}} -or $variation -cne 'exact')) {0} else { {{availableHosts}} }
            if ($registered.Count -ne $expected) { throw 'Unexpected registration count.' }
            $again = @(Register-OwnedMcp $installation $registered)
            if ($again.Count -ne $expected) { throw 'Registration was not idempotent.' }
            $expectedAdds = if (${{existing.ToString().ToLowerInvariant()}}) {0} else { {{availableHosts}} }
            if ($script:adds -ne $expectedAdds) { throw 'Unexpected registration writes.' }
            Write-Output 'MCP contract passed'
            """;
        PowerShellResult result = RunExecutable("powershell.exe",
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command]);
        Assert.True(result.ExitCode == 0, result.StdErr + result.StdOut);
        Assert.Contains("MCP contract passed", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-provider-secret", result.StdOut + result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void OfficialMcpCommandShim_PreservesArgumentsWithoutShellExpansion()
    {
        Requires.Windows();
        string receiver = Path.Combine(_root, "receive.ps1");
        File.WriteAllText(receiver, "ConvertTo-Json -InputObject @($args) -Compress", new UTF8Encoding(false));
        string powerShell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell/v1.0/powershell.exe");
        string shim = Path.Combine(_root, "host with spaces.cmd");
        File.WriteAllText(shim, $"@echo off\r\n\"{powerShell}\" -NoProfile -NonInteractive -File \"{receiver}\" %*\r\n", Encoding.ASCII);
        string installer = Path.Combine(RepositoryPaths.Root, "install.ps1");
        string[] arguments = ["mcp", @"C:\literal %USERNAME% & ! ^ ( )\aspose-cli.exe", "A&B", "mcp serve"];
        string command = $". {PowerShellLiteral(installer)}; $result = Invoke-OfficialMcp {PowerShellLiteral(shim)} @({string.Join(',', arguments.Select(PowerShellLiteral))}); if ($result.ExitCode -ne 0) {{ throw 'Shim failed.' }}; $result.StdOut";
        PowerShellResult result = RunExecutable(powerShell,
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command]);
        Assert.True(result.ExitCode == 0, result.StdOut + result.StdErr);
        Assert.Equal(arguments, JsonSerializer.Deserialize<string[]>(result.StdOut));
    }

    [Fact]
    public void CrashAfterOptionalMcpMetadataDoesNotInvalidateCommittedRecovery()
    {
        Requires.Windows();
        string install = Path.Combine(_root, "mcp-metadata-recovery");
        _package.CopyInstallation(install);
        RewriteMarker(install, marker => marker["mcpRegistrations"] = new JsonArray("codex"));
        string emptyPath = Path.Combine(_root, "mcp-empty-path");
        Directory.CreateDirectory(emptyPath);
        PowerShellResult interrupted = RunInstaller(_package.Path, install,
            new Dictionary<string, string?> { ["Path"] = emptyPath, ["ASPOSE_CLI_INSTALL_CRASH"] = "mcpMetadataUpdated" },
            skipMcp: false);
        Assert.True(interrupted.ExitCode == 97, interrupted.StdErr + interrupted.StdOut);
        PowerShellResult recovered = RunInstaller(_package.Path, install);
        Assert.True(recovered.ExitCode == 0, recovered.StdErr + recovered.StdOut);
        AssertInstall(install);
    }

    [Fact]
    public void CommittedCleanupFailureKeepsInstallValidAndDefersMcpMetadata()
    {
        Requires.Windows();
        string install = Path.Combine(_root, "pending-cleanup");
        _package.CopyInstallation(install);
        RewriteMarker(install, marker => marker["mcpRegistrations"] = new JsonArray("codex"));
        string markerPath = Path.Combine(install, ".aspose-cli-install.json");
        string emptyPath = Path.Combine(_root, "cleanup-empty-path");
        Directory.CreateDirectory(emptyPath);
        PowerShellResult committed = RunInstaller(_package.Path, install,
            new Dictionary<string, string?> { ["Path"] = emptyPath, ["ASPOSE_CLI_INSTALL_FAULT"] = "committedCleanup" }, skipMcp: false);
        Assert.True(committed.ExitCode == 0, committed.StdErr + committed.StdOut);
        Assert.Contains("cleanup remains pending", committed.StdErr + committed.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("codex", JsonNode.Parse(File.ReadAllText(markerPath))!["mcpRegistrations"]![0]!.GetValue<string>());
        AssertInstall(install);
        // The committed journal and the verified backup stay exactly as a process killed at the
        // commit point leaves them, and the next run must finish them without a rollback.
        Assert.Single(Directory.GetFiles(_root, ".aspose-cli-transaction-*.json"));
        Assert.Single(Directory.GetDirectories(_root, ".aspose-cli-backup-*"));
        string committedPayload = Snapshot(install, except: ".aspose-cli-install.json");

        PowerShellResult retry = RunInstaller(_package.Path, install);

        Assert.True(retry.ExitCode == 0, retry.StdErr + retry.StdOut);
        AssertInstall(install);
        // The retry records its own choices in the marker; the payload stays the committed one.
        Assert.Equal(committedPayload, Snapshot(install, except: ".aspose-cli-install.json"));
        AssertNoTransactionLeftovers();
    }

    [Fact]
    public void CleanInstallAndSameVersionUpgrade_PublishVerifiedV2Ownership()
    {
        Requires.Windows();
        string package = _package.Path;
        string install = Path.Combine(_root, "install with spaces");
        string? originalPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User);

        PowerShellResult first = RunInstaller(package, install);
        Assert.True(first.ExitCode == 0, first.StdErr);
        AssertInstall(install);
        string firstSnapshot = Snapshot(install);

        PowerShellResult second = RunInstaller(package, install);
        Assert.True(second.ExitCode == 0, second.StdErr);
        AssertInstall(install);
        Assert.Equal(firstSnapshot, Snapshot(install));
        Assert.Equal(originalPath, Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User));
    }

    [Fact]
    public void CleanInstall_WorksWithoutPowerShellModulesCommandPathOrMcpHosts()
    {
        Requires.Windows();

        string install = Path.Combine(_root, "minimal-environment");
        // An empty directory as the whole command path: nothing the installer or an MCP host
        // lookup could find, while the child processes it starts are named by full path.
        string emptyPath = Path.Combine(_root, "empty-command-path");
        Directory.CreateDirectory(emptyPath);
        PowerShellResult result = RunInstaller(
            _package.Path,
            install,
            new Dictionary<string, string?>
            {
                ["Path"] = emptyPath,
                ["PSModulePath"] = string.Empty,
            },
            skipMcp: false);

        Assert.True(result.ExitCode == 0, result.StdErr + result.StdOut);
        AssertInstall(install);
        using JsonDocument marker = JsonDocument.Parse(File.ReadAllText(Path.Combine(install, ".aspose-cli-install.json")));
        Assert.Equal(0, marker.RootElement.GetProperty("mcpRegistrations").GetArrayLength());
        Assert.True(marker.RootElement.GetProperty("choices").GetProperty("mcp").GetBoolean());
    }

    [Fact]
    public void CustomSkillsRoot_InstallsAndUpdatesOnlyTheIsolatedTree()
    {
        Requires.Windows();

        string install = Path.Combine(_root, "custom-root-install");
        string skills = Path.Combine(_root, "isolated skills");
        UserState before = CaptureUserState();

        PowerShellResult first = RunInstaller(
            _package.Path,
            install,
            arguments: ["-SkillsRoot", skills],
            skipSkills: false);
        Assert.True(first.ExitCode == 0, first.StdErr);
        AssertBundledSkills(skills);

        string stale = AddManagedStaleFile(Path.Combine(skills, "aspose-cli-cells"));
        string custom = Path.Combine(skills, "aspose-cli-pdf", "customer-notes.md");
        File.WriteAllText(custom, "preserve me", Encoding.UTF8);

        PowerShellResult update = RunInstaller(
            _package.Path,
            install,
            arguments: ["-SkillsRoot", skills],
            skipSkills: false);

        Assert.True(update.ExitCode == 0, update.StdErr);
        Assert.False(File.Exists(stale));
        Assert.Equal("preserve me", File.ReadAllText(custom, Encoding.UTF8));
        Assert.Contains("Skipped customized or unmanaged Skill 'aspose-cli-pdf'", update.StdErr + update.StdOut, StringComparison.Ordinal);
        Assert.True(RunSkillOwnershipValidation(Path.Combine(skills, "aspose-cli-cells"), "aspose-cli-cells").ExitCode == 0);
        Assert.Equal(before, CaptureUserState());
    }

    [Fact]
    public void CustomSkillsRoot_RejectsConflictAndUnsafePathsWithoutUserMutation()
    {
        Requires.Windows();

        string install = Path.Combine(_root, "unsafe-root-install");
        string valid = Path.Combine(_root, "safe-skills");
        UserState before = CaptureUserState();

        PowerShellResult conflict = RunInstaller(
            _package.Path,
            install,
            arguments: ["-SkillsRoot", valid]);
        Assert.NotEqual(0, conflict.ExitCode);
        Assert.Contains("conflicts with -SkipSkills", conflict.StdErr + conflict.StdOut, StringComparison.Ordinal);

        foreach (string unsafeRoot in new[] { "relative-skills", "https://example.invalid/skills", @"\\server\share\skills", @"\\?\C:\skills" })
        {
            PowerShellResult rejected = RunInstaller(
                _package.Path,
                install,
                arguments: ["-SkillsRoot", unsafeRoot],
                skipSkills: false);
            Assert.NotEqual(0, rejected.ExitCode);
            Assert.Contains("absolute local fixed-disk", rejected.StdErr + rejected.StdOut, StringComparison.OrdinalIgnoreCase);
        }

        string junctionTarget = Path.Combine(_root, "junction-target");
        string junction = Path.Combine(_root, "junction-skills");
        Directory.CreateDirectory(junctionTarget);
        FileSystemLinks.CreateDirectoryLink(junction, junctionTarget);
        try
        {
            PowerShellResult rejected = RunInstaller(
                _package.Path,
                install,
                arguments: ["-SkillsRoot", junction],
                skipSkills: false);
            Assert.NotEqual(0, rejected.ExitCode);
            Assert.Contains("reparse point", rejected.StdErr + rejected.StdOut, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(junction);
        }

        Assert.Equal(before, CaptureUserState());
        Assert.False(Directory.Exists(install));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustomSkillsRoot_FailureRestoresTheCliAndEntireSkillTree(bool existingSkillTree)
    {
        Requires.Windows();

        string install = Path.Combine(_root, "custom-root-rollback");
        string skills = Path.Combine(_root, "rollback-skills");
        UserState beforeUser = CaptureUserState();
        if (existingSkillTree)
        {
            _package.CopySkillsInstallation(install, skills);
        }
        else
        {
            _package.CopyInstallation(install);
        }
        string beforeInstall = Snapshot(install);
        string beforeSkills = SnapshotOptional(skills);

        PowerShellResult failed = RunInstaller(
            _package.Path,
            install,
            new Dictionary<string, string?> { ["ASPOSE_CLI_INSTALL_FAULT"] = "skillUpdated" },
            arguments: ["-SkillsRoot", skills],
            skipSkills: false);

        Assert.NotEqual(0, failed.ExitCode);
        Assert.Equal(beforeInstall, Snapshot(install));
        Assert.Equal(beforeSkills, SnapshotOptional(skills));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(install)!, ".aspose-cli-transaction-*.json"));
        Assert.Equal(beforeUser, CaptureUserState());
    }

    [Fact]
    public void CustomSkillsRoot_RecoveryUsesTheJournalRootWhateverTheNewRequest()
    {
        Requires.Windows();

        string install = Path.Combine(_root, "custom-root-recovery");
        string originalRoot = Path.Combine(_root, "original-skills");
        string otherRoot = Path.Combine(_root, "other-skills");
        UserState beforeUser = CaptureUserState();
        _package.CopySkillsInstallation(install, originalRoot);
        // The marker records the Skills root, so compare the payload without it.
        string beforeInstall = Snapshot(install, except: ".aspose-cli-install.json");
        string beforeSkills = Snapshot(originalRoot);

        PowerShellResult interrupted = RunInstaller(
            _package.Path,
            install,
            new Dictionary<string, string?> { ["ASPOSE_CLI_INSTALL_CRASH"] = "skillUpdated" },
            arguments: ["-SkillsRoot", originalRoot],
            skipSkills: false);
        Assert.Equal(97, interrupted.ExitCode);

        PowerShellResult otherRequest = RunInstaller(
            _package.Path,
            install,
            arguments: ["-SkillsRoot", otherRoot],
            skipSkills: false);
        Assert.True(otherRequest.ExitCode == 0, otherRequest.StdErr + otherRequest.StdOut);
        // The interrupted transaction was rolled back in the root its journal names...
        Assert.Equal(beforeSkills, Snapshot(originalRoot));
        // ...before the new request installed into its own root.
        AssertBundledSkills(otherRoot);
        Assert.Equal(beforeInstall, Snapshot(install, except: ".aspose-cli-install.json"));
        using (JsonDocument marker = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(install, ".aspose-cli-install.json"))))
        {
            Assert.Equal(otherRoot, marker.RootElement.GetProperty("choices").GetProperty("skillsRoot").GetString());
        }
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(install)!, ".aspose-cli-transaction-*.json"));
        Assert.Equal(beforeUser, CaptureUserState());
    }

    [Fact]
    public void CustomSkillsRoot_RecoveryRejectsAJunctionSubstitution()
    {
        Requires.Windows();

        string install = Path.Combine(_root, "junction-swap-install");
        string skills = Path.Combine(_root, "junction-swap-skills");
        string parked = Path.Combine(_root, "junction-swap-parked");
        string attacker = Path.Combine(_root, "junction-swap-attacker");
        _package.CopySkillsInstallation(install, skills);
        string beforeInstall = Snapshot(install);
        string beforeSkills = Snapshot(skills);

        PowerShellResult interrupted = RunInstaller(
            _package.Path,
            install,
            new Dictionary<string, string?> { ["ASPOSE_CLI_INSTALL_CRASH"] = "skillPrepared" },
            arguments: ["-SkillsRoot", skills],
            skipSkills: false);
        Assert.Equal(97, interrupted.ExitCode);
        Directory.Move(skills, parked);
        Directory.CreateDirectory(attacker);
        FileSystemLinks.CreateDirectoryLink(skills, attacker);
        try
        {
            // Recovery itself meets the junction: an installer run would refuse its own
            // -SkillsRoot argument before it reached the journal.
            PowerShellResult rejected = RunPendingRecovery(install);
            Assert.NotEqual(0, rejected.ExitCode);
            Assert.Contains("reparse point", Flat(rejected), StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.EnumerateFileSystemEntries(attacker));
            Assert.Single(Directory.GetFiles(_root, ".aspose-cli-transaction-*.json"));
        }
        finally
        {
            if (Directory.Exists(skills)
                && (File.GetAttributes(skills) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(skills);
            }
            Directory.Move(parked, skills);
        }

        PowerShellResult recovered = RunPendingRecovery(install);
        Assert.True(recovered.ExitCode == 0, recovered.StdErr + recovered.StdOut);
        Assert.Equal(beforeInstall, Snapshot(install));
        Assert.Equal(beforeSkills, Snapshot(skills));
        AssertNoTransactionLeftovers();
    }

    [Theory]
    [InlineData("pathIntent", false, false)]
    [InlineData("pathWrittenBeforeJournal", true, false)]
    [InlineData("pathApplied", true, false)]
    [InlineData("pathApplied", true, true)]
    public void PathTransaction_FaultBoundariesRestoreTheExactOriginalPath(
        string phase,
        bool writeOccurred,
        bool originalNull)
    {
        Requires.Windows();

        PowerShellResult result = RunPathTransactionValidation(phase, originalNull);

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Contains($"writeOccurred={writeOccurred.ToString().ToLowerInvariant()}", result.StdOut, StringComparison.Ordinal);
        Assert.Contains(originalNull ? "restored=<null>" : "restored=ORIGINAL", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void PathComposition_KeepsRawEntriesAndLeavesExactlyOneInstallEntry()
    {
        Requires.Windows();

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string installRoot = Path.Combine(localAppData, "Aspose", "CLI");
        string current = string.Join(';',
            @"%USERPROFILE%\AppData\Local\Microsoft\WindowsApps",
            @"%LOCALAPPDATA%\Aspose\CLI\",
            @"C:\Tools",
            $"\"{installRoot.ToUpperInvariant()}\"",
            "",
            installRoot);
        string installer = Path.Combine(RepositoryPaths.Root, "install.ps1");
        string command = $". {PowerShellLiteral(installer)}; "
            + $"Get-UpdatedUserPath {PowerShellLiteral(current)} {PowerShellLiteral(installRoot)}";

        PowerShellResult result = RunExecutable(
            "powershell.exe",
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command]);

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal(
            string.Join(';', @"%USERPROFILE%\AppData\Local\Microsoft\WindowsApps", @"C:\Tools", installRoot),
            result.StdOut.Trim());
    }

    [Fact]
    public void UnsupportedOldInstallIsRejectedWithoutExecutingOrChangingIt()
    {
        Requires.Windows();
        string source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "where.exe");
        string old = CreateV1Install("unsupported-old", source);
        string before = Snapshot(old);
        string installer = Path.Combine(RepositoryPaths.Root, "install.ps1");
        string command = $". {PowerShellLiteral(installer)}; Get-ManagedInstallState {PowerShellLiteral(old)}";
        PowerShellResult result = RunExecutable("powershell.exe",
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command]);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("schemaVersion", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Snapshot(old));
    }



    [Fact]
    public void InstallerOwnershipValidator_AcceptsCurrentAndRejectsOldSkillManifests()
    {
        Requires.Windows();
        string parent = Path.Combine(_root, "skill ownership");
        PowerShellResult installed = RunExecutable(
            Path.Combine(_package.Path, "aspose-cli.exe"),
            ["skill", "install", "aspose-cli-cells", "--target", parent, "--output", "json"]);
        Assert.True(installed.ExitCode == 0, installed.StdErr);

        string root = Path.Combine(parent, "aspose-cli-cells");
        PowerShellResult version2 = RunSkillOwnershipValidation(root, "aspose-cli-cells");
        Assert.True(version2.ExitCode == 0, version2.StdErr);

        string manifestPath = Path.Combine(root, ".aspose-skill-manifest.json");
        using JsonDocument version2Manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        JsonElement source = version2Manifest.RootElement;
        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                skill = source.GetProperty("skill").GetString(),
                cliVersion = source.GetProperty("cliVersion").GetString(),
                executable = source.GetProperty("executable").GetString(),
                executableSha256 = source.GetProperty("executableSha256").GetString(),
                contentSha256 = source.GetProperty("contentSha256").GetString(),
            }),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        PowerShellResult version1 = RunSkillOwnershipValidation(root, "aspose-cli-cells");
        Assert.NotEqual(0, version1.ExitCode);
    }

    [Fact]
    public void InvalidMarkerAndUnknownSentinel_AreRejectedWithoutDeletingAnything()
    {
        Requires.Windows();
        string install = Path.Combine(_root, "owned");
        _package.CopyInstallation(install);
        string marker = Path.Combine(install, ".aspose-cli-install.json");
        File.WriteAllText(marker, "{\"schemaVersion\":2,\"schemaVersion\":2}", Encoding.UTF8);
        string sentinel = Path.Combine(install, "CUSTOMER-DO-NOT-DELETE.txt");
        File.WriteAllText(sentinel, "customer-owned", Encoding.UTF8);
        string before = Snapshot(install);

        PowerShellResult result = RunInstaller(_package.Path, install);

        // Every ownership rule rejects through the same check (see the marker table below);
        // this run shows that a rejected installation is left exactly as it was.
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("duplicate JSON property", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("customer-owned", File.ReadAllText(sentinel, Encoding.UTF8));
        Assert.Equal(before, Snapshot(install));
        AssertNoTransactionLeftovers();
    }

    [Fact]
    public void CustomerInstall_RejectsUnsignedPackagesUnlessDevelopmentModeIsExplicit()
    {
        Requires.Windows();
        string customerInstall = Path.Combine(_root, "unsigned-customer");
        PowerShellResult rejected = RunInstaller(
            _package.Path,
            customerInstall,
            developmentPackage: false);

        Assert.NotEqual(0, rejected.ExitCode);
        Assert.Contains("Authenticode signature", rejected.StdErr + rejected.StdOut, StringComparison.Ordinal);
        Assert.False(Directory.Exists(customerInstall));
        // Every other test installs this unsigned package with an explicit -DevelopmentPackage.
    }

    [Fact]
    public void CustomerInstall_VerifiesTrustedSignatureBeforeExecutingPayload()
    {
        Requires.Windows();
        (string package, string trustRing) = CreateSignedPackage("signed-package");
        var environment = new Dictionary<string, string?>
        {
            ["ASPOSE_CLI_RELEASE_TRUSTED_KEYS"] = trustRing,
        };

        PowerShellResult untrusted = RunCustomerPackageTrustValidation(package);
        Assert.NotEqual(0, untrusted.ExitCode);
        Assert.Contains("ASPOSE_CLI_RELEASE_TRUSTED_KEYS", untrusted.StdErr + untrusted.StdOut, StringComparison.Ordinal);

        PowerShellResult accepted = RunCustomerPackageTrustValidation(package, environment);
        Assert.True(accepted.ExitCode == 0, accepted.StdErr);

        File.AppendAllText(Path.Combine(package, "aspose-cli.exe"), "tampered", Encoding.UTF8);
        WriteChecksums(package);
        PowerShellResult rejected = RunCustomerPackageTrustValidation(package, environment);

        Assert.NotEqual(0, rejected.ExitCode);
        Assert.Contains("signature", rejected.StdErr + rejected.StdOut, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("powershell")]
    [InlineData("pwsh")]
    public void Marker_EveryOwnershipRuleRefusesTheInstallationForItsOwnReason(string shell)
    {
        Requires.Windows();
        string executable = shell == "pwsh" ? ToolPath.Require("pwsh") : "powershell.exe";
        string cases = Path.Combine(_root, "marker-cases");
        var expected = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach ((string name, Action<string> damage, string? reason) in MarkerCases())
        {
            string install = Path.Combine(cases, name);
            _package.CopyInstallation(install);
            damage(install);
            expected.Add(name, reason);
        }
        // The installer decides ownership with this one check before it changes anything.
        string command = $". {PowerShellLiteral(Path.Combine(RepositoryPaths.Root, "install.ps1"))}; "
            + $"foreach ($case in @(Get-ChildItem -LiteralPath {PowerShellLiteral(cases)} -Directory)) {{ "
            + "try { [void](Get-ManagedInstallState $case.FullName); \"$($case.Name)|accepted\" } "
            + "catch { \"$($case.Name)|$($_.Exception.Message)\" } }";

        PowerShellResult result = RunExecutable(executable,
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command]);

        Assert.True(result.ExitCode == 0, result.StdErr + result.StdOut);
        Dictionary<string, string> outcomes = result.StdOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('|', 2))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), outcomes.Keys.Order(StringComparer.Ordinal));
        foreach ((string name, string? reason) in expected)
        {
            string outcome = outcomes[name];
            Assert.True(
                reason is null ? outcome == "accepted" : outcome.Contains(reason, StringComparison.Ordinal),
                $"{name}: {outcome}");
        }
    }

    [Fact]
    public void OfficialMcpCapture_BoundsOutputAndTerminatesTheProcessTree()
    {
        Requires.Windows();
        string pidFile = Path.Combine(_root, "mcp-child.pid");
        string producer = Path.Combine(_root, "mcp-output.ps1");
        File.WriteAllText(
            producer,
            "$child = Start-Process powershell.exe -WindowStyle Hidden -ArgumentList '-NoLogo','-NoProfile','-NonInteractive','-Command','Start-Sleep -Seconds 60' -PassThru\n"
            + $"[IO.File]::WriteAllText('{pidFile.Replace("'", "''", StringComparison.Ordinal)}', [string]$child.Id)\n"
            + "[Console]::Out.Write(('x' * (2MB)))\nStart-Sleep -Seconds 60\n",
            new UTF8Encoding(false));
        string installer = Path.Combine(RepositoryPaths.Root, "install.ps1");
        string command = $". {PowerShellLiteral(installer)}; Invoke-OfficialMcp 'powershell.exe' @('-NoLogo','-NoProfile','-NonInteractive','-File',{PowerShellLiteral(producer)})";

        PowerShellResult result = RunExecutable(
            "powershell.exe",
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("1 MiB", result.StdErr + result.StdOut, StringComparison.Ordinal);
        Assert.True(File.Exists(pidFile));
        int childId = int.Parse(File.ReadAllText(pidFile, Encoding.UTF8), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(SpinWait.SpinUntil(() => !IsProcessRunning(childId), TimeSpan.FromSeconds(10)));
    }

    [Theory]
    [InlineData("prepared")]
    [InlineData("oldMoved")]
    public void InjectedTransactionFailure_RestoresOriginalInstall(string phase)
    {
        Requires.Windows();
        string install = Path.Combine(_root, "rollback-" + phase);
        _package.CopyInstallation(install);
        string before = Snapshot(install);

        PowerShellResult result = RunInstaller(
            _package.Path,
            install,
            new Dictionary<string, string?> { ["ASPOSE_CLI_INSTALL_FAULT"] = phase });

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(before, Snapshot(install));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(install)!, ".aspose-cli-transaction-*.json"));
    }

    [Theory]
    [InlineData("oldMovedBeforeJournal", false)]
    [InlineData("newPublishedBeforeJournal", true)]
    public void DirectoryMoveBeforePhaseWrite_IsRecoveredFromVerifiedSnapshots(string phase, bool newTreePublished)
    {
        Requires.Windows();
        string install = Path.Combine(_root, "pre-journal-" + phase);
        _package.CopyInstallation(install);
        // The package publishes a tree that differs from this one, so recovery has to tell the
        // published tree from the backup instead of finding the original already in place.
        RecordAnotherBuild(install);
        string before = Snapshot(install);

        PowerShellResult interrupted = RunInstaller(
            _package.Path,
            install,
            new Dictionary<string, string?> { ["ASPOSE_CLI_INSTALL_CRASH"] = phase });
        Assert.Equal(97, interrupted.ExitCode);
        if (newTreePublished)
        {
            Assert.NotEqual(before, Snapshot(install));
        }
        else
        {
            Assert.False(Directory.Exists(install));
        }

        PowerShellResult recovered = RunPendingRecovery(install);

        Assert.True(recovered.ExitCode == 0, recovered.StdErr + recovered.StdOut);
        Assert.Equal(before, Snapshot(install));
        AssertNoTransactionLeftovers();
    }

    [Fact]
    public void PackageAndInstallDirectoriesMayNotOverlap()
    {
        Requires.Windows();
        string package = _package.Path;

        PowerShellResult result = RunInstaller(package, Path.Combine(package, "installed"));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("may not overlap", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidCommercialLicense_IsRejectedBeforeExistingInstallChanges()
    {
        Requires.Windows();
        string install = Path.Combine(_root, "invalid-license");
        _package.CopyInstallation(install);
        string before = Snapshot(install);
        string invalidLicense = Path.Combine(_root, "invalid.lic");
        File.WriteAllText(invalidLicense, "not a license", Encoding.UTF8);

        PowerShellResult result = RunInstaller(
            _package.Path,
            install,
            new Dictionary<string, string?>
            {
                ["ASPOSE_CLI_CONFIG_DIR"] = Path.Combine(_root, "config"),
            },
            arguments: ["-LicensePath", invalidLicense, "-LicenseProduct", "cells"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(before, Snapshot(install));
        Assert.False(Directory.Exists(Path.Combine(_root, "config")));
    }

    // Each case damages one copy of the baseline installation; a null reason marks the
    // unchanged control, which must be accepted.
    private static IEnumerable<(string Name, Action<string> Damage, string? Reason)> MarkerCases()
    {
        yield return ("valid", _ => { }, null);
        yield return ("duplicate-property",
            install => File.WriteAllText(MarkerPath(install), "{\"schemaVersion\":3,\"schemaVersion\":3}", new UTF8Encoding(false)),
            "duplicate JSON property 'schemaVersion'");
        yield return ("missing-schemaVersion", Remove("schemaVersion"), "missing required property schemaVersion");
        foreach (string property in new[] { "productId", "edition", "cliVersion", "payloadManifest", "payloadManifestSha256", "sourceRevision", "choices" })
        {
            yield return ("missing-" + property, Remove(property), $"missing: [{property}]");
        }
        yield return ("missing-mcpRegistrations", Remove("mcpRegistrations"), "missing mcpRegistrations");
        // The supported schema version, in a JSON type other than an integer.
        yield return ("schema-string", Change(marker => marker["schemaVersion"] = "3"), "schemaVersion must be an integer");
        yield return ("schema-fraction", Change(marker => marker["schemaVersion"] = 3.5), "schemaVersion must be an integer");
        yield return ("empty-version", Change(marker => marker["cliVersion"] = ""), "invalid ownership fields");
        yield return ("revision", Change(marker => marker["sourceRevision"] = "abc"), "invalid ownership fields");
        yield return ("mcp-type", Change(marker => marker["mcpRegistrations"] = "codex"), "invalid ownership fields");
        yield return ("mcp-duplicate", Change(marker => marker["mcpRegistrations"] = new JsonArray("codex", "codex")), "invalid or duplicate MCP registration");
        yield return ("choices-type", Change(marker => marker["choices"] = "detected-hosts"), "invalid installation choices");
        yield return ("choices-skills", Change(marker => marker["choices"]!["skills"] = "everywhere"), "invalid installation choices");
        yield return ("choices-root", Change(marker => marker["choices"]!["skillsRoot"] = @"C:\skills"), "invalid installation choices");
        yield return ("choices-path", Change(marker => marker["choices"]!["path"] = "yes"), "invalid installation choices");
        yield return ("choices-extra", Change(marker => marker["choices"]!["license"] = true), "unknown: [license]");
        yield return ("unknown-file",
            install => File.WriteAllText(Path.Combine(install, "unknown.bin"), "sentinel", Encoding.UTF8),
            "unknown: [unknown.bin]");
        yield return ("modified-payload", ModifyExecutableKeepingItsSize, "Installed payload 'aspose-cli.exe' was modified");

        static Action<string> Remove(string property) => install => RewriteMarker(install, marker => marker.Remove(property));
        static Action<string> Change(Action<JsonObject> change) => install => RewriteMarker(install, change);
    }

    private static void ModifyExecutableKeepingItsSize(string install)
    {
        string executable = Path.Combine(install, "aspose-cli.exe");
        byte[] content = File.ReadAllBytes(executable);
        content[^1] ^= 0xFF;
        // The payload is a hard link to the baseline: replace the link, never write through it.
        File.Delete(executable);
        File.WriteAllBytes(executable, content);
    }

    private static string MarkerPath(string install) => Path.Combine(install, ".aspose-cli-install.json");

    private static void RewriteMarker(string install, Action<JsonObject> change)
    {
        JsonObject marker = JsonNode.Parse(File.ReadAllText(MarkerPath(install), Encoding.UTF8))!.AsObject();
        change(marker);
        File.WriteAllText(MarkerPath(install), marker.ToJsonString(), new UTF8Encoding(false));
    }

    // A different build of the same version, so a tree the package publishes over this
    // installation differs from it.
    private static void RecordAnotherBuild(string install) =>
        RewriteMarker(install, marker => marker["sourceRevision"] = new string('a', 40));

    internal static PowerShellResult RunInstaller(
        string package,
        string install,
        IReadOnlyDictionary<string, string?>? environment = null,
        IReadOnlyList<string>? arguments = null,
        bool skipPath = true,
        bool skipSkills = true,
        bool developmentPackage = true,
        bool skipMcp = true)
    {
        string script = Path.Combine(RepositoryPaths.Root, "install.ps1");
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in new[]
        {
            "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
            "-File", script,
            "-PackageRoot", package,
            "-InstallDirectory", install,
            "-SkipLicensePrompt",
        })
        {
            start.ArgumentList.Add(argument);
        }
        if (skipMcp) { start.ArgumentList.Add("-SkipMcp"); }
        if (skipPath)
        {
            start.ArgumentList.Add("-SkipPath");
        }
        if (skipSkills)
        {
            start.ArgumentList.Add("-SkipSkills");
        }
        if (developmentPackage)
        {
            start.ArgumentList.Add("-DevelopmentPackage");
        }
        if (arguments is not null)
        {
            foreach (string argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }
        }
        start.Environment["DOTNET_EnableCrashReport"] = "0";
        start.Environment["ASPOSE_CLI_CONFIG_DIR"] = Path.Combine(
            Path.GetDirectoryName(install)!,
            ".config-" + Path.GetFileName(install));
        if (environment is not null)
        {
            foreach ((string name, string? value) in environment)
            {
                start.Environment[name] = value;
            }
        }

        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(90)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Installer blocked for 90 seconds; an interactive dialog may have been shown.");
        }
        Task.WaitAll(output, error);
        return new(process.ExitCode, output.Result, error.Result);
    }

    private (string Package, string TrustRing) CreateSignedPackage(string name)
    {
        string package = Path.Combine(_root, name);
        Directory.CreateDirectory(package);
        foreach (string source in Directory.GetFiles(_package.Path))
        {
            File.Copy(source, Path.Combine(package, Path.GetFileName(source)));
        }
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] publicKey = key.ExportSubjectPublicKeyInfo();
        string keyId = Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();
        File.WriteAllText(
            Path.Combine(package, "PACKAGE-SIGNATURE.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                productId = "aspose-cli",
                algorithm = "ECDSA-P256-SHA256",
                format = "rfc3279-der",
                keyId,
                signedFile = "SHA256SUMS",
            }),
            new UTF8Encoding(false));
        byte[] signature = key.SignData(
            File.ReadAllBytes(Path.Combine(package, "SHA256SUMS")),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);
        File.WriteAllText(
            Path.Combine(package, "PACKAGE-SIGNATURE.sig"),
            Convert.ToBase64String(signature) + Environment.NewLine,
            new UTF8Encoding(false));
        string trustRing = Path.Combine(_root, name + "-trust.json");
        File.WriteAllText(
            trustRing,
            JsonSerializer.Serialize(new
            {
                keys = new[] { new { keyId, publicKeyPem = key.ExportSubjectPublicKeyInfoPem() } },
            }),
            new UTF8Encoding(false));
        return (package, trustRing);
    }

    private static void WriteChecksums(string package)
    {
        string[] excluded = ["SHA256SUMS", "PACKAGE-SIGNATURE.json", "PACKAGE-SIGNATURE.sig"];
        string[] files = Directory.GetFiles(package)
            .Where(path => !excluded.Contains(Path.GetFileName(path), StringComparer.Ordinal))
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();
        File.WriteAllText(
            Path.Combine(package, "SHA256SUMS"),
            string.Join(Environment.NewLine, files.Select(path => $"{FileHashes.Sha256(path)}  {Path.GetFileName(path)}")) + Environment.NewLine,
            new UTF8Encoding(false));
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static PowerShellResult RunSkillOwnershipValidation(string root, string skill)
    {
        string installer = Path.Combine(RepositoryPaths.Root, "install.ps1");
        string command = $". {PowerShellLiteral(installer)}; "
            + $"$state = Get-SkillState {PowerShellLiteral(root)} {PowerShellLiteral(skill)}; "
            + "Write-Output $state.Snapshot";
        return RunExecutable(
            "powershell.exe",
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command]);
    }

    private static PowerShellResult RunCustomerPackageTrustValidation(
        string package,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        string installer = Path.Combine(RepositoryPaths.Root, "install.ps1");
        string command = $". {PowerShellLiteral(installer)}; "
            + $"$root = {PowerShellLiteral(package)}; "
            + "$inventory = Get-TreeInventory $root; "
            + "Assert-CustomerPackageTrust $root ([IO.File]::ReadAllBytes((Join-Path $root 'SHA256SUMS'))) $inventory";
        return RunExecutable(
            "powershell.exe",
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command],
            environment);
    }

    private static PowerShellResult RunExecutable(
        string executable,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            ErrorDialog = false,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        if (environment is not null)
        {
            foreach ((string name, string? value) in environment)
            {
                start.Environment[name] = value;
            }
        }
        start.Environment["DOTNET_EnableCrashReport"] = "0";

        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(90)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Child process blocked for 90 seconds; an interactive dialog may have been shown.");
        }
        Task.WaitAll(output, error);
        return new(process.ExitCode, output.Result, error.Result);
    }

    private static string PowerShellLiteral(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static void AssertInstall(string install)
    {
        string markerPath = Path.Combine(install, ".aspose-cli-install.json");
        using JsonDocument marker = JsonDocument.Parse(File.ReadAllBytes(markerPath));
        Assert.Equal(3, marker.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Matches("^(?:[0-9a-f]{40}|unknown)$", marker.RootElement.GetProperty("sourceRevision").GetString());
        Assert.Equal(JsonValueKind.Object, marker.RootElement.GetProperty("choices").ValueKind);
        Assert.Equal("aspose-cli", marker.RootElement.GetProperty("productId").GetString());
        Assert.True(marker.RootElement.GetProperty("mcpRegistrations").GetArrayLength() >= 0);
        string manifestName = marker.RootElement.GetProperty("payloadManifest").GetString()!;
        string manifest = Path.Combine(install, manifestName);
        Assert.Equal(FileHashes.Sha256(manifest), marker.RootElement.GetProperty("payloadManifestSha256").GetString());
    }

    private string CreateV1Install(string name, string executable)
    {
        string root = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.Combine(root, "licenses"));
        File.Copy(executable, Path.Combine(root, "aspose-cli.exe"));
        File.WriteAllText(
            Path.Combine(root, ".aspose-cli-install.json"),
            "{\"schemaVersion\":1,\"edition\":\"free\",\"cliVersion\":\"1.0.0\"}",
            new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(root, "THIRD-PARTY-NOTICES.md"), "test", Encoding.UTF8);
        foreach (string product in new[] { "Cells", "Pdf", "Slides", "Words" })
        {
            File.WriteAllText(
                Path.Combine(root, "licenses", $"Aspose.{product}.FOSS.LICENSE.txt"),
                "test",
                Encoding.UTF8);
        }
        return root;
    }



    private PowerShellResult RunPathTransactionValidation(string phase, bool originalNull)
    {
        string installer = Path.Combine(RepositoryPaths.Root, "install.ps1");
        string journal = Path.Combine(_root, "path-" + phase + ".json");
        string command = $". {PowerShellLiteral(installer)}; "
            + "$script:testPath = " + (originalNull ? "$null; " : "'ORIGINAL'; ")
            + "function Get-UserPath { $script:testPath }; function Set-UserPath { param([AllowNull()]$Value) $script:testPath = $Value }; "
            + "$key = '0123456789abcdef'; $journal = [ordered]@{ pathState='none'; originalPath=(Protect-PathValue $script:testPath $key); originalPathNull=" + (originalNull ? "$true" : "$false") + "; appliedPathSha256='' }; "
            + $"$env:ASPOSE_CLI_INSTALL_FAULT = {PowerShellLiteral(phase)}; "
            + $"try {{ Set-TransactionalUserPath {PowerShellLiteral(journal)} $journal 'UPDATED' }} catch {{ }} finally {{ $env:ASPOSE_CLI_INSTALL_FAULT = $null }}; "
            + $"$before = $script:testPath; $persistedJournal = Read-StrictJson {PowerShellLiteral(journal)} 'test journal'; Restore-TransactionPath $persistedJournal $key 'C:\\unused-install-root'; "
            + "Write-Output ('writeOccurred=' + ($before -ceq 'UPDATED').ToString().ToLowerInvariant() + ';restored=' + $(if ($null -eq $script:testPath) { '<null>' } else { $script:testPath }))";
        return RunExecutable(
            "powershell.exe",
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command]);
    }

    private static void AssertBundledSkills(string root)
    {
        string[] expected = ["aspose-cli-cells", "aspose-cli-pdf", "aspose-cli-slides", "aspose-cli-words"];
        Assert.Equal(expected, Directory.GetDirectories(root).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        foreach (string skill in expected)
        {
            PowerShellResult result = RunSkillOwnershipValidation(Path.Combine(root, skill), skill);
            Assert.True(result.ExitCode == 0, result.StdErr);
        }
    }

    private static string AddManagedStaleFile(string root)
    {
        string stale = Path.Combine(root, "stale-managed.txt");
        File.WriteAllText(stale, "obsolete", Encoding.UTF8);
        string manifestPath = Path.Combine(root, ".aspose-skill-manifest.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        JsonElement source = manifest.RootElement;
        string[] contentFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(path, manifestPath, StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string relative in contentFiles)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(relative));
            hash.AppendData([0]);
            hash.AppendData(File.ReadAllBytes(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))));
        }
        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 2,
                productId = source.GetProperty("productId").GetString(),
                skill = source.GetProperty("skill").GetString(),
                cliVersion = source.GetProperty("cliVersion").GetString(),
                executable = source.GetProperty("executable").GetString(),
                executableSha256 = source.GetProperty("executableSha256").GetString(),
                contentSha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
                files = contentFiles.Select(relative => new
                {
                    path = relative,
                    size = new FileInfo(Path.Combine(root, relative)).Length,
                    sha256 = FileHashes.Sha256(Path.Combine(root, relative)),
                }),
            }),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return stale;
    }

    private static UserState CaptureUserState()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new(
            Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User),
            SnapshotOptional(Path.Combine(profile, ".agents", "skills")),
            SnapshotOptional(Path.Combine(profile, ".claude", "skills")),
            SnapshotOptional(Path.Combine(profile, ".config", "opencode", "skills")));
    }

    private static string SnapshotOptional(string directory) =>
        Directory.Exists(directory) ? Snapshot(directory) : "<missing>";

    private static string Snapshot(string directory, string? except = null)
    {
        var lines = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => new
            {
                Path = Path.GetRelativePath(directory, path).Replace('\\', '/'),
                Hash = FileHashes.Sha256(path),
            })
            .Where(item => !string.Equals(item.Path, except, StringComparison.Ordinal))
            .OrderBy(static item => item.Path, StringComparer.Ordinal)
            .Select(static item => item.Path + "|" + item.Hash);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))))
            .ToLowerInvariant();
    }

    private static void DeleteDirectoryWithRetry(string root)
    {
        for (int attempt = 0; attempt != 50; attempt++)
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
                return;
            }
            catch (IOException) when (attempt != 49)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt != 49)
            {
                Thread.Sleep(100);
            }
        }
    }

    internal sealed record PowerShellResult(int ExitCode, string StdOut, string StdErr);

    private sealed record UserState(string? Path, string Codex, string Claude, string OpenCode);
}

/// <summary>
/// The package every installer test installs, and installations of it that the installer made
/// and verified once for the class. A test whose starting state is an existing installation
/// copies one instead of installing it again.
/// </summary>
public sealed class CustomerInstallerPackageFixture : IDisposable
{
    private const string MarkerName = ".aspose-cli-install.json";
    private const string PayloadManifestName = ".aspose-cli-payload.json";

    private readonly string _root = Directory.CreateTempSubdirectory("aspose-installer-package-").FullName;
    private readonly Lazy<string> _installation;
    private readonly Lazy<(string Installation, string SkillsRoot)> _skillsInstallation;

    public CustomerInstallerPackageFixture()
    {
        _installation = new(() => Install("installation", skillsRoot: null));
        _skillsInstallation = new(() =>
        {
            string skillsRoot = System.IO.Path.Combine(_root, "skills");
            return (Install("skills-installation", skillsRoot), skillsRoot);
        });
        Path = System.IO.Path.Combine(_root, "package");
        Directory.CreateDirectory(Path);
        string sourceDirectory = System.IO.Path.GetDirectoryName(CliRunner.ExecutablePath)!;
        foreach (string source in Directory.GetFiles(sourceDirectory))
        {
            File.Copy(source, System.IO.Path.Combine(Path, System.IO.Path.GetFileName(source)));
        }
        string[] files = Directory.GetFiles(Path)
            .OrderBy(static path => System.IO.Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();
        File.WriteAllText(
            System.IO.Path.Combine(Path, "SHA256SUMS"),
            string.Join(
                Environment.NewLine,
                files.Select(path => $"{FileSha256(path)}  {System.IO.Path.GetFileName(path)}"))
                + Environment.NewLine,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public string Path { get; }

    /// <summary>Copies the installation made with -SkipPath -SkipSkills -SkipMcp to <paramref name="install"/>.</summary>
    public void CopyInstallation(string install) => CopyInstallationTree(_installation.Value, install);

    /// <summary>
    /// Copies the installation made with -SkillsRoot -SkipPath -SkipMcp to <paramref name="install"/>
    /// and its Skills to <paramref name="skillsRoot"/>, which the copied marker records.
    /// </summary>
    public void CopySkillsInstallation(string install, string skillsRoot)
    {
        (string installation, string skills) = _skillsInstallation.Value;
        CopyInstallationTree(installation, install);
        foreach (string file in Directory.GetFiles(skills, "*", SearchOption.AllDirectories))
        {
            string target = System.IO.Path.Combine(skillsRoot, System.IO.Path.GetRelativePath(skills, file));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        string markerPath = System.IO.Path.Combine(install, MarkerName);
        JsonObject marker = JsonNode.Parse(File.ReadAllText(markerPath, Encoding.UTF8))!.AsObject();
        marker["choices"]!["skillsRoot"] = skillsRoot;
        File.WriteAllText(markerPath, marker.ToJsonString(), new UTF8Encoding(false));
    }

    private string Install(string name, string? skillsRoot)
    {
        string install = System.IO.Path.Combine(_root, name);
        CustomerInstallerPowerShellTests.PowerShellResult result = CustomerInstallerPowerShellTests.RunInstaller(
            Path,
            install,
            arguments: skillsRoot is null ? null : new[] { "-SkillsRoot", skillsRoot },
            skipSkills: skillsRoot is null);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"The installer could not create the '{name}' installation: {result.StdErr}{result.StdOut}");
        }
        return install;
    }

    // Payload files become hard links, since neither a test nor the installer writes one in
    // place. The marker and the payload manifest, which tests rewrite, are copied.
    private static void CopyInstallationTree(string source, string destination)
    {
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = System.IO.Path.GetRelativePath(source, file);
            string target = System.IO.Path.Combine(destination, relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            if (relative is MarkerName or PayloadManifestName)
            {
                File.Copy(file, target);
            }
            else if (!CreateHardLink(target, file, IntPtr.Zero))
            {
                throw new IOException($"CreateHardLink failed with {Marshal.GetLastWin32Error()}: {target}");
            }
        }
    }

    public void Dispose()
    {
        for (int attempt = 0; attempt != 50; attempt++)
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
                return;
            }
            catch (IOException) when (attempt != 49)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt != 49)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static string FileSha256(string path)
    {
        using FileStream input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string name, string existing, IntPtr security);
}
