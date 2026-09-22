using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Architecture.Tests;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

[CollectionDefinition("Customer installer user state", DisableParallelization = true)]
public sealed class CustomerInstallerUserStateCollection;

/// <summary>Real Windows PowerShell black-box coverage for customer installation ownership and recovery.</summary>
[Collection("Customer installer user state")]
public sealed class CustomerInstallerPowerShellTests : IDisposable, IClassFixture<CustomerInstallerPackageFixture>
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
        if (!OperatingSystem.IsWindows()) { return; }
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
        if (!OperatingSystem.IsWindows()) { return; }
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
    public void DefaultMcpPathWithoutHostExecutablesCompletesCleanInstall()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        string install = Path.Combine(_root, "default-mcp");
        string emptyPath = Path.Combine(_root, "empty-command-path");
        Directory.CreateDirectory(emptyPath);
        PowerShellResult result = RunInstaller(_package.Path, install,
            new Dictionary<string, string?> { ["Path"] = emptyPath }, skipMcp: false);
        Assert.True(result.ExitCode == 0, result.StdErr + result.StdOut);
        AssertV2Install(install);
        using JsonDocument marker = JsonDocument.Parse(File.ReadAllText(Path.Combine(install, ".aspose-cli-install.json")));
        Assert.Equal(0, marker.RootElement.GetProperty("mcpRegistrations").GetArrayLength());
    }

    [Fact]
    public void CrashAfterOptionalMcpMetadataDoesNotInvalidateCommittedRecovery()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        string install = Path.Combine(_root, "mcp-metadata-recovery");
        PowerShellResult initial = RunInstaller(_package.Path, install);
        Assert.True(initial.ExitCode == 0, initial.StdErr);
        string markerPath = Path.Combine(install, ".aspose-cli-install.json");
        JsonObject marker = JsonNode.Parse(File.ReadAllText(markerPath))!.AsObject();
        marker["mcpRegistrations"] = new JsonArray("codex");
        File.WriteAllText(markerPath, marker.ToJsonString());
        string emptyPath = Path.Combine(_root, "mcp-empty-path");
        Directory.CreateDirectory(emptyPath);
        PowerShellResult interrupted = RunInstaller(_package.Path, install,
            new Dictionary<string, string?> { ["Path"] = emptyPath, ["ASPOSE_CLI_INSTALL_CRASH"] = "mcpMetadataUpdated" },
            skipMcp: false);
        Assert.True(interrupted.ExitCode == 97, interrupted.StdErr + interrupted.StdOut);
        PowerShellResult recovered = RunInstaller(_package.Path, install);
        Assert.True(recovered.ExitCode == 0, recovered.StdErr + recovered.StdOut);
        AssertV2Install(install);
    }

    [Fact]
    public void CommittedCleanupFailureKeepsInstallValidAndDefersMcpMetadata()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        string install = Path.Combine(_root, "pending-cleanup");
        Assert.Equal(0, RunInstaller(_package.Path, install).ExitCode);
        string markerPath = Path.Combine(install, ".aspose-cli-install.json");
        JsonObject marker = JsonNode.Parse(File.ReadAllText(markerPath))!.AsObject();
        marker["mcpRegistrations"] = new JsonArray("codex");
        File.WriteAllText(markerPath, marker.ToJsonString());
        string emptyPath = Path.Combine(_root, "cleanup-empty-path");
        Directory.CreateDirectory(emptyPath);
        PowerShellResult committed = RunInstaller(_package.Path, install,
            new Dictionary<string, string?> { ["Path"] = emptyPath, ["ASPOSE_CLI_INSTALL_FAULT"] = "committedCleanup" }, skipMcp: false);
        Assert.True(committed.ExitCode == 0, committed.StdErr + committed.StdOut);
        Assert.Contains("cleanup remains pending", committed.StdErr + committed.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("codex", JsonNode.Parse(File.ReadAllText(markerPath))!["mcpRegistrations"]![0]!.GetValue<string>());
        AssertV2Install(install);
        PowerShellResult retry = RunInstaller(_package.Path, install);
        Assert.True(retry.ExitCode == 0, retry.StdErr + retry.StdOut);
    }

    [Fact]
    public void CleanInstallAndSameVersionUpgrade_PublishVerifiedV2Ownership()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string package = _package.Path;
        string install = Path.Combine(_root, "install with spaces");
        string? originalPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User);

        PowerShellResult first = RunInstaller(package, install);
        Assert.True(first.ExitCode == 0, first.StdErr);
        AssertV2Install(install);
        string firstSnapshot = Snapshot(install);

        PowerShellResult second = RunInstaller(package, install);
        Assert.True(second.ExitCode == 0, second.StdErr);
        AssertV2Install(install);
        Assert.Equal(firstSnapshot, Snapshot(install));
        Assert.Equal(originalPath, Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User));
    }

    [Fact]
    public void CleanInstall_WorksWithoutPowerShellHashModuleOrNormalPath()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string install = Path.Combine(_root, "minimal-environment");
        string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        PowerShellResult result = RunInstaller(
            _package.Path,
            install,
            new Dictionary<string, string?>
            {
                ["Path"] = systemDirectory,
                ["PSModulePath"] = string.Empty,
            });

        Assert.True(result.ExitCode == 0, result.StdErr);
        AssertV2Install(install);
    }

    [Fact]
    public void CustomSkillsRoot_InstallsAndUpdatesOnlyTheIsolatedTree()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

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
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

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
        PowerShellResult linked = RunExecutable(
            "powershell.exe",
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command",
             $"New-Item -ItemType Junction -Path {PowerShellLiteral(junction)} -Target {PowerShellLiteral(junctionTarget)} | Out-Null"]);
        Assert.True(linked.ExitCode == 0, linked.StdErr);
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
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string install = Path.Combine(_root, "custom-root-rollback");
        string skills = Path.Combine(_root, "rollback-skills");
        UserState beforeUser = CaptureUserState();
        PowerShellResult installed = existingSkillTree
            ? RunInstaller(
                _package.Path,
                install,
                arguments: ["-SkillsRoot", skills],
                skipSkills: false)
            : RunInstaller(_package.Path, install);
        Assert.Equal(0, installed.ExitCode);
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
    public void CustomSkillsRoot_RecoveryRejectsAChangedRequestedRoot()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string install = Path.Combine(_root, "custom-root-recovery");
        string originalRoot = Path.Combine(_root, "original-skills");
        string otherRoot = Path.Combine(_root, "other-skills");
        UserState beforeUser = CaptureUserState();
        Assert.Equal(0, RunInstaller(
            _package.Path,
            install,
            arguments: ["-SkillsRoot", originalRoot],
            skipSkills: false).ExitCode);
        string beforeInstall = Snapshot(install);
        string beforeSkills = Snapshot(originalRoot);

        PowerShellResult interrupted = RunInstaller(
            _package.Path,
            install,
            new Dictionary<string, string?> { ["ASPOSE_CLI_INSTALL_CRASH"] = "skillUpdated" },
            arguments: ["-SkillsRoot", originalRoot],
            skipSkills: false);
        Assert.Equal(97, interrupted.ExitCode);

        PowerShellResult wrongRoot = RunInstaller(
            _package.Path,
            install,
            arguments: ["-SkillsRoot", otherRoot],
            skipSkills: false);
        Assert.NotEqual(0, wrongRoot.ExitCode);
        Assert.Contains("journal is invalid", wrongRoot.StdErr + wrongRoot.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(otherRoot));

        PowerShellResult recovered = RunInstaller(
            _package.Path,
            install,
            arguments: ["-SkillsRoot", originalRoot],
            skipSkills: false);
        Assert.True(recovered.ExitCode == 0, recovered.StdErr);
        Assert.Equal(beforeInstall, Snapshot(install));
        Assert.Equal(beforeSkills, Snapshot(originalRoot));
        Assert.Equal(beforeUser, CaptureUserState());
    }

    [Fact]
    public void CustomSkillsRoot_RecoveryRejectsAJunctionSubstitution()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string install = Path.Combine(_root, "junction-swap-install");
        string skills = Path.Combine(_root, "junction-swap-skills");
        string parked = Path.Combine(_root, "junction-swap-parked");
        string attacker = Path.Combine(_root, "junction-swap-attacker");
        Assert.Equal(0, RunInstaller(
            _package.Path,
            install,
            arguments: ["-SkillsRoot", skills],
            skipSkills: false).ExitCode);
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
        PowerShellResult linked = CreateJunction(skills, attacker);
        Assert.True(linked.ExitCode == 0, linked.StdErr);
        try
        {
            PowerShellResult rejected = RunInstaller(
                _package.Path,
                install,
                arguments: ["-SkillsRoot", skills],
                skipSkills: false);
            Assert.NotEqual(0, rejected.ExitCode);
            Assert.Contains("reparse point", rejected.StdErr + rejected.StdOut, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.EnumerateFileSystemEntries(attacker));
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

        PowerShellResult recovered = RunInstaller(
            _package.Path,
            install,
            arguments: ["-SkillsRoot", skills],
            skipSkills: false);
        Assert.True(recovered.ExitCode == 0, recovered.StdErr);
        Assert.Equal(beforeInstall, Snapshot(install));
        Assert.Equal(beforeSkills, Snapshot(skills));
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
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        PowerShellResult result = RunPathTransactionValidation(phase, originalNull);

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Contains($"writeOccurred={writeOccurred.ToString().ToLowerInvariant()}", result.StdOut, StringComparison.Ordinal);
        Assert.Contains(originalNull ? "restored=<null>" : "restored=ORIGINAL", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedOldInstallIsRejectedWithoutExecutingOrChangingIt()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
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
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
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
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string package = _package.Path;
        string install = Path.Combine(_root, "owned");
        Assert.Equal(0, RunInstaller(package, install).ExitCode);
        string executable = Path.Combine(install, "aspose-cli.exe");
        string executableHash = Sha256(executable);
        string marker = Path.Combine(install, ".aspose-cli-install.json");
        File.WriteAllText(marker, "{\"schemaVersion\":2,\"schemaVersion\":2}", Encoding.UTF8);
        string sentinel = Path.Combine(install, "CUSTOMER-DO-NOT-DELETE.txt");
        File.WriteAllText(sentinel, "customer-owned", Encoding.UTF8);

        PowerShellResult result = RunInstaller(package, install);

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(File.Exists(sentinel));
        Assert.Equal("customer-owned", File.ReadAllText(sentinel, Encoding.UTF8));
        Assert.Equal(executableHash, Sha256(executable));
        Assert.Contains("duplicate JSON property", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CustomerInstall_RejectsUnsignedPackagesUnlessDevelopmentModeIsExplicit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string customerInstall = Path.Combine(_root, "unsigned-customer");
        PowerShellResult rejected = RunInstaller(
            _package.Path,
            customerInstall,
            developmentPackage: false);

        Assert.NotEqual(0, rejected.ExitCode);
        Assert.Contains("Authenticode signature", rejected.StdErr + rejected.StdOut, StringComparison.Ordinal);
        Assert.False(Directory.Exists(customerInstall));

        string developmentInstall = Path.Combine(_root, "unsigned-development");
        PowerShellResult accepted = RunInstaller(_package.Path, developmentInstall);
        Assert.True(accepted.ExitCode == 0, accepted.StdErr);
        AssertV2Install(developmentInstall);
    }

    [Fact]
    public void CustomerInstall_VerifiesTrustedSignatureBeforeExecutingPayload()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        (string package, string trustRing) = CreateSignedPackage("signed-package");
        string install = Path.Combine(_root, "signed-install");
        var environment = new Dictionary<string, string?>
        {
            ["ASPOSE_CLI_RELEASE_TRUSTED_KEYS"] = trustRing,
        };

        Assert.Equal(0, RunInstaller(_package.Path, install).ExitCode);
        string installed = Snapshot(install);

        PowerShellResult untrusted = RunCustomerPackageTrustValidation(package);
        Assert.NotEqual(0, untrusted.ExitCode);
        Assert.Contains("ASPOSE_CLI_RELEASE_TRUSTED_KEYS", untrusted.StdErr + untrusted.StdOut, StringComparison.Ordinal);
        Assert.Equal(installed, Snapshot(install));

        PowerShellResult accepted = RunCustomerPackageTrustValidation(package, environment);
        Assert.True(accepted.ExitCode == 0, accepted.StdErr);

        File.AppendAllText(Path.Combine(package, "aspose-cli.exe"), "tampered", Encoding.UTF8);
        WriteChecksums(package);
        PowerShellResult rejected = RunCustomerPackageTrustValidation(package, environment);

        Assert.NotEqual(0, rejected.ExitCode);
        Assert.Contains("signature", rejected.StdErr + rejected.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(installed, Snapshot(install));
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("productId")]
    [InlineData("edition")]
    [InlineData("cliVersion")]
    [InlineData("payloadManifest")]
    [InlineData("payloadManifestSha256")]
    public void V2Marker_MissingRequiredPropertyRefusesOwnership(string property)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string install = Path.Combine(_root, "missing-marker-" + property);
        Assert.Equal(0, RunInstaller(_package.Path, install).ExitCode);
        string executable = Path.Combine(install, "aspose-cli.exe");
        string executableHash = Sha256(executable);
        string markerPath = Path.Combine(install, ".aspose-cli-install.json");
        JsonObject marker = JsonNode.Parse(File.ReadAllText(markerPath, Encoding.UTF8))!.AsObject();
        Assert.True(marker.Remove(property));
        File.WriteAllText(markerPath, marker.ToJsonString(), new UTF8Encoding(false));

        PowerShellResult result = RunInstaller(_package.Path, install);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("missing", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(executableHash, Sha256(executable));
    }

    [Fact]
    public void CurrentMarker_RequiresExplicitMcpOwnership()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string install = Path.Combine(_root, "marker-without-mcp");
        Assert.Equal(0, RunInstaller(_package.Path, install).ExitCode);
        string markerPath = Path.Combine(install, ".aspose-cli-install.json");
        JsonObject marker = JsonNode.Parse(File.ReadAllText(markerPath, Encoding.UTF8))!.AsObject();
        Assert.True(marker.Remove("mcpRegistrations"));
        File.WriteAllText(markerPath, marker.ToJsonString(), new UTF8Encoding(false));

        PowerShellResult result = RunInstaller(_package.Path, install);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("mcpRegistrations", result.StdErr + result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentMarker_PowerShellSevenRejectsMissingMcpOwnership()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string? powerShell = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => Path.Combine(path.Trim('"'), "pwsh.exe"))
            .FirstOrDefault(File.Exists);
        if (powerShell is null)
        {
            return;
        }
        string install = Path.Combine(_root, "pwsh7-marker");

        Assert.Equal(0, RunInstaller(_package.Path, install).ExitCode);
        string markerPath = Path.Combine(install, ".aspose-cli-install.json");
        JsonObject marker = JsonNode.Parse(File.ReadAllText(markerPath, Encoding.UTF8))!.AsObject();
        Assert.True(marker.Remove("mcpRegistrations"));
        File.WriteAllText(markerPath, marker.ToJsonString(), new UTF8Encoding(false));
        string installer = Path.Combine(RepositoryPaths.Root, "install.ps1");
        string command = $". {PowerShellLiteral(installer)}; "
            + $"$state = Get-ManagedInstallState {PowerShellLiteral(install)}; "
            + "Write-Output \"$($state.SchemaVersion)|$(@($state.McpRegistrations).Count)\"";
        PowerShellResult result = RunExecutable(
            powerShell,
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", command]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("mcpRegistrations", result.StdErr + result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void V2Marker_RejectsWrongTypesEmptyVersionAndDuplicateMcpOwnership()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        foreach ((string name, Action<JsonObject> mutate) in new (string, Action<JsonObject>)[]
        {
            ("schema-type", marker => marker["schemaVersion"] = "2"),
            ("schema-fraction", marker => marker["schemaVersion"] = 2.5),
            ("empty-version", marker => marker["cliVersion"] = ""),
            ("mcp-type", marker => marker["mcpRegistrations"] = "codex"),
            ("mcp-duplicate", marker => marker["mcpRegistrations"] = new JsonArray("codex", "codex")),
        })
        {
            string install = Path.Combine(_root, "invalid-marker-" + name);
            Assert.Equal(0, RunInstaller(_package.Path, install).ExitCode);
            string markerPath = Path.Combine(install, ".aspose-cli-install.json");
            JsonObject marker = JsonNode.Parse(File.ReadAllText(markerPath, Encoding.UTF8))!.AsObject();
            mutate(marker);
            File.WriteAllText(markerPath, marker.ToJsonString(), new UTF8Encoding(false));

            PowerShellResult result = RunInstaller(_package.Path, install);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("marker", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void OfficialMcpCapture_BoundsOutputAndTerminatesTheProcessTree()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
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

    [Fact]
    public void ValidOwnedInstallWithUnknownFile_IsPreservedAndRejected()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string package = _package.Path;
        string install = Path.Combine(_root, "unknown-file");
        Assert.Equal(0, RunInstaller(package, install).ExitCode);
        string before = Snapshot(install);
        string sentinel = Path.Combine(install, "unknown.bin");
        File.WriteAllText(sentinel, "sentinel", Encoding.UTF8);

        PowerShellResult result = RunInstaller(package, install);

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(File.Exists(sentinel));
        Assert.Equal(before, Snapshot(install, except: "unknown.bin"));
    }

    [Theory]
    [InlineData("prepared")]
    [InlineData("oldMoved")]
    [InlineData("newPublished")]
    public void InjectedTransactionFailure_RestoresOriginalInstall(string phase)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string package = _package.Path;
        string install = Path.Combine(_root, "rollback-" + phase);
        Assert.Equal(0, RunInstaller(package, install).ExitCode);
        string before = Snapshot(install);

        PowerShellResult result = RunInstaller(
            package,
            install,
            new Dictionary<string, string?> { ["ASPOSE_CLI_INSTALL_FAULT"] = phase });

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(before, Snapshot(install));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(install)!, ".aspose-cli-transaction-*.json"));
    }

    [Fact]
    public void InterruptedOldMove_IsRecoveredOnRetryWithoutAUserInterface()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string package = _package.Path;
        string install = Path.Combine(_root, "crash-retry");
        Assert.Equal(0, RunInstaller(package, install).ExitCode);
        string before = Snapshot(install);

        PowerShellResult interrupted = RunInstaller(
            package,
            install,
            new Dictionary<string, string?> { ["ASPOSE_CLI_INSTALL_CRASH"] = "oldMoved" });
        Assert.Equal(97, interrupted.ExitCode);

        PowerShellResult retry = RunInstaller(package, install);
        Assert.True(retry.ExitCode == 0, retry.StdErr);
        AssertV2Install(install);
        Assert.Equal(before, Snapshot(install));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(install)!, ".aspose-cli-transaction-*.json"));
    }

    [Theory]
    [InlineData("oldMovedBeforeJournal")]
    [InlineData("newPublishedBeforeJournal")]
    public void DirectoryMoveBeforePhaseWrite_IsRecoveredFromVerifiedSnapshots(
        string phase)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string package = _package.Path;
        string install = Path.Combine(_root, "pre-journal-" + phase);
        Assert.Equal(0, RunInstaller(package, install).ExitCode);
        string before = Snapshot(install);

        PowerShellResult interrupted = RunInstaller(
            package,
            install,
            new Dictionary<string, string?> { ["ASPOSE_CLI_INSTALL_CRASH"] = phase });
        Assert.Equal(97, interrupted.ExitCode);

        PowerShellResult retry = RunInstaller(package, install);
        Assert.True(retry.ExitCode == 0, retry.StdErr);
        Assert.Equal(before, Snapshot(install));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(install)!, ".aspose-cli-transaction-*.json"));
        Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(install)!, ".aspose-cli-backup-*"));
        Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(install)!, ".aspose-cli-stage-*"));
    }

    [Fact]
    public void InterruptedAfterCommit_CleansVerifiedBackupsOnRetryWithoutRollback()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string package = _package.Path;
        string install = Path.Combine(_root, "committed-crash");

        PowerShellResult interrupted = RunInstaller(
            package,
            install,
            new Dictionary<string, string?> { ["ASPOSE_CLI_INSTALL_CRASH"] = "committed" });
        Assert.Equal(97, interrupted.ExitCode);
        AssertV2Install(install);
        string committed = Snapshot(install);

        PowerShellResult retry = RunInstaller(package, install);
        Assert.True(retry.ExitCode == 0, retry.StdErr);
        Assert.Equal(committed, Snapshot(install));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(install)!, ".aspose-cli-transaction-*.json"));
        Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(install)!, ".aspose-cli-backup-*"));
    }

    [Fact]
    public void PackageAndInstallDirectoriesMayNotOverlap()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string package = _package.Path;

        PowerShellResult result = RunInstaller(package, Path.Combine(package, "installed"));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("may not overlap", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidCommercialLicense_IsRejectedBeforeExistingInstallChanges()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string install = Path.Combine(_root, "invalid-license");
        Assert.Equal(0, RunInstaller(_package.Path, install).ExitCode);
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

    private static PowerShellResult RunInstaller(
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
            string.Join(Environment.NewLine, files.Select(path => $"{Sha256(path)}  {Path.GetFileName(path)}")) + Environment.NewLine,
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
            + "Assert-CustomerPackageTrust $root (Join-Path $root 'SHA256SUMS') $inventory";
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

    private static void AssertV2Install(string install)
    {
        string markerPath = Path.Combine(install, ".aspose-cli-install.json");
        using JsonDocument marker = JsonDocument.Parse(File.ReadAllBytes(markerPath));
        Assert.Equal(2, marker.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("aspose-cli", marker.RootElement.GetProperty("productId").GetString());
        Assert.True(marker.RootElement.GetProperty("mcpRegistrations").GetArrayLength() >= 0);
        string manifestName = marker.RootElement.GetProperty("payloadManifest").GetString()!;
        string manifest = Path.Combine(install, manifestName);
        Assert.Equal(Sha256(manifest), marker.RootElement.GetProperty("payloadManifestSha256").GetString());
    }

    private static PowerShellResult CreateJunction(string junction, string target) =>
        RunExecutable(
            "powershell.exe",
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command",
             $"New-Item -ItemType Junction -Path {PowerShellLiteral(junction)} -Target {PowerShellLiteral(target)} | Out-Null"]);

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
            + $"$before = $script:testPath; $persistedJournal = Read-StrictJson {PowerShellLiteral(journal)} 'test journal'; Restore-TransactionPath $persistedJournal $key; "
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
                    sha256 = Sha256(Path.Combine(root, relative)),
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
                Hash = Sha256(path),
            })
            .Where(item => !string.Equals(item.Path, except, StringComparison.Ordinal))
            .OrderBy(static item => item.Path, StringComparer.Ordinal)
            .Select(static item => item.Path + "|" + item.Hash);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))))
            .ToLowerInvariant();
    }

    private static string Sha256(string path)
    {
        using FileStream input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
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

    private sealed record PowerShellResult(int ExitCode, string StdOut, string StdErr);

    private sealed record UserState(string? Path, string Codex, string Claude, string OpenCode);
}

public sealed class CustomerInstallerPackageFixture : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aspose-installer-package-").FullName;

    public CustomerInstallerPackageFixture()
    {
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
}
