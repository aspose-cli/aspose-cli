<#
.SYNOPSIS
Installs, updates or uninstalls a verified Aspose CLI Windows release for the current user.

.DESCRIPTION
Run beside an extracted release package, it installs that package; run on its own, as
'irm https://github.com/<repository>/releases/latest/download/install.ps1 | iex' does, it first
downloads the latest release and checks it against the SHA-256 its release manifest records.
Every file of the package must match the package's SHA256SUMS.

It adds the installation to the user PATH and installs the Agent Skills for the agent hosts it
finds. It changes no agent host's MCP configuration unless -Mcp asks it to register the MCP
server with the Codex, Claude Code and OpenCode setups it finds.

An installation records the choices it was made with (PATH, Skills, MCP). -Update replaces an
existing installation and replays those choices; -Uninstall removes the installation, its PATH
entry, its pristine Skill copies and the MCP registrations it owns. Every mode is one
transaction under per-user interprocess locks.
#>
[CmdletBinding()]
param(
    [string] $PackageRoot,
    [string] $InstallDirectory,
    [switch] $SkipPath,
    [switch] $SkipSkills,
    [string] $SkillsRoot,
    [string] $LicensePath,
    [string] $LicenseProduct,
    [switch] $SkipLicensePrompt,
    [switch] $Mcp,

    [switch] $Update,

    [switch] $Uninstall,

    [switch] $RemoveConfiguration,

    [switch] $DevelopmentPackage,

    [int] $WaitForProcessId,

    [string] $CleanupRoot,

    [string] $StatusPath
)

$isDotSourced = $MyInvocation.InvocationName -ceq '.'
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
if ($env:OS -cne 'Windows_NT') {
    throw 'This release installer currently supports Windows only.'
}

Add-Type -AssemblyName System.Security
if ($null -eq ('AsposeFileInstaller.NativeMethods' -as [type])) {
    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
namespace AsposeFileInstaller {
    public static class NativeMethods {
        [DllImport("kernel32.dll")]
        public static extern uint GetErrorMode();
        [DllImport("kernel32.dll")]
        public static extern uint SetErrorMode(uint mode);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern System.IntPtr SendMessageTimeout(
            System.IntPtr window, uint message, System.UIntPtr wParam, string lParam,
            uint flags, uint timeout, out System.UIntPtr result);
    }

    public sealed class BoundedWriteStream : System.IO.Stream {
        private readonly System.IO.MemoryStream _inner = new System.IO.MemoryStream();
        private readonly long _maximum;

        public BoundedWriteStream(long maximum) { _maximum = maximum; }
        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { return _inner.Length; } }
        public override long Position { get { return _inner.Position; } set { throw new System.NotSupportedException(); } }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) { throw new System.NotSupportedException(); }
        public override long Seek(long offset, System.IO.SeekOrigin origin) { throw new System.NotSupportedException(); }
        public override void SetLength(long value) { throw new System.NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) {
            if (_inner.Length + count > _maximum) { throw new System.IO.InvalidDataException("Child process output exceeded its limit."); }
            _inner.Write(buffer, offset, count);
        }
        public override System.Threading.Tasks.Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken) {
            try { Write(buffer, offset, count); return System.Threading.Tasks.Task.FromResult(0); }
            catch (System.Exception exception) { return System.Threading.Tasks.Task.FromException(exception); }
        }
        public string GetText() { return System.Text.Encoding.UTF8.GetString(_inner.ToArray()); }
        protected override void Dispose(bool disposing) { if (disposing) { _inner.Dispose(); } base.Dispose(disposing); }
    }

    public sealed class KillOnCloseJob : System.IDisposable {
        private System.IntPtr _handle;

        public KillOnCloseJob() {
            _handle = CreateJobObject(System.IntPtr.Zero, null);
            if (_handle == System.IntPtr.Zero) { return; }
            var information = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            information.BasicLimitInformation.LimitFlags = 0x00002000;
            int length = System.Runtime.InteropServices.Marshal.SizeOf(information);
            System.IntPtr pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(length);
            try {
                System.Runtime.InteropServices.Marshal.StructureToPtr(information, pointer, false);
                if (!SetInformationJobObject(_handle, 9, pointer, (uint)length)) {
                    CloseHandle(_handle); _handle = System.IntPtr.Zero;
                }
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
        }

        public bool TryAssign(System.Diagnostics.Process process) {
            return _handle != System.IntPtr.Zero && AssignProcessToJobObject(_handle, process.Handle);
        }

        public void Dispose() {
            if (_handle != System.IntPtr.Zero) { CloseHandle(_handle); _handle = System.IntPtr.Zero; }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern System.IntPtr CreateJobObject(System.IntPtr securityAttributes, string name);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(System.IntPtr job, int infoClass, System.IntPtr information, uint length);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AssignProcessToJobObject(System.IntPtr job, System.IntPtr process);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool CloseHandle(System.IntPtr handle);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION {
            public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
            public uint LimitFlags;
            public System.UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public System.UIntPtr Affinity;
            public uint PriorityClass, SchedulingClass;
        }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct IO_COUNTERS { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public System.UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
        }
    }

    // Strict JSON with unique object keys, compared as PowerShell compares property names.
    public static class StrictJson {
        private const int MaximumLength = 4 * 1024 * 1024;
        private const int MaximumTokens = 200000;
        private const int MaximumDepth = 64;
        private static readonly System.Text.RegularExpressions.Regex Tokenizer = new System.Text.RegularExpressions.Regex(
            @"\G(?:(?<ws>\s+)|(?<string>""(?:\\[""\\/bfnrt]|\\u[0-9A-Fa-f]{4}|[^""\\\x00-\x1F])*"")|(?<number>-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)|(?<literal>true|false|null)|(?<punct>[{}\[\],:]))",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            System.TimeSpan.FromSeconds(2));

        public static void AssertNoDuplicateProperties(string text, string context) {
            if (text.Length > MaximumLength) { throw Invalid(context, " exceeds the 4 MiB JSON limit."); }
            var tokens = new System.Collections.Generic.List<string>();
            int offset = 0;
            while (offset < text.Length) {
                System.Text.RegularExpressions.Match match = Tokenizer.Match(text, offset);
                if (!match.Success || match.Index != offset) { throw Invalid(context, " is not strict JSON near character " + offset + "."); }
                offset += match.Length;
                if (match.Groups["ws"].Success) { continue; }
                tokens.Add(match.Value);
                if (tokens.Count > MaximumTokens) { throw Invalid(context, " exceeds the JSON token limit."); }
            }
            int position = 0;
            ParseValue(tokens, ref position, 0, context);
            if (position != tokens.Count) { throw Invalid(context, " has trailing JSON tokens."); }
        }

        private static void ParseValue(System.Collections.Generic.List<string> tokens, ref int position, int depth, string context) {
            string token = Next(tokens, ref position, context);
            if (token == "{") { ParseObject(tokens, ref position, depth, context); return; }
            if (token == "[") { ParseArray(tokens, ref position, depth, context); return; }
            if (IsPunctuation(token)) { throw Invalid(context, " contains an invalid JSON value."); }
        }

        private static void ParseObject(System.Collections.Generic.List<string> tokens, ref int position, int depth, string context) {
            if (depth > MaximumDepth) { throw Invalid(context, " exceeds the JSON depth limit."); }
            var names = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (position < tokens.Count && tokens[position] == "}") { position++; return; }
            while (true) {
                string keyToken = Next(tokens, ref position, context);
                if (keyToken[0] != '"') { throw Invalid(context, " contains a non-string object key."); }
                string key = Unescape(keyToken);
                if (!names.Add(key)) { throw Invalid(context, " contains duplicate JSON property '" + key + "'."); }
                if (Next(tokens, ref position, context) != ":") { throw Invalid(context, " is missing ':' after '" + key + "'."); }
                ParseValue(tokens, ref position, depth + 1, context);
                string separator = Next(tokens, ref position, context);
                if (separator == "}") { return; }
                if (separator != ",") { throw Invalid(context, " is missing ',' between object properties."); }
            }
        }

        private static void ParseArray(System.Collections.Generic.List<string> tokens, ref int position, int depth, string context) {
            if (depth > MaximumDepth) { throw Invalid(context, " exceeds the JSON depth limit."); }
            if (position < tokens.Count && tokens[position] == "]") { position++; return; }
            while (true) {
                ParseValue(tokens, ref position, depth + 1, context);
                string separator = Next(tokens, ref position, context);
                if (separator == "]") { return; }
                if (separator != ",") { throw Invalid(context, " is missing ',' between array items."); }
            }
        }

        private static string Next(System.Collections.Generic.List<string> tokens, ref int position, string context) {
            if (position >= tokens.Count) { throw Invalid(context, " ended unexpectedly."); }
            return tokens[position++];
        }

        private static bool IsPunctuation(string token) {
            return token.Length == 1 && "{}[],:".IndexOf(token[0]) >= 0;
        }

        // The tokenizer admitted only valid escapes, so each one decodes directly.
        private static string Unescape(string token) {
            var builder = new System.Text.StringBuilder(token.Length);
            for (int i = 1; i < token.Length - 1; i++) {
                char value = token[i];
                if (value != '\\') { builder.Append(value); continue; }
                char escape = token[++i];
                switch (escape) {
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u': builder.Append((char)System.Convert.ToInt32(token.Substring(i + 1, 4), 16)); i += 4; break;
                    default: builder.Append(escape); break;
                }
            }
            return builder.ToString();
        }

        private static System.IO.InvalidDataException Invalid(string context, string problem) {
            return new System.IO.InvalidDataException(context + problem);
        }
    }
}
'@
}
# Child CLIs inherit this mode. Native/.NET failures remain exit-code and stderr
# events instead of blocking an unattended install behind a Windows crash dialog.
$existingErrorMode = [AsposeFileInstaller.NativeMethods]::GetErrorMode()
[void][AsposeFileInstaller.NativeMethods]::SetErrorMode($existingErrorMode -bor 0x00008003)
$env:DOTNET_EnableCrashReport = '0'

if ($WaitForProcessId -gt 0 -and $WaitForProcessId -ne $PID) {
    while ($true) {
        $parent = $null
        try {
            $parent = [Diagnostics.Process]::GetProcessById($WaitForProcessId)
            if ($parent.HasExited) { break }
            Start-Sleep -Milliseconds 100
        }
        catch [ArgumentException] {
            break
        }
        finally {
            if ($null -ne $parent) { $parent.Dispose() }
        }
    }
}

# <generated-distribution-identity>
$script:ProductId = 'aspose-cli'
$script:CommandName = 'aspose-cli'
$script:DisplayName = 'Aspose CLI'
$script:ExecutableName = 'aspose-cli.exe'
$script:MarkerName = '.aspose-cli-install.json'
$script:PayloadManifestName = '.aspose-cli-payload.json'
$script:BuildManifestName = 'ASPOSE-CLI-BUILD.json'
$script:SkillManifestProductId = 'aspose-cli-skill'
$script:DefaultInstallDirectory = 'Aspose\CLI'
$script:ConfigurationDirectoryName = 'aspose-cli'
$script:ConfigurationOwnerName = '.aspose-cli-config.json'
$script:EnvironmentVariablePrefix = 'ASPOSE_CLI_'
$script:ReleaseRepository = 'aspose-cli/aspose-cli'
$script:Utf8 = [Text.UTF8Encoding]::new($false)
$script:AllowedSkills = @('aspose-cli-platform', 'aspose-cli-cells', 'aspose-cli-pdf', 'aspose-cli-slides', 'aspose-cli-words')
$script:AllowedLicenseProducts = @('cells', 'pdf', 'slides', 'words')
# </generated-distribution-identity>



function Get-StringSha256 {
    param([Parameter(Mandatory)][AllowEmptyString()][string] $Value)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString(
            $algorithm.ComputeHash($script:Utf8.GetBytes($Value)))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Get-FileSha256 {
    param([Parameter(Mandatory)][string] $Path)
    $stream = $null
    $algorithm = $null
    try {
        # Stream the file so hashing does not depend on PowerShell modules or load the file into memory.
        $stream = [IO.FileStream]::new(
            $Path,
            [IO.FileMode]::Open,
            [IO.FileAccess]::Read,
            [IO.FileShare]::Read,
            65536,
            [IO.FileOptions]::SequentialScan)
        $algorithm = [Security.Cryptography.SHA256]::Create()
        return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        if ($null -ne $algorithm) { $algorithm.Dispose() }
        if ($null -ne $stream) { $stream.Dispose() }
    }
}

function ConvertTo-NativeArgument {
    param([AllowEmptyString()][string] $Value)
    if ($Value.IndexOf([char]0) -ge 0 -or $Value.IndexOf("`r") -ge 0 -or $Value.IndexOf("`n") -ge 0) {
        throw 'A child-process argument contains an invalid control character.'
    }
    if ($Value.Length -ne 0 -and $Value -notmatch '[\s"]') { return $Value }

    # Windows CommandLineToArgvW-compatible quoting. Backslashes are doubled
    # only when they precede a quote or the closing quote.
    $builder = [Text.StringBuilder]::new()
    [void]$builder.Append('"')
    $slashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -ceq '\') {
            $slashes++
            continue
        }
        if ($character -ceq '"') {
            [void]$builder.Append(('\' * (($slashes * 2) + 1)))
            [void]$builder.Append('"')
            $slashes = 0
            continue
        }
        if ($slashes -ne 0) {
            [void]$builder.Append(('\' * $slashes))
            $slashes = 0
        }
        [void]$builder.Append($character)
    }
    if ($slashes -ne 0) { [void]$builder.Append(('\' * ($slashes * 2))) }
    [void]$builder.Append('"')
    return $builder.ToString()
}

function Stop-CliChildProcessTree {
    param([Parameter(Mandatory)][Diagnostics.Process] $Process)
    try { if ($Process.HasExited) { return } } catch { return }
    $systemDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::System)
    $taskKill = Join-Path $systemDirectory 'taskkill.exe'
    if (Test-Path -LiteralPath $taskKill -PathType Leaf) {
        $stop = [Diagnostics.ProcessStartInfo]::new()
        $stop.FileName = $taskKill
        $stop.Arguments = "/PID $($Process.Id) /T /F"
        $stop.UseShellExecute = $false
        $stop.CreateNoWindow = $true
        $stop.ErrorDialog = $false
        $stop.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
        $killer = [Diagnostics.Process]::Start($stop)
        if ($null -ne $killer) {
            try {
                if (-not $killer.WaitForExit(10000)) {
                    try { $killer.Kill() } catch { }
                }
            }
            finally { $killer.Dispose() }
        }
    }
    try {
        if (-not $Process.HasExited) { $Process.Kill() }
    }
    catch { }
    try { [void]$Process.WaitForExit(5000) } catch { }
    if (-not $Process.HasExited) {
        throw "Child CLI process tree could not be terminated (pid $($Process.Id))."
    }
}

function Invoke-CliChildProcess {
    param(
        [Parameter(Mandatory)][string] $Executable,
        [Parameter(Mandatory)][string[]] $Arguments,
        [int] $TimeoutSeconds = 60
    )
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable
    $start.Arguments = (@($Arguments | ForEach-Object { ConvertTo-NativeArgument ([string]$_) }) -join ' ')
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.CreateNoWindow = $true
    $start.ErrorDialog = $false
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $process = [Diagnostics.Process]::Start($start)
    if ($null -eq $process) { throw "Child CLI could not start: $Executable" }
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) {
            Stop-CliChildProcessTree $process
        }
        $pipesClosed = [Threading.Tasks.Task]::WaitAll(
            [Threading.Tasks.Task[]]@($stdout, $stderr),
            5000)
        if (-not $pipesClosed) {
            try { $process.StandardOutput.Dispose() } catch { }
            try { $process.StandardError.Dispose() } catch { }
            if (-not $process.HasExited) { Stop-CliChildProcessTree $process }
            throw "Child CLI output pipes did not close within 5 seconds: $Executable"
        }
        if ($timedOut) {
            throw "Child CLI timed out after $TimeoutSeconds seconds and its process tree was terminated: $Executable"
        }
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            StdOut = $stdout.Result
            StdErr = $stderr.Result
        }
    }
    finally { $process.Dispose() }
}

# Without a license argument, the licenses already configured for the user still apply; say
# which products they cover rather than implying there is none.
function Write-LicenseSummary {
    param([Parameter(Mandatory)][string] $Executable)
    $licensed = @()
    try {
        $status = Invoke-CliChildProcess $Executable @('license', 'status', '--output', 'json')
        if ($status.ExitCode -eq 0) {
            $licensed = @(($status.StdOut | ConvertFrom-Json).products |
                Where-Object { [string]$_.mode -ceq 'licensed' } |
                ForEach-Object { [string]$_.product })
        }
    }
    catch { $licensed = @() }
    if ($licensed.Count -gt 0) {
        Write-Host "Licenses already configured for this user apply to: $($licensed -join ', '). Check them with: $($script:CommandName) license status"
    }
    else {
        Write-Host "No license is configured, so the products run in evaluation mode, which marks their output. Install one with: $($script:CommandName) license install <file>"
    }
}

function Get-ChildProcessDiagnostic {
    param([Parameter(Mandatory)] $Result)
    $text = @($Result.StdErr, $Result.StdOut) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    return ($text -join [Environment]::NewLine).Trim()
}

function Assert-ExactProperties {
    param(
        [Parameter(Mandatory)] $Object,
        [Parameter(Mandatory)][string[]] $Names,
        [Parameter(Mandatory)][string] $Context
    )
    $actual = @($Object.PSObject.Properties.Name)
    $unknown = @($actual | Where-Object { $_ -cnotin $Names })
    $missing = @($Names | Where-Object { $_ -cnotin $actual })
    if ($unknown.Count -ne 0 -or $missing.Count -ne 0) {
        throw "$Context has unexpected properties (unknown: [$($unknown -join ', ')], missing: [$($missing -join ', ')])."
    }
}

function Assert-JsonHasNoDuplicateProperties {
    param(
        [Parameter(Mandatory)][string] $Text,
        [Parameter(Mandatory)][string] $Context
    )
    try { [AsposeFileInstaller.StrictJson]::AssertNoDuplicateProperties($Text, $Context) }
    catch { throw $_.Exception.GetBaseException().Message }
}

function Read-StrictJson {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $Context
    )
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "$Context is missing: $Path" }
    $text = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)
    Assert-JsonHasNoDuplicateProperties -Text $text -Context $Context
    try { return $text | ConvertFrom-Json }
    catch { throw "$Context is invalid JSON: $($_.Exception.Message)" }
}

function Test-JsonInteger {
    param($Value, [long] $Expected)
    return (($Value -is [int] -or $Value -is [long]) -and [long]$Value -eq $Expected)
}

function Read-BuildMetadata {
    param([Parameter(Mandatory)][string] $Root)

    $metadata = Read-StrictJson (Join-Path $Root $script:BuildManifestName) 'build manifest'
    Assert-ExactProperties $metadata @(
        'schemaVersion','productId','runtimeIdentifier',
        'sourceRevision','buildDirty','enginePackages') 'build manifest'
    if (-not (Test-JsonInteger $metadata.schemaVersion 1) -or
        $metadata.productId -cne $script:ProductId -or
        $metadata.runtimeIdentifier -cne 'win-x64' -or
        $metadata.sourceRevision -cnotmatch '^[0-9a-f]{40}$' -or
        $metadata.buildDirty -isnot [bool] -or
        ([bool]$metadata.buildDirty -and -not $DevelopmentPackage)) {
        throw 'Build manifest has invalid release provenance.'
    }

    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $engines = @($metadata.enginePackages)
    if ($engines.Count -lt 1 -or $engines.Count -gt 32) { throw 'Build manifest has an invalid SDK package count.' }
    foreach ($package in $engines) {
        Assert-ExactProperties $package @('product','packageId','version','contentHash') 'SDK package'
        $product = [string]$package.product
        if ($product -cnotmatch '^[a-z][a-z0-9-]{0,63}$' -or -not $seen.Add($product) -or
            $package.packageId -cnotmatch '^[A-Za-z0-9_.-]{1,128}$' -or
            $package.version -cnotmatch '^[A-Za-z0-9.+-]{1,128}$' -or
            [Convert]::FromBase64String([string]$package.contentHash).Length -ne 64) {
            throw "Build manifest has invalid SDK provenance for '$product'."
        }
    }
    return $metadata
}

function Assert-CapabilitiesMatchBuildMetadata {
    param(
        [Parameter(Mandatory)] $Capabilities,
        [Parameter(Mandatory)] $Metadata
    )

    $properties = @($Capabilities.PSObject.Properties.Name)
    if ('sourceRevision' -cnotin $properties -or 'buildDirty' -cnotin $properties -or
        $Capabilities.sourceRevision -cne $Metadata.sourceRevision -or
        $Capabilities.buildDirty -isnot [bool] -or
        [bool]$Capabilities.buildDirty -ne [bool]$Metadata.buildDirty) {
        throw 'Executable capabilities do not match the verified build manifest.'
    }

    $provenance = @($Metadata.enginePackages)
    Assert-SetEqual @($provenance | ForEach-Object { [string]$_.product }) @($Capabilities.products | ForEach-Object { [string]$_.id }) 'compiled product graph'
    foreach ($engine in $provenance) {
        $actual = @($Capabilities.products | Where-Object { $_.id -ceq [string]$engine.product })
        if ($actual.Count -ne 1 -or $actual[0].engine.sdkVersion -cne [string]$engine.version) { throw 'Executable engine version does not match the packaged build provenance.' }
        if ($actual[0].engine.sdk -cne [string]$engine.packageId) { throw 'Executable SDK name does not match the packaged build provenance.' }
    }
}

function Assert-SafeRelativePath {
    param([Parameter(Mandatory)][string] $Path)
    $normalized = $Path.Replace('\', '/')
    if ([string]::IsNullOrWhiteSpace($normalized) -or
        [IO.Path]::IsPathRooted($normalized) -or
        $normalized.StartsWith('//', [StringComparison]::Ordinal) -or
        $normalized.Contains(':')) {
        throw "Unsafe relative path '$Path'."
    }
    $reserved = @('CON','PRN','AUX','NUL','COM1','COM2','COM3','COM4','COM5','COM6','COM7','COM8','COM9','LPT1','LPT2','LPT3','LPT4','LPT5','LPT6','LPT7','LPT8','LPT9')
    foreach ($segment in $normalized.Split('/')) {
        $base = $segment.Split('.')[0]
        if ([string]::IsNullOrWhiteSpace($segment) -or
            $segment -in @('.', '..') -or
            $segment.EndsWith(' ') -or
            $segment.EndsWith('.') -or
            $base.ToUpperInvariant() -in $reserved -or
            $segment.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) {
            throw "Unsafe relative path '$Path'."
        }
    }
    return $normalized
}

function Assert-LocalAbsolutePath {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $Name
    )
    $full = [IO.Path]::GetFullPath($Path)
    if ($full.StartsWith('\\', [StringComparison]::Ordinal) -or
        $full.StartsWith('\\?\', [StringComparison]::Ordinal) -or
        $full.StartsWith('\\.\', [StringComparison]::Ordinal)) {
        throw "$Name must be a local non-device path: $full"
    }
    $cursor = $full
    while (-not [string]::IsNullOrEmpty($cursor)) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "$Name may not traverse a reparse point: $($item.FullName)"
            }
        }
        $parent = Split-Path -Parent $cursor
        if ($parent -ceq $cursor) { break }
        $cursor = $parent
    }
    return $full
}

function Test-IsSameOrChildPath {
    param([string] $Path, [string] $Root)
    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    return $fullPath.Equals($fullRoot, [StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith($fullRoot + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Get-RelativePathCompat {
    param([string] $Root, [string] $Path)
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $pathFull = [IO.Path]::GetFullPath($Path)
    $rootUri = [Uri]$rootFull
    $pathUri = [Uri]$pathFull
    return [Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString()).Replace('/', '\')
}

function Get-TreeInventory {
    param([Parameter(Mandatory)][string] $Root)
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { throw "Directory is missing: $Root" }
    $rootItem = Get-Item -LiteralPath $Root -Force
    if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse point is not allowed: $Root" }
    $files = [Collections.Generic.List[object]]::new()
    $directories = [Collections.Generic.List[string]]::new()
    $pending = [Collections.Generic.Queue[IO.DirectoryInfo]]::new()
    $pending.Enqueue([IO.DirectoryInfo]$rootItem)
    while ($pending.Count -ne 0) {
        $current = $pending.Dequeue()
        foreach ($item in $current.EnumerateFileSystemInfos()) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse point is not allowed: $($item.FullName)" }
            $relative = Assert-SafeRelativePath (Get-RelativePathCompat $Root $item.FullName)
            if ($item -is [IO.DirectoryInfo]) {
                $directories.Add($relative)
                $pending.Enqueue($item)
            }
            else {
                $files.Add([pscustomobject]@{
                    Path = $relative
                    FullPath = $item.FullName
                    Size = ([IO.FileInfo]$item).Length
                    Sha256 = Get-FileSha256 $item.FullName
                })
            }
        }
    }
    return [pscustomobject]@{ Files = @($files); Directories = @($directories) }
}

function Get-ProcessesUsingDirectory {
    param([Parameter(Mandatory)][string] $Root)
    $prefix = $Root.TrimEnd('\') + '\'
    $users = @()
    foreach ($process in @(Get-Process -ErrorAction SilentlyContinue)) {
        $path = $null
        try { $path = $process.Path } catch { }
        if (-not [string]::IsNullOrEmpty($path) -and $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            $users += "$($process.ProcessName) (pid $($process.Id))"
        }
    }
    return $users
}

function Move-DirectoryWithRetry {
    param(
        [Parameter(Mandatory)][string] $Source,
        [Parameter(Mandatory)][string] $Destination
    )
    foreach ($attempt in 1..30) {
        try {
            [IO.Directory]::Move($Source, $Destination)
            return
        }
        catch {
            if ($attempt -eq 30) {
                $users = @(Get-ProcessesUsingDirectory $Source)
                $running = if ($users.Count -ne 0) { " Running from it: $($users -join ', ')." } else { '' }
                throw "Directory publication remained blocked after 3 seconds: '$Source' -> '$Destination' ($($_.Exception.Message)).$running Close every program that uses it, including AI agents (Codex, Claude Code, OpenCode) that started '$($script:CommandName) mcp serve' and terminals whose current directory is inside it, then retry."
            }
            Start-Sleep -Milliseconds 100
        }
    }
}

function Remove-DirectoryWithRetry {
    param(
        [Parameter(Mandatory)][string] $Root,
        [bool] $Recursive = $true
    )
    foreach ($attempt in 1..30) {
        try {
            if (-not (Test-Path -LiteralPath $Root)) { return }
            [IO.Directory]::Delete($Root, $Recursive)
            return
        }
        catch {
            if ($attempt -eq 30) {
                throw "Verified directory removal remained blocked after 3 seconds: '$Root' ($($_.Exception.Message))"
            }
            Start-Sleep -Milliseconds 100
        }
    }
}

function Get-ExpectedDirectories {
    param([Parameter(Mandatory)][string[]] $Files)
    $set = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $Files) {
        $directory = [IO.Path]::GetDirectoryName($file.Replace('/', '\'))
        while (-not [string]::IsNullOrEmpty($directory)) {
            [void]$set.Add($directory.Replace('\', '/'))
            $directory = [IO.Path]::GetDirectoryName($directory)
        }
    }
    return @($set)
}

function Assert-SetEqual {
    param([string[]] $Expected, [string[]] $Actual, [string] $Context)
    $expectedSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($item in $Expected) { if (-not $expectedSet.Add($item)) { throw "$Context has duplicate expected path '$item'." } }
    $actualSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($item in $Actual) { if (-not $actualSet.Add($item)) { throw "$Context has case-conflicting path '$item'." } }
    if (-not $expectedSet.SetEquals($actualSet)) {
        $extra = @($actualSet | Where-Object { -not $expectedSet.Contains($_) })
        $missing = @($expectedSet | Where-Object { -not $actualSet.Contains($_) })
        throw "$Context inventory differs (unknown: [$($extra -join ', ')], missing: [$($missing -join ', ')])."
    }
}

function Get-InventorySnapshot {
    param([Parameter(Mandatory)] $Inventory)
    $lines = @($Inventory.Files | Sort-Object Path | ForEach-Object { "$($_.Path)|$($_.Size)|$($_.Sha256)" })
    return Get-StringSha256 ($lines -join "`n")
}

function Invoke-Capabilities {
    param([Parameter(Mandatory)][string] $Executable)
    $result = Invoke-CliChildProcess $Executable @('capabilities','--output','json')
    if ($result.ExitCode -ne 0) { throw "Executable capability validation failed with exit code $($result.ExitCode): $(Get-ChildProcessDiagnostic $result)" }
    $json = $result.StdOut
    if ($json.Length -gt 16MB) { throw 'Executable capabilities output exceeds 16 MiB.' }
    Assert-JsonHasNoDuplicateProperties $json 'executable capabilities output'
    try { return $json | ConvertFrom-Json }
    catch { throw "Executable capabilities output is invalid JSON: $($_.Exception.Message)" }
}



function Assert-InstallChoices {
    param($Choices)
    if ($Choices -isnot [Management.Automation.PSCustomObject]) { throw 'The installation marker has invalid installation choices.' }
    Assert-ExactProperties $Choices @('path','skills','skillsRoot','mcp') 'installation marker choices'
    if ($Choices.path -isnot [bool] -or $Choices.mcp -isnot [bool] -or
        $Choices.skills -isnot [string] -or $Choices.skills -cnotin @('detected-hosts','custom','none') -or
        ($Choices.skills -ceq 'custom' -and ($Choices.skillsRoot -isnot [string] -or $Choices.skillsRoot -cnotmatch '^[A-Za-z]:\\')) -or
        ($Choices.skills -cne 'custom' -and $null -ne $Choices.skillsRoot)) {
        throw 'The installation marker has invalid installation choices.'
    }
    return $Choices
}

function Get-ManagedInstallState {
    param([Parameter(Mandatory)][string] $Root)
    $inventory = Get-TreeInventory $Root
    $buildMetadata = $null
    $markerPath = Join-Path $Root $script:MarkerName
    $marker = Read-StrictJson $markerPath 'installation marker'
    if ('schemaVersion' -cnotin @($marker.PSObject.Properties.Name)) {
        throw 'Installation marker is missing required property schemaVersion.'
    }
    if (-not (Test-JsonInteger $marker.schemaVersion 3)) {
        throw "Installation marker schemaVersion must be an integer with a supported value: '$Root' was installed by a build this installer does not support. Nothing was changed. Move that directory aside, and remove the $($script:ProductId)-* Skill folders it installed, then install again."
    }
    $schemaVersion = [long]$marker.schemaVersion

        if ('mcpRegistrations' -cnotin @($marker.PSObject.Properties.Name)) { throw 'Installation marker is missing mcpRegistrations.' }
        $markerNames = @('schemaVersion','productId','cliVersion','sourceRevision','payloadManifest','payloadManifestSha256','choices','mcpRegistrations')
        Assert-ExactProperties $marker $markerNames 'installation marker'
        if ($marker.productId -isnot [string] -or $marker.productId -cne $script:ProductId -or
            $marker.cliVersion -isnot [string] -or [string]::IsNullOrWhiteSpace($marker.cliVersion) -or
            $marker.cliVersion -cnotmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$' -or
            $marker.sourceRevision -isnot [string] -or $marker.sourceRevision -cnotmatch '^(?:[0-9a-f]{40}|unknown)$' -or
            $marker.payloadManifest -isnot [string] -or $marker.payloadManifest -cne $script:PayloadManifestName -or
            $marker.payloadManifestSha256 -isnot [string] -or $marker.payloadManifestSha256 -cnotmatch '^[0-9a-f]{64}$' -or
            $marker.mcpRegistrations -isnot [Array]) {
            throw 'The installation marker has invalid ownership fields.'
        }
        $choices = Assert-InstallChoices $marker.choices
        $registrations = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($registration in @($marker.mcpRegistrations)) {
            if ($registration -isnot [string] -or $registration -notin @('codex','claude','opencode') -or
                -not $registrations.Add([string]$registration)) {
                throw 'The installation marker contains an invalid or duplicate MCP registration.'
            }
        }
        $payloadManifestPath = Join-Path $Root $script:PayloadManifestName
        if ((Get-FileSha256 $payloadManifestPath) -cne $marker.payloadManifestSha256) { throw 'The payload manifest hash does not match the installation marker.' }
        $manifest = Read-StrictJson $payloadManifestPath 'payload manifest'
        Assert-ExactProperties $manifest @('schemaVersion','productId','files') 'payload manifest'
        if (-not (Test-JsonInteger $manifest.schemaVersion 1) -or $manifest.productId -cne $script:ProductId) { throw 'The payload manifest ownership fields are invalid.' }
        $expectedPayload = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($file in @($manifest.files)) {
            Assert-ExactProperties $file @('path','size','sha256') 'payload manifest file'
            $relative = Assert-SafeRelativePath ([string]$file.path)
            if ([long]$file.size -lt 0 -or [string]$file.sha256 -cnotmatch '^[0-9a-f]{64}$' -or
                $expectedPayload.ContainsKey($relative)) { throw "Invalid or duplicate payload entry '$relative'." }
            $expectedPayload.Add($relative, $file)
        }
        $expected = @($expectedPayload.Keys) + @($script:MarkerName, $script:PayloadManifestName)
        Assert-SetEqual $expected @($inventory.Files.Path) 'managed installation'
        Assert-SetEqual (Get-ExpectedDirectories $expected) @($inventory.Directories) 'managed directories'
        foreach ($relative in $expectedPayload.Keys) {
            $actual = $inventory.Files | Where-Object { $_.Path.Equals($relative, [StringComparison]::OrdinalIgnoreCase) }
            $declared = $expectedPayload[$relative]
            if ($actual.Size -ne [long]$declared.size -or $actual.Sha256 -cne [string]$declared.sha256) { throw "Installed payload '$relative' was modified." }
        }
        if ($expectedPayload.ContainsKey($script:BuildManifestName)) {
            $buildMetadata = Read-BuildMetadata $Root
        }

    return [pscustomobject]@{
        SchemaVersion = $schemaVersion
        CliVersion = [string]$marker.cliVersion
        SourceRevision = [string]$marker.sourceRevision
        Choices = $choices
        Snapshot = Get-InventorySnapshot $inventory
        BuildMetadata = $buildMetadata
        McpRegistrations = @($marker.mcpRegistrations)
    }
}

function Compare-NumericText {
    param([string] $Left, [string] $Right)
    $a = $Left.TrimStart('0')
    $b = $Right.TrimStart('0')
    if ($a.Length -ne $b.Length) { return $a.Length.CompareTo($b.Length) }
    return [Math]::Sign([string]::CompareOrdinal($a, $b))
}

# Semantic Version 2.0 precedence; build metadata does not take part.
function Compare-SemanticVersion {
    param([Parameter(Mandatory)][string] $Left, [Parameter(Mandatory)][string] $Right)
    $pattern = '^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$'
    $parts = foreach ($value in @($Left, $Right)) {
        if ($value -cnotmatch $pattern) { throw "Version '$value' is not a semantic version." }
        ,@($Matches[1], $Matches[2], $Matches[3], $(if ($Matches.ContainsKey(4)) { $Matches[4] } else { '' }))
    }
    for ($index = 0; $index -lt 3; $index++) {
        $order = Compare-NumericText $parts[0][$index] $parts[1][$index]
        if ($order -ne 0) { return $order }
    }
    $leftPre = [string]$parts[0][3]
    $rightPre = [string]$parts[1][3]
    if ($leftPre -ceq $rightPre) { return 0 }
    if ($leftPre -ceq '') { return 1 }
    if ($rightPre -ceq '') { return -1 }
    $leftIds = $leftPre.Split('.')
    $rightIds = $rightPre.Split('.')
    for ($index = 0; $index -lt [Math]::Min($leftIds.Count, $rightIds.Count); $index++) {
        $leftNumeric = $leftIds[$index] -cmatch '^\d+$'
        $rightNumeric = $rightIds[$index] -cmatch '^\d+$'
        $order = if ($leftNumeric -and $rightNumeric) { Compare-NumericText $leftIds[$index] $rightIds[$index] }
            elseif ($leftNumeric) { -1 }
            elseif ($rightNumeric) { 1 }
            else { [Math]::Sign([string]::CompareOrdinal($leftIds[$index], $rightIds[$index])) }
        if ($order -ne 0) { return $order }
    }
    return $leftIds.Count.CompareTo($rightIds.Count)
}

# One rule for installer and updater: a release replaces an installation only when its
# version has higher precedence, or when it is the identical build (a repair). A release
# never replaces a newer build or a different build with the same version.
function Assert-InstallationUpgrade {
    param(
        [Parameter(Mandatory)] $Installed,
        [Parameter(Mandatory)][string] $Version,
        [Parameter(Mandatory)][string] $Revision
    )
    $order = Compare-SemanticVersion $Version $Installed.CliVersion
    if ($order -lt 0) {
        throw "The package version $Version is older than the installed version $($Installed.CliVersion). Downgrades are refused; to roll back deliberately, run install.ps1 -Uninstall, then install the older release."
    }
    if ($order -eq 0 -and -not ($Version -ceq $Installed.CliVersion -and $Revision -ieq $Installed.SourceRevision)) {
        throw "The package ($Version, revision $Revision) has the same version precedence as the installed build ($($Installed.CliVersion), revision $($Installed.SourceRevision)) but is a different build. A new build needs a higher version; to replace it deliberately, run install.ps1 -Uninstall first."
    }
}

function Write-JsonAtomic {
    param([string] $Path, $Value)
    $temporary = "$Path.$([Guid]::NewGuid().ToString('N')).tmp"
    $bytes = $script:Utf8.GetBytes(($Value | ConvertTo-Json -Depth 12) + [Environment]::NewLine)
    $stream = [IO.FileStream]::new(
        $temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None,
        4096, [IO.FileOptions]::WriteThrough)
    try {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally { $stream.Dispose() }
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        $replacementBackup = "$Path.$([Guid]::NewGuid().ToString('N')).replaced"
        [IO.File]::Replace($temporary, $Path, $replacementBackup, $true)
        Remove-Item -LiteralPath $replacementBackup -Force
    }
    else { [IO.File]::Move($temporary, $Path) }
}

function Write-Journal {
    param([string] $Path, $Journal)
    Write-JsonAtomic $Path $Journal
}

function Protect-PathValue {
    param([AllowNull()][string] $Value, [string] $TargetKey)
    $plain = $script:Utf8.GetBytes($(if ($null -eq $Value) { '' } else { $Value }))
    $entropy = $script:Utf8.GetBytes($TargetKey)
    $protected = [Security.Cryptography.ProtectedData]::Protect(
        $plain, $entropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    return [Convert]::ToBase64String($protected)
}

function Unprotect-PathValue {
    param([string] $Value, [string] $TargetKey)
    $plain = [Security.Cryptography.ProtectedData]::Unprotect(
        [Convert]::FromBase64String($Value),
        $script:Utf8.GetBytes($TargetKey),
        [Security.Cryptography.DataProtectionScope]::CurrentUser)
    return $script:Utf8.GetString($plain)
}

# The user PATH is read and written as its raw registry value. The .NET environment API
# expands %VAR% entries on read and always writes REG_SZ, which would permanently freeze
# entries such as %USERPROFILE%\AppData\Local\Microsoft\WindowsApps.
function Get-UserPath {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $false)
    if ($null -eq $key) { return $null }
    try { return $key.GetValue('Path', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) }
    finally { $key.Dispose() }
}

# Writes keep the existing value kind, so restoring the original string also restores the
# original kind. A newly created PATH is REG_EXPAND_SZ, the Windows default for it.
function Set-UserPath {
    param([AllowNull()] $Value)
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Environment')
    try {
        if ($null -eq $Value) { $key.DeleteValue('Path', $false) }
        else {
            $kind = if ($null -eq $key.GetValue('Path')) { [Microsoft.Win32.RegistryValueKind]::ExpandString }
                else { $key.GetValueKind('Path') }
            $key.SetValue('Path', [string]$Value, $kind)
        }
    }
    finally { $key.Dispose() }
    $ignored = [UIntPtr]::Zero
    # HWND_BROADCAST, WM_SETTINGCHANGE, SMTO_ABORTIFHUNG: processes started later see the change.
    [void][AsposeFileInstaller.NativeMethods]::SendMessageTimeout(
        [IntPtr]0xffff, 0x001A, [UIntPtr]::Zero, 'Environment', 0x0002, 5000, [ref]$ignored)
}

# Separates the install-directory entries from every other raw entry. Entries are compared
# after expansion, so a %LOCALAPPDATA%-relative entry counts as the install root.
function Split-UserPath {
    param([AllowNull()][string] $CurrentPath, [Parameter(Mandatory)][string] $InstallRoot)
    $others = @()
    $containsRoot = $false
    foreach ($entry in @(($CurrentPath -split ';'))) {
        $trimmed = $entry.Trim().Trim('"').TrimEnd('\')
        if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }
        $same = $false
        try {
            $expanded = [Environment]::ExpandEnvironmentVariables($trimmed)
            $same = [IO.Path]::GetFullPath($expanded).TrimEnd('\').Equals($InstallRoot.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)
        }
        catch { $same = $false }
        if ($same) { $containsRoot = $true } else { $others += $entry.Trim() }
    }
    return [pscustomobject]@{ Others = @($others); ContainsRoot = $containsRoot }
}

# Keeps every other raw entry and exactly one install-directory entry at the end.
function Get-UpdatedUserPath {
    param([AllowNull()][string] $CurrentPath, [Parameter(Mandatory)][string] $InstallRoot)
    return (@((Split-UserPath $CurrentPath $InstallRoot).Others) + $InstallRoot) -join ';'
}

# Keeps every other raw entry and no install-directory entry.
function Get-UserPathWithoutInstallRoot {
    param([AllowNull()][string] $CurrentPath, [Parameter(Mandatory)][string] $InstallRoot)
    return @((Split-UserPath $CurrentPath $InstallRoot).Others) -join ';'
}

function Set-TransactionalUserPath {
    param([string] $JournalPath, $Journal, [string] $UpdatedPath)
    $Journal.pathState = 'intent'
    $Journal.appliedPathSha256 = Get-StringSha256 $UpdatedPath
    Write-Journal $JournalPath $Journal
    Invoke-TestFault 'pathIntent'
    Set-UserPath $UpdatedPath
    Invoke-TestFault 'pathWrittenBeforeJournal'
    if ((Get-UserPath) -cne $UpdatedPath) { throw 'The user PATH update did not persist exactly.' }
    $Journal.pathState = 'applied'
    Write-Journal $JournalPath $Journal
    Invoke-TestFault 'pathApplied'
}

function Restore-TransactionPath {
    param($Journal, [string] $TargetKey, [string] $InstallRoot)
    if ($Journal.pathState -ceq 'none') { return }
    $currentPath = Get-UserPath
    $originalPath = if ([bool]$Journal.originalPathNull) { $null } else { Unprotect-PathValue ([string]$Journal.originalPath) $TargetKey }
    $isOriginal = if ([bool]$Journal.originalPathNull) { $null -eq $currentPath } else { $currentPath -ceq $originalPath }
    if ((Get-StringSha256 $(if ($null -eq $currentPath) { '' } else { $currentPath })) -ceq $Journal.appliedPathSha256) {
        Set-UserPath $originalPath
        $restoredPath = Get-UserPath
        if (([bool]$Journal.originalPathNull -and $null -ne $restoredPath) -or
            (-not [bool]$Journal.originalPathNull -and $restoredPath -cne $originalPath)) {
            throw 'The original user PATH could not be restored exactly.'
        }
        return
    }
    if ($isOriginal) { return }
    # Another program changed the PATH after this transaction wrote it. Keep that change and
    # return only the install-directory entry to what the original PATH had.
    $shouldContain = (Split-UserPath $originalPath $InstallRoot).ContainsRoot
    $current = Split-UserPath $currentPath $InstallRoot
    if ($current.ContainsRoot -ne $shouldContain) {
        $repaired = if ($shouldContain) { Get-UpdatedUserPath $currentPath $InstallRoot } else { @($current.Others) -join ';' }
        Set-UserPath $repaired
        if ((Get-UserPath) -cne $repaired) { throw 'The user PATH could not be repaired exactly.' }
    }
    Write-Warning 'The user PATH was changed by another program during the interrupted transaction. That change was kept; only the install-directory entry was returned to its original state.'
}

function Get-HostSkillParent {
    param([string] $HostName)
    if ($HostName -cnotin @('codex','claude-code','opencode')) { throw "Unknown Skill host '$HostName'." }
    $profile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile, [Environment+SpecialFolderOption]::DoNotVerify)
    $path = switch ($HostName) {
        'codex' { Join-Path $profile '.agents\skills' }
        'claude-code' { Join-Path $profile '.claude\skills' }
        'opencode' { Join-Path $profile '.config\opencode\skills' }
    }
    return Assert-LocalAbsolutePath $path "Skill host '$HostName' directory"
}

function Get-SkillParent {
    param([string] $TargetName, [string] $CustomRoot)
    if ($TargetName -ceq 'custom') {
        if ([string]::IsNullOrWhiteSpace($CustomRoot)) { throw 'The custom Skill root is not available for transaction recovery.' }
        return Assert-LocalAbsolutePath $CustomRoot 'custom Skill root'
    }
    if (-not [string]::IsNullOrWhiteSpace($CustomRoot)) { throw "Unexpected detected Skill host '$TargetName' in a custom-root transaction." }
    return Get-HostSkillParent $TargetName
}

function Get-SkillTransactionPaths {
    param([string] $TargetName, [string] $CustomRoot, [string] $Skill, [string] $TransactionId)
    if ($Skill -cnotin $script:AllowedSkills -or $TransactionId -cnotmatch '^[0-9a-f]{32}$') {
        throw 'The Skill transaction identity is invalid.'
    }
    $parent = Get-SkillParent $TargetName $CustomRoot
    return [pscustomobject]@{
        Parent = $parent
        Target = Assert-LocalAbsolutePath (Join-Path $parent $Skill) "Skill '$Skill' target"
        Backup = Assert-LocalAbsolutePath (Join-Path $parent ".aspose-skill-install-backup-$TransactionId-$Skill") "Skill '$Skill' backup"
    }
}

function Get-DetectedSkillHosts {
    $profile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile, [Environment+SpecialFolderOption]::DoNotVerify)
    $hosts = @()
    if ((Test-Path -LiteralPath (Join-Path $profile '.codex')) -or (Test-Path -LiteralPath (Join-Path $profile '.agents'))) { $hosts += 'codex' }
    if (Test-Path -LiteralPath (Join-Path $profile '.claude')) { $hosts += 'claude-code' }
    if (Test-Path -LiteralPath (Join-Path $profile '.config\opencode')) { $hosts += 'opencode' }
    return $hosts
}

function Invoke-OfficialMcp {
    param(
        [Parameter(Mandatory)][string] $Executable,
        [Parameter(Mandatory)][string[]] $Arguments,
        [hashtable] $Environment = @{},
        [string] $WorkingDirectory
    )
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable
    $start.Arguments = (@($Arguments | ForEach-Object { ConvertTo-NativeArgument ([string]$_) }) -join ' ')
    if ($WorkingDirectory) { $start.WorkingDirectory = $WorkingDirectory }
    foreach ($name in $Environment.Keys) { $start.EnvironmentVariables[$name] = $Environment[$name] }
    if ([IO.Path]::GetExtension($Executable) -in @('.cmd', '.bat')) {
        # cmd has different escaping from CommandLineToArgvW. Expand private
        # variables once, inside quotes, with AutoRun and delayed expansion off.
        $values = @($Executable) + $Arguments
        $tokens = @()
        for ($index = 0; $index -lt $values.Count; $index++) {
            $value = [string]$values[$index]
            [void](ConvertTo-NativeArgument $value)
            if ($value.Contains('"')) { throw 'MCP command-shim arguments may not contain double quotes.' }
            $name = $script:EnvironmentVariablePrefix + 'MCP_ARGUMENT_' + $index
            $start.EnvironmentVariables[$name] = $value
            $tokens += '"%' + $name + '%"'
        }
        $start.FileName = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) 'cmd.exe'
        $start.Arguments = '/d /v:off /s /c "' + ($tokens -join ' ') + '"'
    }
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.CreateNoWindow = $true
    $start.ErrorDialog = $false
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $process = [Diagnostics.Process]::Start($start)
    if ($null -eq $process) { throw "MCP host CLI could not start: $Executable" }
    $job = [AsposeFileInstaller.KillOnCloseJob]::new()
    [void]$job.TryAssign($process)
    $stdout = [AsposeFileInstaller.BoundedWriteStream]::new(1MB)
    $stderr = [AsposeFileInstaller.BoundedWriteStream]::new(1MB)
    try {
        $stdoutTask = $process.StandardOutput.BaseStream.CopyToAsync($stdout)
        $stderrTask = $process.StandardError.BaseStream.CopyToAsync($stderr)
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        while (-not $process.WaitForExit(100)) {
            if ($stdoutTask.IsFaulted -or $stderrTask.IsFaulted) {
                Stop-CliChildProcessTree $process
                throw "MCP host CLI output exceeded the 1 MiB per-stream limit: $Executable"
            }
            if ([DateTime]::UtcNow -ge $deadline) {
                Stop-CliChildProcessTree $process
                throw "MCP host CLI timed out after 30 seconds and its process tree was terminated: $Executable"
            }
        }
        $pipesClosed = $false
        try {
            $pipesClosed = [Threading.Tasks.Task]::WaitAll(
                [Threading.Tasks.Task[]]@($stdoutTask, $stderrTask),
                5000)
        }
        catch [AggregateException] {
            throw "MCP host CLI output exceeded the 1 MiB per-stream limit: $Executable"
        }
        if (-not $pipesClosed) {
            Stop-CliChildProcessTree $process
            throw "MCP host CLI output pipes did not close within 5 seconds: $Executable"
        }
        if ($stdoutTask.IsFaulted -or $stderrTask.IsFaulted) {
            throw "MCP host CLI output exceeded the 1 MiB per-stream limit: $Executable"
        }
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            StdOut = $stdout.GetText()
            StdErr = $stderr.GetText()
        }
    }
    finally {
        if (-not $process.HasExited) { Stop-CliChildProcessTree $process }
        $job.Dispose()
        $stdout.Dispose()
        $stderr.Dispose()
        $process.Dispose()
    }
}

# Parses host JSON into case-sensitive dictionaries. PowerShell objects merge keys that differ
# only in case, and host configuration files (Claude's in particular) grow far beyond what a
# strict tokenizer should read, so neither ConvertFrom-Json objects nor Read-StrictJson fit.
function ConvertFrom-HostJson {
    param([Parameter(Mandatory)][AllowEmptyString()][string] $Text, [Parameter(Mandatory)][string] $Context)
    try {
        if ($PSVersionTable.PSEdition -ceq 'Core') {
            return ,(ConvertFrom-Json -InputObject $Text -AsHashtable)
        }
        Add-Type -AssemblyName System.Web.Extensions
        $serializer = [Web.Script.Serialization.JavaScriptSerializer]::new()
        $serializer.MaxJsonLength = [int]::MaxValue
        $serializer.RecursionLimit = 256
        return ,$serializer.DeserializeObject($Text)
    }
    catch { throw "$Context is not valid JSON." }
}

function Read-HostJsonFile {
    param([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][string] $Context)
    # The host may be writing its own file; share it fully and never hold it open.
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
        [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
    try {
        if ($stream.Length -gt 256MB) { throw "$Context exceeds its 256 MiB read budget." }
        $reader = [IO.StreamReader]::new($stream, $script:Utf8, $true)
        try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally { $stream.Dispose() }
    return ,(ConvertFrom-HostJson $text $Context)
}

function Get-JsonMember {
    param($Object, [Parameter(Mandatory)][string] $Name)
    # Both parsers' dictionaries expose ContainsKey; Dictionary<,> hides IDictionary.Contains.
    if ($Object -isnot [Collections.IDictionary] -or -not $Object.ContainsKey($Name)) { return $null }
    $value = $Object[$Name]
    if ($null -eq $value) { return $null }
    return ,$value
}

function Test-StdioMcpCommand {
    param($Transport, [string] $InstallExecutable)
    $type = Get-JsonMember $Transport 'type'
    $command = Get-JsonMember $Transport 'command'
    $arguments = Get-JsonMember $Transport 'args'
    return $type -is [string] -and $type -ceq 'stdio' -and
        $command -is [string] -and [string]::Equals($command, $InstallExecutable, [StringComparison]::OrdinalIgnoreCase) -and
        $arguments -is [array] -and $arguments.Count -eq 2 -and
        $arguments[0] -ceq 'mcp' -and $arguments[1] -ceq 'serve'
}

function Get-McpRegistration {
    param([string] $HostName, [string] $Executable, [string] $InstallExecutable)
    if ($HostName -ceq 'opencode') {
        # Query only user-wide configuration; never echo the resolved configuration,
        # which can contain provider credentials unrelated to this installation.
        $environment = @{
            OPENCODE_DISABLE_PROJECT_CONFIG = 'true'
            OPENCODE_CONFIG = ''; OPENCODE_CONFIG_DIR = ''; OPENCODE_CONFIG_CONTENT = ''
        }
        $result = Invoke-OfficialMcp $Executable @('debug','config') `
            -Environment $environment -WorkingDirectory ([IO.Path]::GetTempPath())
        if ($result.ExitCode -ne 0) { throw 'OpenCode configuration could not be queried.' }
        $configuration = ConvertFrom-HostJson $result.StdOut 'OpenCode configuration response'
        if ($configuration -isnot [Collections.IDictionary]) { throw 'OpenCode configuration response is not an object.' }
        $entry = Get-JsonMember (Get-JsonMember $configuration 'mcp') $script:CommandName
        if ($null -eq $entry) { return [pscustomobject]@{ Exists = $false; Matches = $false } }
        $type = Get-JsonMember $entry 'type'
        # Get-JsonMember keeps an array whole, so wrap only a scalar.
        $command = Get-JsonMember $entry 'command'
        $arguments = if ($command -is [array]) { $command } else { @($command) }
        $sameCommand = $type -is [string] -and $type -ceq 'local' -and $arguments.Count -eq 3 -and
            $arguments[0] -is [string] -and [string]::Equals($arguments[0], $InstallExecutable, [StringComparison]::OrdinalIgnoreCase) -and
            $arguments[1] -ceq 'mcp' -and $arguments[2] -ceq 'serve'
        return [pscustomobject]@{ Exists = $true; Matches = [bool]$sameCommand }
    }
    $query = @('mcp','get',$script:CommandName)
    if ($HostName -ceq 'codex') { $query += '--json' }
    $result = Invoke-OfficialMcp $Executable $query -WorkingDirectory ([IO.Path]::GetTempPath())
    if ($result.ExitCode -ne 0) { return [pscustomobject]@{ Exists = $false; Matches = $false } }
    if ($HostName -ceq 'codex') {
        $configuration = ConvertFrom-HostJson $result.StdOut 'Codex MCP response'
        $sameCommand = Test-StdioMcpCommand (Get-JsonMember $configuration 'transport') $InstallExecutable
    }
    else {
        # Claude's get output joins arguments with spaces. Read its documented
        # user configuration to distinguish ['mcp','serve'] from ['mcp serve'].
        $scope = [regex]::Matches($result.StdOut, '(?m)^  Scope: User config \(available in all your projects\)\r?$')
        $sameCommand = $false
        if ($scope.Count -eq 1) {
            $configDirectory = [Environment]::GetEnvironmentVariable('CLAUDE_CONFIG_DIR', 'Process')
            if ([string]::IsNullOrWhiteSpace($configDirectory)) {
                $configDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
            }
            elseif (-not [IO.Path]::IsPathRooted($configDirectory)) {
                $configDirectory = Join-Path ([IO.Path]::GetTempPath()) $configDirectory
            }
            $configuration = Read-HostJsonFile (Join-Path $configDirectory '.claude.json') 'Claude user configuration'
            $entry = Get-JsonMember (Get-JsonMember $configuration 'mcpServers') $script:CommandName
            $sameCommand = Test-StdioMcpCommand $entry $InstallExecutable
        }
    }
    return [pscustomobject]@{ Exists = $true; Matches = [bool]$sameCommand }
}

# Each host's own CLI adds and removes its registration; OpenCode's named, noninteractive add
# writes global configuration itself, preserving JSONC. Do not implement a second host
# configuration writer. OpenCode has no noninteractive remove, so its removal is manual.
function Get-McpHostSpecs {
    param([Parameter(Mandatory)][string] $InstallExecutable)
    $name = $script:CommandName
    return @(
        [pscustomobject]@{ Name = 'codex'; Executable = 'codex'
            Add = @('mcp','add',$name,'--',$InstallExecutable,'mcp','serve'); Remove = @('mcp','remove',$name)
            ManualRemoval = "run 'codex mcp remove $name'" },
        [pscustomobject]@{ Name = 'claude'; Executable = 'claude'
            Add = @('mcp','add',$name,'--scope','user','--',$InstallExecutable,'mcp','serve'); Remove = @('mcp','remove',$name,'--scope','user')
            ManualRemoval = "run 'claude mcp remove $name --scope user'" },
        [pscustomobject]@{ Name = 'opencode'; Executable = 'opencode'
            Add = @('mcp','add',$name,'--',$InstallExecutable,'mcp','serve'); Remove = $null
            ManualRemoval = "remove the '$name' entry from the 'mcp' section of the OpenCode global configuration (~/.config/opencode/opencode.json or opencode.jsonc)" }
    )
}

function Remove-McpRegistration {
    param([Parameter(Mandatory)] $HostSpec, [Parameter(Mandatory)][string] $Executable, [Parameter(Mandatory)][string] $InstallExecutable)
    if ($null -eq $HostSpec.Remove) { return $false }
    try {
        $removal = Invoke-OfficialMcp $Executable ([string[]]$HostSpec.Remove) -WorkingDirectory ([IO.Path]::GetTempPath())
        if ($removal.ExitCode -ne 0) { return $false }
        return -not (Get-McpRegistration $HostSpec.Name $Executable $InstallExecutable).Exists
    }
    catch { return $false }
}

# Returns the hosts whose registration this installation owns afterwards. An entry is owned
# only when this installer created it: an existing entry stays owned while it still matches,
# and a failed query keeps the recorded ownership instead of silently giving it up.
function Register-OwnedMcp {
    param(
        [Parameter(Mandatory)][string] $InstallExecutable,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]] $PreviouslyOwned
    )
    $registered = [Collections.Generic.List[string]]::new()
    foreach ($hostSpec in @(Get-McpHostSpecs $InstallExecutable)) {
        $name = $hostSpec.Name
        $owned = $name -in $PreviouslyOwned
        $command = Get-Command $hostSpec.Executable -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -eq $command) {
            if ($owned) { $registered.Add($name) }
            Write-Warning "MCP host '$name' CLI was not found; registration was skipped."
            continue
        }
        try { $existing = Get-McpRegistration $name $command.Source $InstallExecutable }
        catch {
            if ($owned) { $registered.Add($name) }
            Write-Warning "MCP host '$name' registration could not be queried and was left unchanged ($($_.Exception.Message)); the CLI installation remains valid."
            continue
        }
        if ($existing.Exists) {
            if ($owned -and $existing.Matches) { $registered.Add($name) }
            else { Write-Warning "MCP host '$name' already has a '$($script:CommandName)' registration that is not verified as installer-owned; it was preserved as user-owned." }
            continue
        }
        try { $add = Invoke-OfficialMcp $command.Source ([string[]]$hostSpec.Add) -WorkingDirectory ([IO.Path]::GetTempPath()) }
        catch {
            Write-Warning "MCP host '$name' registration command could not run ($($_.Exception.Message)); registration was skipped."
            continue
        }
        if ($add.ExitCode -ne 0) {
            # The host's own first line of output usually names the fix, such as a broken configuration file.
            $reason = @((Get-ChildProcessDiagnostic $add) -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 3) -join ' '
            Write-Warning "MCP host '$name' registration command failed with exit code $($add.ExitCode)$(if ($reason) { ": $reason" }); registration was skipped."
            continue
        }
        # No entry existed before the add, so whatever is registered now was created here.
        $verified = $false
        try {
            $published = Get-McpRegistration $name $command.Source $InstallExecutable
            $verified = $published.Exists -and $published.Matches
        }
        catch { $verified = $false }
        if ($verified) { $registered.Add($name); continue }
        if (Remove-McpRegistration $hostSpec $command.Source $InstallExecutable) {
            Write-Warning "MCP host '$name' accepted the registration, but it could not be verified, so it was removed again."
        }
        else {
            $registered.Add($name)
            Write-Warning "MCP host '$name' accepted the registration, but it could not be verified or removed again; it is recorded as installer-owned so that uninstall removes it."
        }
    }
    return @($registered)
}

# Removes only registrations this installation owns and that still point at it.
function Unregister-OwnedMcp {
    param(
        [Parameter(Mandatory)][string] $InstallExecutable,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]] $Owned
    )
    foreach ($hostSpec in @(Get-McpHostSpecs $InstallExecutable | Where-Object { $_.Name -in $Owned })) {
        $name = $hostSpec.Name
        $command = Get-Command $hostSpec.Executable -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -eq $command) {
            Write-Warning "MCP host '$name' CLI was not found. To remove this installation's MCP registration, $($hostSpec.ManualRemoval)."
            continue
        }
        try { $existing = Get-McpRegistration $name $command.Source $InstallExecutable }
        catch {
            Write-Warning "MCP host '$name' registration could not be queried ($($_.Exception.Message)). To remove it, $($hostSpec.ManualRemoval)."
            continue
        }
        if (-not $existing.Exists) { continue }
        if (-not $existing.Matches) {
            Write-Warning "MCP host '$name' has a '$($script:CommandName)' registration that no longer points at this installation; it was preserved."
            continue
        }
        if (Remove-McpRegistration $hostSpec $command.Source $InstallExecutable) {
            Write-Host "Removed the '$($script:CommandName)' MCP registration from $name."
        }
        else {
            Write-Warning "MCP host '$name' registration could not be removed automatically. To remove it, $($hostSpec.ManualRemoval)."
        }
    }
}

function Get-SkillContentHash {
    param([string] $Root, [string[]] $RelativePaths)
    # This is a persisted cross-runtime contract shared with
    # SkillCatalog.AggregateHash. PowerShell's default Sort-Object comparison is
    # culture-aware and case-insensitive, so it cannot be used for ownership
    # hashes (for example, it sorts SKILL.md differently from .NET ordinal).
    $orderedPaths = [string[]]@($RelativePaths)
    [Array]::Sort($orderedPaths, [StringComparer]::Ordinal)
    $incremental = [Security.Cryptography.IncrementalHash]::CreateHash(
        [Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        foreach ($relative in $orderedPaths) {
            $incremental.AppendData($script:Utf8.GetBytes($relative))
            $incremental.AppendData([byte[]]@(0))
            $incremental.AppendData([IO.File]::ReadAllBytes(
                (Join-Path $Root $relative.Replace('/','\'))))
        }
        return ([BitConverter]::ToString(
            $incremental.GetHashAndReset())).Replace('-', '').ToLowerInvariant()
    }
    finally { $incremental.Dispose() }
}

function Get-SkillState {
    param([string] $Root, [string] $ExpectedSkill)
    $inventory = Get-TreeInventory $Root
    $manifestPath = Join-Path $Root '.aspose-skill-manifest.json'
    $manifest = Read-StrictJson $manifestPath 'Skill ownership manifest'
    if (-not (Test-JsonInteger $manifest.schemaVersion 2)) {
        throw 'Skill ownership manifest schemaVersion must be an integer with a supported value.'
    }
    $version = [long]$manifest.schemaVersion
    if ($version -eq 2) {
        Assert-ExactProperties $manifest @('schemaVersion','productId','skill','cliVersion','executable','executableSha256','contentSha256','files') 'Skill v2 manifest'
        if ($manifest.productId -cne $script:SkillManifestProductId -or $manifest.skill -cne $ExpectedSkill) { throw 'Skill ownership fields do not match the target.' }
        $expectedFiles = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($file in @($manifest.files)) {
            Assert-ExactProperties $file @('path','size','sha256') 'Skill file manifest'
            $relative = Assert-SafeRelativePath ([string]$file.path)
            if ($expectedFiles.ContainsKey($relative)) { throw "Duplicate Skill path '$relative'." }
            $expectedFiles.Add($relative, $file)
        }
        $expected = @($expectedFiles.Keys) + '.aspose-skill-manifest.json'
        Assert-SetEqual $expected @($inventory.Files.Path) 'managed Skill files'
        Assert-SetEqual (Get-ExpectedDirectories $expected) @($inventory.Directories) 'managed Skill directories'
        foreach ($relative in $expectedFiles.Keys) {
            $actual = $inventory.Files | Where-Object { $_.Path.Equals($relative, [StringComparison]::OrdinalIgnoreCase) }
            $declared = $expectedFiles[$relative]
            if ($actual.Size -ne [long]$declared.size -or $actual.Sha256 -cne [string]$declared.sha256) { throw "Managed Skill file '$relative' was modified." }
        }
        $contentHash = Get-SkillContentHash $Root ([string[]]@($expectedFiles.Keys))
        if ($contentHash -cne [string]$manifest.contentSha256) { throw 'The managed Skill aggregate content hash does not match.' }
    }

    return [pscustomobject]@{
        Version = $version
        Snapshot = Get-InventorySnapshot $inventory
        ExecutableSha256 = [string]$manifest.executableSha256
    }
}

function Remove-VerifiedInstallDirectory {
    param([string] $Root, [string] $Snapshot)
    $state = Get-ManagedInstallState $Root
    if ($state.Snapshot -cne $Snapshot) { throw "Managed installation changed externally and was preserved: $Root" }
    Remove-DirectoryWithRetry $Root $true
}

function Remove-VerifiedSkillDirectory {
    param([string] $Root, [string] $Skill, [string] $Snapshot)
    $state = Get-SkillState $Root $Skill
    if ($state.Snapshot -cne $Snapshot) { throw "Managed Skill changed externally and was preserved: $Root" }
    Remove-DirectoryWithRetry $Root $true
}

function Remove-VerifiedSkillStageParent {
    param([string] $Root)
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { return }
    foreach ($item in @(Get-ChildItem -LiteralPath $Root -Force)) {
        if (-not $item.PSIsContainer -or $item.Name -cnotin $script:AllowedSkills -or
            ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Skill staging contains an unknown entry and was preserved: $($item.FullName)"
        }
        $state = Get-SkillState $item.FullName $item.Name
        Remove-VerifiedSkillDirectory $item.FullName $item.Name $state.Snapshot
    }
    Remove-DirectoryWithRetry $Root $false
}

# Fault injection exists for transaction tests. A release installation never
# honors it: only development packages and dot-sourced test hosts do.
$script:TestFaultsEnabled = $DevelopmentPackage -or $isDotSourced

function Invoke-TestCrash {
    param([string] $Phase)
    if ($script:TestFaultsEnabled -and [Environment]::GetEnvironmentVariable($script:EnvironmentVariablePrefix + 'INSTALL_CRASH') -ceq $Phase) { [Environment]::Exit(97) }
}

function Invoke-TestFault {
    param([string] $Phase)
    Invoke-TestCrash $Phase
    if ($script:TestFaultsEnabled -and [Environment]::GetEnvironmentVariable($script:EnvironmentVariablePrefix + 'INSTALL_FAULT') -ceq $Phase) { throw "Injected installer failure at '$Phase'." }
}

function Get-InvalidJournalMessage {
    param([string] $JournalPath, [string] $Reason)
    return "The installer journal '$JournalPath' is damaged or was written by a different installer version ($Reason). It was preserved and nothing was changed. Compare it with the sibling .$($script:ProductId)-stage-*, .$($script:ProductId)-backup-* and .aspose-skill-install-backup-* directories, keep what you need, move the journal aside, then retry."
}

function Recover-PendingTransaction {
    param(
        [string] $JournalPath,
        [string] $InstallRoot,
        [string] $InstallParent,
        [string] $TargetKey
    )
    if (-not (Test-Path -LiteralPath $JournalPath -PathType Leaf)) { return }
    try {
        $journal = Read-StrictJson $JournalPath 'installer transaction journal'
        Assert-ExactProperties $journal @(
            'schemaVersion','productId','targetKey','transactionId','phase','oldSnapshot','newSnapshot',
            'pathState','originalPath','originalPathNull','appliedPathSha256','customSkillsRoot',
            'customSkillsRootExisted','skills') 'installer transaction journal'
        if (-not (Test-JsonInteger $journal.schemaVersion 5) -or $journal.productId -cne $script:ProductId -or
            $journal.targetKey -cne $TargetKey -or $journal.transactionId -cnotmatch '^[0-9a-f]{32}$' -or
            $journal.phase -cnotin @('prepared','oldMoved','newPublished','committed') -or
            $journal.oldSnapshot -isnot [string] -or $journal.newSnapshot -isnot [string] -or
            $journal.pathState -cnotin @('none','intent','applied') -or
            $journal.originalPathNull -isnot [bool] -or $journal.customSkillsRootExisted -isnot [bool] -or
            $journal.customSkillsRoot -isnot [string] -or
            ($journal.pathState -ceq 'none' -and $journal.appliedPathSha256 -cne '') -or
            ($journal.pathState -cne 'none' -and $journal.appliedPathSha256 -cnotmatch '^[0-9a-f]{64}$')) {
            throw 'its fields are invalid'
        }
        # The journal names its own Skill root, so recovery never depends on the switches
        # of the invocation that happens to find it.
        $CustomSkillsRoot = ''
        if ([string]$journal.customSkillsRoot) {
            $CustomSkillsRoot = Unprotect-PathValue ([string]$journal.customSkillsRoot) $TargetKey
        }
    }
    catch { throw (Get-InvalidJournalMessage $JournalPath $_.Exception.Message) }
    $id = [string]$journal.transactionId
    $stage = Join-Path $InstallParent ".$($script:ProductId)-stage-$id"
    $backup = Join-Path $InstallParent ".$($script:ProductId)-backup-$id"
    $skillStageParent = Join-Path $InstallParent ".$($script:ProductId)-skill-stage-$id"
    if ($journal.phase -ceq 'committed') {
        if ([string]$journal.newSnapshot) {
            $current = Get-ManagedInstallState $InstallRoot
            if ($current.Snapshot -cne $journal.newSnapshot) { throw "Committed installation changed externally; recovery stopped: $InstallRoot" }
        }
        elseif (Test-Path -LiteralPath $InstallRoot) {
            throw "A directory appeared where an uninstalled installation was removed; recovery stopped: $InstallRoot"
        }
        if (Test-Path -LiteralPath $backup -PathType Container) { Remove-VerifiedInstallDirectory $backup ([string]$journal.oldSnapshot) }
        if (Test-Path -LiteralPath $stage -PathType Container) { Remove-VerifiedInstallDirectory $stage ([string]$journal.newSnapshot) }
        foreach ($skillRecord in @($journal.skills)) {
            Assert-ExactProperties $skillRecord @('order','host','skill','oldSnapshot','newSnapshot','status') 'Skill transaction record'
            if ($skillRecord.status -cne 'published') { throw 'Committed journal contains an incomplete Skill record.' }
            $paths = Get-SkillTransactionPaths ([string]$skillRecord.host) $CustomSkillsRoot ([string]$skillRecord.skill) $id
            if (Test-Path -LiteralPath $paths.Backup -PathType Container) {
                $paths = Get-SkillTransactionPaths ([string]$skillRecord.host) $CustomSkillsRoot ([string]$skillRecord.skill) $id
                Remove-VerifiedSkillDirectory $paths.Backup ([string]$skillRecord.skill) ([string]$skillRecord.oldSnapshot)
            }
        }
        Remove-VerifiedSkillStageParent $skillStageParent
        Remove-Item -LiteralPath $JournalPath -Force
        return
    }

    foreach ($skillRecord in @($journal.skills) | Sort-Object -Property order -Descending) {
        Assert-ExactProperties $skillRecord @('order','host','skill','oldSnapshot','newSnapshot','status') 'Skill transaction record'
        if ($skillRecord.host -cnotin @('codex','claude-code','opencode','custom') -or $skillRecord.skill -cnotin $script:AllowedSkills -or
            $skillRecord.status -cnotin @('prepared','published')) { throw 'Installer journal contains an unknown Skill target.' }
        $paths = Get-SkillTransactionPaths ([string]$skillRecord.host) $CustomSkillsRoot ([string]$skillRecord.skill) $id
        $backupExists = Test-Path -LiteralPath $paths.Backup -PathType Container
        $targetIsOld = $false
        if (Test-Path -LiteralPath $paths.Target -PathType Container) {
            $paths = Get-SkillTransactionPaths ([string]$skillRecord.host) $CustomSkillsRoot ([string]$skillRecord.skill) $id
            $targetState = Get-SkillState $paths.Target ([string]$skillRecord.skill)
            if ($skillRecord.status -ceq 'prepared' -and -not $backupExists -and
                $skillRecord.oldSnapshot -and $targetState.Snapshot -ceq $skillRecord.oldSnapshot) {
                $targetIsOld = $true
            }
            elseif ($skillRecord.newSnapshot -and $targetState.Snapshot -ceq $skillRecord.newSnapshot) {
                $paths = Get-SkillTransactionPaths ([string]$skillRecord.host) $CustomSkillsRoot ([string]$skillRecord.skill) $id
                Remove-VerifiedSkillDirectory $paths.Target ([string]$skillRecord.skill) ([string]$skillRecord.newSnapshot)
            }
            elseif ($skillRecord.oldSnapshot -and $targetState.Snapshot -ceq $skillRecord.oldSnapshot) {
                $targetIsOld = $true
            }
            else { throw "Skill target changed externally: $($paths.Target)" }
        }
        $paths = Get-SkillTransactionPaths ([string]$skillRecord.host) $CustomSkillsRoot ([string]$skillRecord.skill) $id
        if (Test-Path -LiteralPath $paths.Backup -PathType Container) {
            $old = Get-SkillState $paths.Backup ([string]$skillRecord.skill)
            if ($old.Snapshot -cne $skillRecord.oldSnapshot) { throw "Skill backup changed externally: $($paths.Backup)" }
            $paths = Get-SkillTransactionPaths ([string]$skillRecord.host) $CustomSkillsRoot ([string]$skillRecord.skill) $id
            if ($targetIsOld) { Remove-VerifiedSkillDirectory $paths.Backup ([string]$skillRecord.skill) ([string]$skillRecord.oldSnapshot) }
            else { Move-DirectoryWithRetry $paths.Backup $paths.Target }
        }
        elseif ($skillRecord.oldSnapshot -and -not $targetIsOld) {
            throw "Skill backup required for recovery is missing: $($paths.Backup)"
        }
    }
    Restore-TransactionPath $journal $TargetKey $InstallRoot
    # Recover from observed snapshots, not only the last journal phase. A
    # process can terminate after an atomic directory move but before the next
    # phase write reaches disk.
    $oldSnapshot = [string]$journal.oldSnapshot
    $newSnapshot = [string]$journal.newSnapshot
    $targetIsOld = $false
    if (Test-Path -LiteralPath $InstallRoot -PathType Container) {
        if (-not $oldSnapshot -and @(Get-ChildItem -LiteralPath $InstallRoot -Force).Count -eq 0) {
            $targetIsOld = $true
        }
        else {
            $targetState = Get-ManagedInstallState $InstallRoot
            if ($oldSnapshot -and $targetState.Snapshot -ceq $oldSnapshot) {
                $targetIsOld = $true
            }
            elseif ($newSnapshot -and $targetState.Snapshot -ceq $newSnapshot) {
                Remove-VerifiedInstallDirectory $InstallRoot $newSnapshot
            }
            else { throw "Installation target changed externally during recovery: $InstallRoot" }
        }
    }
    elseif (Test-Path -LiteralPath $InstallRoot) {
        throw "Installation recovery target is not a directory: $InstallRoot"
    }
    if (Test-Path -LiteralPath $backup -PathType Container) {
        if (-not $oldSnapshot) { throw "Unexpected installation backup was preserved: $backup" }
        $old = Get-ManagedInstallState $backup
        if ($old.Snapshot -cne $oldSnapshot) { throw "Installation backup changed externally: $backup" }
        if ($targetIsOld) {
            # Same-version publication can make old and new snapshots equal.
            # The verified target already represents the required rollback
            # state, so the duplicate verified backup is safe to remove.
            Remove-VerifiedInstallDirectory $backup $oldSnapshot
        }
        else {
            Move-DirectoryWithRetry $backup $InstallRoot
            $targetIsOld = $true
        }
    }
    elseif ($oldSnapshot -and -not $targetIsOld) {
        throw "Installation backup required for recovery is missing: $backup"
    }
    if (Test-Path -LiteralPath $stage -PathType Container) { Remove-VerifiedInstallDirectory $stage $newSnapshot }
    Remove-VerifiedSkillStageParent $skillStageParent
    if (-not [bool]$journal.customSkillsRootExisted -and -not [string]::IsNullOrWhiteSpace($CustomSkillsRoot)) {
        $verifiedCustomRoot = Get-SkillParent 'custom' $CustomSkillsRoot
        if ((Test-Path -LiteralPath $verifiedCustomRoot -PathType Container) -and
            @(Get-ChildItem -LiteralPath $verifiedCustomRoot -Force).Count -eq 0) {
            $verifiedCustomRoot = Get-SkillParent 'custom' $CustomSkillsRoot
            Remove-DirectoryWithRetry $verifiedCustomRoot $false
        }
    }
    Remove-Item -LiteralPath $JournalPath -Force
}

function Resolve-InstallRoot {
    param([AllowEmptyString()][string] $Directory)
    $localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData, [Environment+SpecialFolderOption]::DoNotVerify)
    $requested = if ([string]::IsNullOrWhiteSpace($Directory)) { Join-Path $localAppData $script:DefaultInstallDirectory } else { $Directory }
    $full = Assert-LocalAbsolutePath $requested 'install directory'
    # One spelling per directory: the transaction key, lock, journal and PATH entry must not
    # depend on whether a caller passed a trailing separator.
    $trimmed = $full.TrimEnd('\')
    if ($trimmed.Length -le ([IO.Path]::GetPathRoot($full)).TrimEnd('\').Length) { throw "The install directory may not be a drive root: $full" }
    return $trimmed
}

function Get-TransactionKey {
    param([Parameter(Mandatory)][string] $InstallRoot)
    return (Get-StringSha256 $InstallRoot.ToUpperInvariant()).Substring(0, 16)
}

# Install, update and uninstall share two locks: a per-user mutex for state shared by every
# installation (the user PATH and host registrations), and a per-directory lock file.
function Enter-InstallLock {
    param([Parameter(Mandatory)][string] $InstallRoot)
    $installParent = Split-Path -Parent $InstallRoot
    [IO.Directory]::CreateDirectory($installParent) | Out-Null
    $targetKey = Get-TransactionKey $InstallRoot
    $userIdentity = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $mutex = [Threading.Mutex]::new($false, "Global\AsposeCli.Install.$(Get-StringSha256 $userIdentity)")
    $held = $false
    try { $held = $mutex.WaitOne([TimeSpan]::FromMinutes(5)) }
    catch [Threading.AbandonedMutexException] { $held = $true }
    if (-not $held) { $mutex.Dispose(); throw 'Another installer is updating the current user state. Retry after it completes.' }
    $lockPath = Join-Path $installParent ".$($script:ProductId)-install-$targetKey.lock"
    try { $stream = [IO.FileStream]::new($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
    catch { $mutex.ReleaseMutex(); $mutex.Dispose(); throw "Another installation is using '$InstallRoot'. Retry after it finishes." }
    return [pscustomobject]@{
        InstallRoot = $InstallRoot
        InstallParent = $installParent
        TargetKey = $targetKey
        JournalPath = Join-Path $installParent ".$($script:ProductId)-transaction-$targetKey.json"
        LockPath = $lockPath
        Stream = $stream
        Mutex = $mutex
    }
}

function Exit-InstallLock {
    param($Lock)
    if ($null -eq $Lock) { return }
    $Lock.Stream.Dispose()
    $Lock.Mutex.ReleaseMutex()
    $Lock.Mutex.Dispose()
    try { Remove-Item -LiteralPath $Lock.LockPath -Force -ErrorAction SilentlyContinue } catch { }
}

function Invoke-PendingRecovery {
    param([Parameter(Mandatory)] $Lock)
    try { Recover-PendingTransaction $Lock.JournalPath $Lock.InstallRoot $Lock.InstallParent $Lock.TargetKey }
    catch {
        throw "An interrupted earlier transaction for '$($Lock.InstallRoot)' could not be completed automatically: $($_.Exception.Message) Its journal '$($Lock.JournalPath)' was kept and nothing unverified was deleted; resolve the named path, then retry."
    }
}

function Resolve-CustomSkillsRoot {
    param([AllowEmptyString()][string] $Root)
    if ([string]::IsNullOrWhiteSpace($Root)) { return '' }
    if ($Root -cnotmatch '^[A-Za-z]:[\\/]') {
        throw '-SkillsRoot must be an absolute local fixed-disk directory.'
    }
    $full = (Assert-LocalAbsolutePath $Root 'custom Skill root').TrimEnd('\')
    $drive = [IO.Path]::GetPathRoot($full)
    if ($full.Length -le $drive.TrimEnd('\').Length) {
        throw '-SkillsRoot may not be a drive root.'
    }
    if ([IO.DriveInfo]::new($drive).DriveType -ne [IO.DriveType]::Fixed) {
        throw '-SkillsRoot must be on a fixed local disk.'
    }
    if ((Test-Path -LiteralPath $full) -and -not (Test-Path -LiteralPath $full -PathType Container)) {
        throw "Custom Skill root is not a directory: $full"
    }
    return $full
}

function Assert-SkillsRootPlacement {
    param([AllowEmptyString()][string] $SkillsRoot, [string] $PackageDirectory, [string] $InstallRoot)
    if ([string]::IsNullOrWhiteSpace($SkillsRoot)) { return }
    if ((Test-IsSameOrChildPath $SkillsRoot $PackageDirectory) -or (Test-IsSameOrChildPath $PackageDirectory $SkillsRoot) -or
        (Test-IsSameOrChildPath $SkillsRoot $InstallRoot) -or (Test-IsSameOrChildPath $InstallRoot $SkillsRoot)) {
        throw 'Custom Skill root may not overlap the package or install directory.'
    }
}

# Run without a package beside it, as 'irm .../install.ps1 | iex' does, the installer downloads the
# latest GitHub release: its manifest names the archive, and the archive must have exactly the size
# and SHA-256 the manifest records before it is extracted. The extracted package is then verified
# file by file like any other.
function Get-ReleasePackage {
    param([Parameter(Mandatory)][string] $Root)
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $ProgressPreference = 'SilentlyContinue'
    $latest = "https://github.com/$($script:ReleaseRepository)/releases/latest/download"
    [IO.Directory]::CreateDirectory($Root) | Out-Null
    $manifestPath = Join-Path $Root 'RELEASE-MANIFEST.json'
    Write-Host "Downloading the latest $($script:DisplayName) release from https://github.com/$($script:ReleaseRepository)."
    Invoke-WebRequest -UseBasicParsing -Uri "$latest/RELEASE-MANIFEST.json" -OutFile $manifestPath
    if ((Get-Item -LiteralPath $manifestPath).Length -gt 64KB) { throw 'The release manifest exceeds its 64 KiB limit.' }
    $manifest = Read-StrictJson $manifestPath 'release manifest'
    Assert-ExactProperties $manifest @('schemaVersion','productId','runtimeIdentifier','artifactVersion','sourceRevision','archive') 'release manifest'
    Assert-ExactProperties $manifest.archive @('path','size','sha256') 'release manifest archive'
    $archive = $manifest.archive
    if (-not (Test-JsonInteger $manifest.schemaVersion 1) -or $manifest.productId -cne $script:ProductId -or
        $manifest.runtimeIdentifier -cne 'win-x64' -or
        $archive.path -isnot [string] -or $archive.path -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]*\.zip$' -or
        -not ($archive.size -is [int] -or $archive.size -is [long]) -or $archive.size -le 0 -or $archive.size -gt 1GB -or
        $archive.sha256 -isnot [string] -or $archive.sha256 -cnotmatch '^[0-9a-f]{64}$') {
        throw 'The release manifest is invalid.'
    }
    $archivePath = Join-Path $Root $archive.path
    Write-Host "Downloading $($archive.path)."
    Invoke-WebRequest -UseBasicParsing -Uri "$latest/$($archive.path)" -OutFile $archivePath
    if ((Get-Item -LiteralPath $archivePath).Length -ne [long]$archive.size -or (Get-FileSha256 $archivePath) -cne $archive.sha256) {
        throw "The downloaded $($archive.path) does not match the size and SHA-256 its release manifest records."
    }
    $package = Join-Path $Root 'package'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    # ExtractToDirectory refuses entries that would land outside the package directory.
    [IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $package)
    return $package
}

function Resolve-CleanupRoot {
    param([AllowEmptyString()][string] $Root)
    if ([string]::IsNullOrWhiteSpace($Root)) { return '' }
    $directory = Assert-LocalAbsolutePath $Root 'update staging directory'
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if ((-not $directory.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) -or
        (-not [IO.Path]::GetFileName($directory).StartsWith($script:ProductId + '-update-', [StringComparison]::OrdinalIgnoreCase))) {
        throw "-CleanupRoot must be a $($script:ProductId)-update-* directory below the local temporary directory."
    }
    return $directory
}

# The status file tells a later CLI run how a detached installer run ended. The CLI may have
# written it first (state 'pending', with the versions involved); those fields are kept.
function Start-InstallerStatus {
    param([Parameter(Mandatory)][string] $Path)
    $full = Assert-LocalAbsolutePath $Path 'installer status file'
    if ([IO.Path]::GetExtension($full) -cne '.json' -or -not (Test-Path -LiteralPath (Split-Path -Parent $full) -PathType Container)) {
        throw '-StatusPath must name a .json file in an existing directory.'
    }
    $status = $null
    if (Test-Path -LiteralPath $full -PathType Leaf) {
        try { $status = Read-StrictJson $full 'installer status' } catch { $status = $null }
    }
    if ($status -isnot [Management.Automation.PSCustomObject]) { $status = [pscustomobject]@{} }
    $context = [pscustomobject]@{ Path = $full; Log = [IO.Path]::ChangeExtension($full, '.log'); Status = $status; Transcript = $false }
    Set-InstallerStatus $context 'running' $null
    try {
        Start-Transcript -LiteralPath $context.Log -Force | Out-Null
        $context.Transcript = $true
    }
    catch { Write-Warning "The installer log could not be started at '$($context.Log)': $($_.Exception.Message)" }
    return $context
}

function Set-InstallerStatus {
    # $Message stays untyped: a [string] parameter would turn $null into an empty string.
    param($Context, [string] $State, $Message)
    if ($null -eq $Context) { return }
    $values = [ordered]@{
        schemaVersion = 1
        state = $State
        installerProcessId = $PID
        message = $Message
        log = $Context.Log
        updatedAt = [DateTime]::UtcNow.ToString('o', [Globalization.CultureInfo]::InvariantCulture)
    }
    foreach ($entry in $values.GetEnumerator()) {
        $Context.Status | Add-Member -NotePropertyName $entry.Key -NotePropertyValue $entry.Value -Force
    }
    try { Write-JsonAtomic $Context.Path $Context.Status }
    catch { Write-Warning "The installer status could not be written to '$($Context.Path)': $($_.Exception.Message)" }
}

# Pristine Skill copies that this installation's executable installed at the recorded places.
function Get-OwnedSkillCopies {
    param([Parameter(Mandatory)] $Choices, [Parameter(Mandatory)][string] $ExecutableSha256)
    $customRoot = ''
    $hosts = @()
    if ($Choices.skills -ceq 'custom') { $customRoot = [string]$Choices.skillsRoot; $hosts = @('custom') }
    elseif ($Choices.skills -ceq 'detected-hosts') { $hosts = @('codex','claude-code','opencode') }
    foreach ($hostName in $hosts) {
        $parent = $null
        try { $parent = Get-SkillParent $hostName $customRoot }
        catch { Write-Warning "Skills for '$hostName' were kept: $($_.Exception.Message)"; continue }
        if (-not (Test-Path -LiteralPath $parent -PathType Container)) { continue }
        foreach ($skill in $script:AllowedSkills) {
            $target = Join-Path $parent $skill
            if (-not (Test-Path -LiteralPath $target -PathType Container)) { continue }
            $state = $null
            try { $state = Get-SkillState $target $skill }
            catch { Write-Warning "Kept customized or unmanaged Skill '$skill' for '$hostName': $($_.Exception.Message)"; continue }
            if ($state.ExecutableSha256 -cne $ExecutableSha256) {
                Write-Warning "Kept Skill '$skill' for '$hostName': another $($script:DisplayName) executable installed it."
                continue
            }
            [pscustomobject]@{ Host = $hostName; Skill = $skill; Snapshot = $state.Snapshot }
        }
    }
}

# Removes the CLI configuration directory (settings and installed licenses), only on request
# and only when its ownership marker names this distribution.
function Remove-OwnedConfiguration {
    $configured = [Environment]::GetEnvironmentVariable($script:EnvironmentVariablePrefix + 'CONFIG_DIR', 'Process')
    if ([string]::IsNullOrWhiteSpace($configured)) {
        $appData = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData, [Environment+SpecialFolderOption]::DoNotVerify)
        $configured = Join-Path $appData $script:ConfigurationDirectoryName
    }
    elseif ($configured -cnotmatch '^[A-Za-z]:[\\/]') {
        throw "$($script:EnvironmentVariablePrefix)CONFIG_DIR must be an absolute local directory; the configuration was preserved."
    }
    $directory = Assert-LocalAbsolutePath $configured 'configuration directory'
    if (-not (Test-Path -LiteralPath $directory)) {
        Write-Host "No configuration directory exists at $directory."
        return
    }
    $owner = $null
    try { $owner = Read-StrictJson (Join-Path $directory $script:ConfigurationOwnerName) 'configuration ownership marker' } catch { }
    if ($null -eq $owner -or $owner.productId -cne $script:ProductId) {
        throw "The configuration directory '$directory' is not marked as owned by $($script:DisplayName) and was preserved."
    }
    # The inventory rejects reparse points anywhere in the tree before anything is deleted.
    [void](Get-TreeInventory $directory)
    Remove-DirectoryWithRetry $directory $true
    Write-Host "Removed the configuration directory $directory, including installed licenses."
}

function Install-Release {
    $bound = $script:ScriptParameters
    if ($bound.ContainsKey('LicenseProduct') -and $LicenseProduct -cnotin $script:AllowedLicenseProducts) {
        throw "Unknown license product '$LicenseProduct'. Active products: $($script:AllowedLicenseProducts -join ', ')."
    }
    $skipPath = [bool]$SkipPath
    $skipSkills = [bool]$SkipSkills
    $skipMcp = -not [bool]$Mcp
    $skipLicensePrompt = [bool]$SkipLicensePrompt
    $licensePath = $LicensePath
    if ($Update) {
        foreach ($name in @('SkipPath','SkipSkills','SkillsRoot','Mcp','LicensePath','LicenseProduct')) {
            if ($bound.ContainsKey($name)) { throw "-Update replays the choices recorded by the existing installation and cannot be combined with -$name." }
        }
        $skipLicensePrompt = $true
    }
    elseif ($skipSkills -and -not [string]::IsNullOrWhiteSpace($SkillsRoot)) {
        throw '-SkillsRoot conflicts with -SkipSkills.'
    }
    $customSkillsRoot = ''
    if (-not $Update) { $customSkillsRoot = Resolve-CustomSkillsRoot $SkillsRoot }

    # Resolve and verify the release package before touching customer state.
    $packageDirectory = Assert-LocalAbsolutePath ((Resolve-Path -LiteralPath $PackageRoot).Path) 'package directory'
    $sourceExecutable = Join-Path $packageDirectory $script:ExecutableName
    $checksumPath = Join-Path $packageDirectory 'SHA256SUMS'
    if (-not (Test-Path -LiteralPath $sourceExecutable -PathType Leaf) -or -not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
        throw "Release package must contain $($script:ExecutableName) and SHA256SUMS: $packageDirectory"
    }
    $packageInventory = Get-TreeInventory $packageDirectory
    if ((Get-Item -LiteralPath $checksumPath).Length -gt 1MB) { throw 'SHA256SUMS exceeds its 1 MiB limit.' }
    $checksumBytes = [IO.File]::ReadAllBytes($checksumPath)
    $verifiedFiles = @($packageInventory.Files | Where-Object { $_.Path -cne 'SHA256SUMS' } | Sort-Object Path)
    # install.ps1 is part of the payload, so the installation can update and uninstall itself.
    $checksums = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($line in ($script:Utf8.GetString($checksumBytes) -split "`r?`n")) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -cnotmatch '^([0-9A-Fa-f]{64})\s+\*?(.+)$') { throw "Malformed SHA256SUMS line: $line" }
        $relative = Assert-SafeRelativePath $Matches[2]
        if ($checksums.ContainsKey($relative)) { throw "Duplicate checksum path '$relative'." }
        $checksums.Add($relative, $Matches[1].ToLowerInvariant())
    }
    Assert-SetEqual @($verifiedFiles.Path) @($checksums.Keys) 'package checksum manifest'
    foreach ($payload in $verifiedFiles) {
        if ($payload.Sha256 -cne $checksums[$payload.Path]) { throw "Checksum mismatch for '$($payload.Path)': expected $($checksums[$payload.Path]), got $($payload.Sha256)." }
    }
    $capabilities = Invoke-Capabilities $sourceExecutable
    $releaseIndicators = @('install.ps1',$script:BuildManifestName) |
        Where-Object { $_ -cin @($packageInventory.Files.Path) }
    if (@($releaseIndicators).Count -ne 0) {
        foreach ($required in @('install.ps1',$script:BuildManifestName)) {
            if ($required -cnotin @($packageInventory.Files.Path)) { throw "Release package is missing '$required'." }
        }
        $packageBuildMetadata = Read-BuildMetadata $packageDirectory
        Assert-CapabilitiesMatchBuildMetadata $capabilities $packageBuildMetadata
    }

    $installRoot = Resolve-InstallRoot $InstallDirectory
    if ((Test-IsSameOrChildPath $installRoot $packageDirectory) -or (Test-IsSameOrChildPath $packageDirectory $installRoot)) { throw 'Package and install directories may not overlap.' }
    Assert-SkillsRootPlacement $customSkillsRoot $packageDirectory $installRoot
    $lock = Enter-InstallLock $installRoot
    $installParent = $lock.InstallParent
    $targetKey = $lock.TargetKey
    $journalPath = $lock.JournalPath
    $installExecutable = Join-Path $installRoot $script:ExecutableName

    $transactionId = [Guid]::NewGuid().ToString('N')
    $stage = Join-Path $installParent ".$($script:ProductId)-stage-$transactionId"
    $backup = Join-Path $installParent ".$($script:ProductId)-backup-$transactionId"
    $licenseStage = Join-Path $installParent ".$($script:ProductId)-license-stage-$transactionId"
    $journal = $null
    $licenseFailure = $null
    $existingState = $null
    $oldUserPath = $null
    try {
        Invoke-PendingRecovery $lock
        if (Test-Path -LiteralPath $installRoot -PathType Container) {
            if (@(Get-ChildItem -LiteralPath $installRoot -Force).Count -ne 0) { $existingState = Get-ManagedInstallState $installRoot }
        }
        elseif (Test-Path -LiteralPath $installRoot) { throw "Install target is not a directory: $installRoot" }

        if ($Update) {
            if ($null -eq $existingState) { throw "No managed installation exists at '$installRoot'; -Update requires one. Install it with install.ps1 first." }
            # Replay the choices the installation was made with.
            $choices = $existingState.Choices
            $skipPath = -not [bool]$choices.path
            $skipMcp = -not [bool]$choices.mcp
            $skipSkills = $choices.skills -ceq 'none'
            if ($choices.skills -ceq 'custom') {
                $customSkillsRoot = Resolve-CustomSkillsRoot ([string]$choices.skillsRoot)
                Assert-SkillsRootPlacement $customSkillsRoot $packageDirectory $installRoot
            }
        }
        # Development packages are local builds that may replace any build.
        if ($null -ne $existingState -and -not $DevelopmentPackage) {
            Assert-InstallationUpgrade $existingState ([string]$capabilities.cliVersion) ([string]$capabilities.sourceRevision)
        }

        [IO.Directory]::CreateDirectory($stage) | Out-Null
        foreach ($payload in $verifiedFiles) {
            $destination = Join-Path $stage $payload.Path.Replace('/', '\')
            [IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null
            Copy-Item -LiteralPath $payload.FullPath -Destination $destination
        }
        $payloadManifest = [ordered]@{
            schemaVersion = 1
            productId = $script:ProductId
            files = @($verifiedFiles | ForEach-Object { [ordered]@{ path = $_.Path; size = $_.Size; sha256 = $_.Sha256 } })
        }
        $payloadManifestPath = Join-Path $stage $script:PayloadManifestName
        Write-JsonAtomic $payloadManifestPath $payloadManifest
        $marker = [ordered]@{
            schemaVersion = 3
            productId = $script:ProductId
            cliVersion = [string]$capabilities.cliVersion
            sourceRevision = [string]$capabilities.sourceRevision
            payloadManifest = $script:PayloadManifestName
            payloadManifestSha256 = Get-FileSha256 $payloadManifestPath
            # The choices this installation was made with; -Update and -Uninstall replay them.
            choices = [ordered]@{
                path = -not $skipPath
                skills = $(if ($skipSkills) { 'none' } elseif ($customSkillsRoot) { 'custom' } else { 'detected-hosts' })
                skillsRoot = $(if ($customSkillsRoot) { $customSkillsRoot } else { $null })
                mcp = -not $skipMcp
            }
            mcpRegistrations = @(
                if ($null -ne $existingState) { @($existingState.McpRegistrations) }
            )
        }
        Write-JsonAtomic (Join-Path $stage $script:MarkerName) $marker
        $newState = Get-ManagedInstallState $stage

        if ($LicenseProduct -and [string]::IsNullOrWhiteSpace($licensePath)) { throw '-LicenseProduct requires -LicensePath.' }
        if ([string]::IsNullOrWhiteSpace($licensePath) -and -not $skipLicensePrompt) {
            $licensePath = Read-Host 'Optional Commercial .lic path (press Enter to keep the current license configuration)'
        }
        # License storage belongs to the CLI. The installer validates the license with the staged
        # executable in a throwaway configuration before changing anything, and installs it with the
        # installed executable once the installation is committed.
        $licenseArguments = $null
        if (-not [string]::IsNullOrWhiteSpace($licensePath)) {
            $resolvedLicense = Assert-LocalAbsolutePath ((Resolve-Path -LiteralPath $licensePath).Path) 'license file'
            if (-not (Test-Path -LiteralPath $resolvedLicense -PathType Leaf)) { throw "License file is missing: $resolvedLicense" }
            $licenseArguments = @('license','install',$resolvedLicense)
            if ($LicenseProduct) { $licenseArguments += @('--product',$LicenseProduct) }
            $licenseArguments += @('--output','json')
            [IO.Directory]::CreateDirectory($licenseStage) | Out-Null
            $configurationVariable = $script:EnvironmentVariablePrefix + 'CONFIG_DIR'
            $previousConfig = [Environment]::GetEnvironmentVariable($configurationVariable,'Process')
            [Environment]::SetEnvironmentVariable($configurationVariable,$licenseStage,'Process')
            try {
                $licenseResult = Invoke-CliChildProcess (Join-Path $stage $script:ExecutableName) $licenseArguments
                if ($licenseResult.ExitCode -ne 0) { throw "License validation failed with exit code $($licenseResult.ExitCode): $(Get-ChildProcessDiagnostic $licenseResult)" }
            }
            finally { [Environment]::SetEnvironmentVariable($configurationVariable,$previousConfig,'Process') }
        }

        if ($null -ne $existingState) {
            # The package executable is the caller-selected, checksum-verified
            # authority. Never execute the replaceable old installation merely
            # because its marker and manifest are self-consistent.
            $serviceExecutable = Join-Path $stage $script:ExecutableName
            # One service holds the App and every open document, so one stop ends
            # everything that could still be using the installation.
            $stopResult = Invoke-CliChildProcess $serviceExecutable @('preview','stop','--all','--output','json')
            if ($stopResult.ExitCode -ne 0) { throw "Existing local service could not be stopped safely with exit code $($stopResult.ExitCode): $(Get-ChildProcessDiagnostic $stopResult)" }
            $rechecked = Get-ManagedInstallState $installRoot
            if ($rechecked.Snapshot -cne $existingState.Snapshot) { throw 'Existing installation changed while services were stopping.' }
        }

        $oldUserPath = Get-UserPath
        $journal = [ordered]@{
            schemaVersion = 5
            productId = $script:ProductId
            targetKey = $targetKey
            transactionId = $transactionId
            phase = 'prepared'
            oldSnapshot = $(if ($null -eq $existingState) { '' } else { $existingState.Snapshot })
            newSnapshot = $newState.Snapshot
            pathState = 'none'
            originalPath = Protect-PathValue $oldUserPath $targetKey
            originalPathNull = ($null -eq $oldUserPath)
            appliedPathSha256 = ''
            customSkillsRoot = $(if ($customSkillsRoot) { Protect-PathValue $customSkillsRoot $targetKey } else { '' })
            customSkillsRootExisted = (-not [string]::IsNullOrWhiteSpace($customSkillsRoot) -and (Test-Path -LiteralPath $customSkillsRoot -PathType Container))
            skills = @()
        }
        Write-Journal $journalPath $journal
        Invoke-TestFault 'prepared'
        if ($null -ne $existingState) { Move-DirectoryWithRetry $installRoot $backup }
        elseif (Test-Path -LiteralPath $installRoot -PathType Container) { Remove-DirectoryWithRetry $installRoot $false }
        Invoke-TestFault 'oldMovedBeforeJournal'
        $journal.phase = 'oldMoved'; Write-Journal $journalPath $journal; Invoke-TestFault 'oldMoved'
        Move-DirectoryWithRetry $stage $installRoot
        Invoke-TestFault 'newPublishedBeforeJournal'
        $journal.phase = 'newPublished'; Write-Journal $journalPath $journal; Invoke-TestFault 'newPublished'
        $published = Get-ManagedInstallState $installRoot
        if ($published.Snapshot -cne $newState.Snapshot) { throw 'Published installation failed its payload manifest verification.' }

        if (-not $skipPath) {
            Set-TransactionalUserPath $journalPath $journal (Get-UpdatedUserPath $oldUserPath $installRoot)
        }

        $installedSkills = 0
        if (-not $skipSkills) {
            $skillListResult = Invoke-CliChildProcess $installExecutable @('skill','list','--output','json')
            if ($skillListResult.ExitCode -ne 0) { throw "Bundled Skills could not be listed: $(Get-ChildProcessDiagnostic $skillListResult)" }
            $skillList = $skillListResult.StdOut | ConvertFrom-Json
            $skillTargets = @(
                if (-not [string]::IsNullOrWhiteSpace($customSkillsRoot)) {
                    [pscustomobject]@{ Name = 'custom'; Arguments = @('--target',$customSkillsRoot) }
                }
                else {
                    Get-DetectedSkillHosts | ForEach-Object {
                    [pscustomobject]@{ Name = $_; Arguments = @('--host',$_,'--scope','user') }
                    }
                }
            )
            $skillStageParent = Join-Path $installParent ".$($script:ProductId)-skill-stage-$transactionId"
            $expectedSkills = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
            if ($skillTargets.Count -ne 0) {
                foreach ($skill in @($skillList.skills)) {
                    if ($skill.name -cnotin $script:AllowedSkills) { throw "Executable reported unknown Skill '$($skill.name)'." }
                    $probeResult = Invoke-CliChildProcess $installExecutable @('skill','install',[string]$skill.name,'--target',$skillStageParent,'--output','json')
                    if ($probeResult.ExitCode -ne 0) { throw "Skill '$($skill.name)' could not be staged: $(Get-ChildProcessDiagnostic $probeResult)" }
                    $probeTarget = Join-Path $skillStageParent $skill.name
                    $expectedSkills.Add([string]$skill.name, (Get-SkillState $probeTarget $skill.name))
                }
            }
            foreach ($skillTarget in $skillTargets) {
                $hostName = [string]$skillTarget.Name
                $parent = Get-SkillParent $hostName $customSkillsRoot
                [IO.Directory]::CreateDirectory($parent) | Out-Null
                [void](Get-SkillParent $hostName $customSkillsRoot)
                foreach ($skill in @($skillList.skills)) {
                    if ($skill.name -cnotin $script:AllowedSkills) { throw "Executable reported unknown Skill '$($skill.name)'." }
                    $paths = Get-SkillTransactionPaths $hostName $customSkillsRoot ([string]$skill.name) $transactionId
                    $oldSkill = $null
                    if (Test-Path -LiteralPath $paths.Target -PathType Container) {
                        try { $oldSkill = Get-SkillState $paths.Target $skill.name }
                        catch { Write-Warning "Skipped customized or unmanaged Skill '$($skill.name)' for '$hostName': $($_.Exception.Message)"; continue }
                    }
                    elseif (Test-Path -LiteralPath $paths.Target) { Write-Warning "Skipped Skill '$($skill.name)' because its target is a file: $($paths.Target)"; continue }
                    $skillRecord = [ordered]@{
                        order = $journal.skills.Count
                        host = $hostName
                        skill = $skill.name
                        oldSnapshot = $(if ($null -eq $oldSkill) { '' } else { $oldSkill.Snapshot })
                        newSnapshot = $expectedSkills[$skill.name].Snapshot
                        status = 'prepared'
                    }
                    $journal.skills += $skillRecord
                    Write-Journal $journalPath $journal
                    Invoke-TestFault 'skillPrepared'
                    if ($null -ne $oldSkill) {
                        $paths = Get-SkillTransactionPaths $hostName $customSkillsRoot ([string]$skill.name) $transactionId
                        Copy-Item -LiteralPath $paths.Target -Destination $paths.Backup -Recurse
                    }
                    [void](Get-SkillTransactionPaths $hostName $customSkillsRoot ([string]$skill.name) $transactionId)
                    $skillArguments = @('skill','install',[string]$skill.name) + @($skillTarget.Arguments) + @('--output','json')
                    $skillResult = Invoke-CliChildProcess $installExecutable $skillArguments
                    if ($skillResult.ExitCode -ne 0) { throw "Skill '$($skill.name)' installation failed: $(Get-ChildProcessDiagnostic $skillResult)" }
                    $paths = Get-SkillTransactionPaths $hostName $customSkillsRoot ([string]$skill.name) $transactionId
                    $newSkill = Get-SkillState $paths.Target $skill.name
                    if ($newSkill.Snapshot -cne $expectedSkills[$skill.name].Snapshot) {
                        throw "Skill '$($skill.name)' published content differs from its verified stage."
                    }
                    $skillRecord['status'] = 'published'
                    Write-Journal $journalPath $journal
                    $installedSkills++
                    Invoke-TestFault 'skillUpdated'
                }
            }
            Remove-VerifiedSkillStageParent $skillStageParent
        }

        $final = Get-ManagedInstallState $installRoot
        if ($final.Snapshot -cne $newState.Snapshot -or $final.CliVersion -cne $capabilities.cliVersion) { throw 'Final installed CLI validation failed.' }
        $journal.phase = 'committed'
        Write-Journal $journalPath $journal
        # Once committed, an injected ordinary failure must not report a rollbackable
        # error. A hard-exit hook remains so recovery of committed-but-not-cleaned
        # transactions can be exercised without lying about transaction outcome.
        Invoke-TestCrash 'committed'

        $committedCleanupComplete = $false
        try {
            Invoke-TestFault 'committedCleanup'
            if (Test-Path -LiteralPath $backup -PathType Container) { Remove-VerifiedInstallDirectory $backup $existingState.Snapshot }
            foreach ($skillRecord in @($journal.skills)) {
                $paths = Get-SkillTransactionPaths ([string]$skillRecord.host) $customSkillsRoot ([string]$skillRecord.skill) $transactionId
                if (Test-Path -LiteralPath $paths.Backup -PathType Container) {
                    $paths = Get-SkillTransactionPaths ([string]$skillRecord.host) $customSkillsRoot ([string]$skillRecord.skill) $transactionId
                    Remove-VerifiedSkillDirectory $paths.Backup ([string]$skillRecord.skill) ([string]$skillRecord.oldSnapshot)
                }
            }
            Remove-Item -LiteralPath $journalPath -Force
            $committedCleanupComplete = $true
        }
        catch {
            Write-Warning "The CLI installation is committed, but transaction cleanup remains pending. MCP setup was skipped; retry installation to finish cleanup: $($_.Exception.Message)"
        }

        if ($committedCleanupComplete -and -not $skipMcp) {
            try {
                $mcpRegistrations = @(Register-OwnedMcp $installExecutable @($newState.McpRegistrations))
                $installedMarkerPath = Join-Path $installRoot $script:MarkerName
                $installedMarker = Read-StrictJson $installedMarkerPath 'installation marker'
                $installedMarker.mcpRegistrations = @($mcpRegistrations)
                Write-JsonAtomic $installedMarkerPath $installedMarker
                Invoke-TestFault 'mcpMetadataUpdated'
            }
            catch {
                Write-Warning "Optional MCP setup could not be completed; the CLI installation remains valid: $($_.Exception.Message)"
            }
        }
        if ($null -ne $licenseArguments) {
            $licenseResult = Invoke-CliChildProcess $installExecutable $licenseArguments
            if ($licenseResult.ExitCode -ne 0) {
                $licenseFailure = "The CLI is installed, but the validated license could not be installed (exit code $($licenseResult.ExitCode)): $(Get-ChildProcessDiagnostic $licenseResult) Retry with: $($script:CommandName) $($licenseArguments[0..($licenseArguments.Count - 3)] -join ' ')"
            }
        }
        Write-Host "$($script:DisplayName) $($capabilities.cliVersion) installed to $installRoot"
        if (-not $skipPath) { Write-Host 'The user PATH contains exactly one install-directory entry; restart terminals and AI agents to pick it up.' }
        if ($installedSkills -ne 0) { Write-Host "Installed or updated $installedSkills pristine bundled Agent Skill package(s)." }
        if ($null -eq $licenseArguments -and -not $Update) { Write-LicenseSummary $installExecutable }
    }
    catch {
        $failure = $_
        try {
            if ($null -ne $journal -and (Test-Path -LiteralPath $journalPath -PathType Leaf)) {
                Recover-PendingTransaction $journalPath $installRoot $installParent $targetKey
            }
            elseif (Test-Path -LiteralPath $stage -PathType Container) {
                $stageState = Get-ManagedInstallState $stage
                Remove-VerifiedInstallDirectory $stage $stageState.Snapshot
            }
        }
        catch {
            throw "Installation failed: $($failure.Exception.Message) Recovery also stopped safely: $($_.Exception.Message) Inspect '$journalPath' and sibling stage/backup paths; no unverified tree was deleted."
        }
        throw $failure
    }
    finally {
        if (Test-Path -LiteralPath $licenseStage -PathType Container) {
            try { Remove-DirectoryWithRetry $licenseStage $true } catch { Write-Warning "Temporary validated license staging remains at '$licenseStage'." }
        }
        Exit-InstallLock $lock
    }
    if ($null -ne $licenseFailure) { throw $licenseFailure }
}

function Uninstall-Installation {
    $bound = $script:ScriptParameters
    foreach ($name in @('SkipPath','SkipSkills','SkillsRoot','Mcp','LicensePath','LicenseProduct')) {
        if ($bound.ContainsKey($name)) { throw "-Uninstall cannot be combined with -$name." }
    }
    # The installed copy of this script uninstalls the installation it belongs to.
    $requested = $InstallDirectory
    if (-not $bound.ContainsKey('InstallDirectory') -and -not [string]::IsNullOrWhiteSpace($PSScriptRoot) -and
        (Test-Path -LiteralPath (Join-Path $PSScriptRoot $script:MarkerName) -PathType Leaf)) {
        $requested = $PSScriptRoot
    }
    $installRoot = Resolve-InstallRoot $requested
    if (-not (Test-Path -LiteralPath (Split-Path -Parent $installRoot) -PathType Container)) {
        Write-Host "No $($script:DisplayName) installation exists at $installRoot."
        if ($RemoveConfiguration) { Remove-OwnedConfiguration }
        return
    }
    $lock = Enter-InstallLock $installRoot
    $journalPath = $lock.JournalPath
    $transactionId = [Guid]::NewGuid().ToString('N')
    $backup = Join-Path $lock.InstallParent ".$($script:ProductId)-backup-$transactionId"
    $journal = $null
    try {
        Invoke-PendingRecovery $lock
        if (-not (Test-Path -LiteralPath $installRoot)) {
            Write-Host "No $($script:DisplayName) installation exists at $installRoot."
        }
        elseif (-not (Test-Path -LiteralPath $installRoot -PathType Container)) {
            throw "Install target is not a directory: $installRoot"
        }
        elseif (@(Get-ChildItem -LiteralPath $installRoot -Force).Count -eq 0) {
            Remove-DirectoryWithRetry $installRoot $false
            Write-Host "Removed the empty install directory $installRoot."
        }
        else {
            $state = Get-ManagedInstallState $installRoot
            $executable = Join-Path $installRoot $script:ExecutableName
            # One service holds the App and every open document. The verified installed
            # executable is the only one available to stop it; no package is involved here.
            $stopFailure = $null
            try {
                $stopResult = Invoke-CliChildProcess $executable @('preview','stop','--all','--output','json')
                if ($stopResult.ExitCode -ne 0) { $stopFailure = "exit code $($stopResult.ExitCode): $(Get-ChildProcessDiagnostic $stopResult)" }
            }
            catch { $stopFailure = $_.Exception.Message }
            if ($null -ne $stopFailure) {
                $users = @(Get-ProcessesUsingDirectory $installRoot)
                if ($users.Count -ne 0) {
                    throw "Local services of the installation could not be stopped ($stopFailure). Running from it: $($users -join ', '). Close them, including AI agents that started '$($script:CommandName) mcp serve', then retry."
                }
                Write-Warning "Local services could not be stopped ($stopFailure), but no program runs from the installation; uninstall continues."
            }
            $rechecked = Get-ManagedInstallState $installRoot
            if ($rechecked.Snapshot -cne $state.Snapshot) { throw 'The installation changed while its services were stopping.' }
            $customSkillsRoot = ''
            if ($state.Choices.skills -ceq 'custom') { $customSkillsRoot = [string]$state.Choices.skillsRoot }
            $skillCopies = @(Get-OwnedSkillCopies $state.Choices (Get-FileSha256 $executable))
            $oldUserPath = Get-UserPath
            $journal = [ordered]@{
                schemaVersion = 5
                productId = $script:ProductId
                targetKey = $lock.TargetKey
                transactionId = $transactionId
                phase = 'prepared'
                oldSnapshot = $state.Snapshot
                newSnapshot = ''
                pathState = 'none'
                originalPath = Protect-PathValue $oldUserPath $lock.TargetKey
                originalPathNull = ($null -eq $oldUserPath)
                appliedPathSha256 = ''
                customSkillsRoot = $(if ($customSkillsRoot) { Protect-PathValue $customSkillsRoot $lock.TargetKey } else { '' })
                customSkillsRootExisted = $true
                skills = @()
            }
            Write-Journal $journalPath $journal
            Invoke-TestFault 'prepared'
            Move-DirectoryWithRetry $installRoot $backup
            Invoke-TestFault 'oldMovedBeforeJournal'
            $journal.phase = 'oldMoved'; Write-Journal $journalPath $journal; Invoke-TestFault 'oldMoved'
            # The directory is gone, so a PATH entry that points at it can only be stale.
            if ((Split-UserPath $oldUserPath $installRoot).ContainsRoot) {
                Set-TransactionalUserPath $journalPath $journal (Get-UserPathWithoutInstallRoot $oldUserPath $installRoot)
            }
            foreach ($copy in $skillCopies) {
                $skillRecord = [ordered]@{
                    order = $journal.skills.Count
                    host = $copy.Host
                    skill = $copy.Skill
                    oldSnapshot = $copy.Snapshot
                    newSnapshot = ''
                    status = 'prepared'
                }
                $journal.skills += $skillRecord
                Write-Journal $journalPath $journal
                Invoke-TestFault 'skillPrepared'
                $paths = Get-SkillTransactionPaths $copy.Host $customSkillsRoot $copy.Skill $transactionId
                if ((Get-SkillState $paths.Target $copy.Skill).Snapshot -cne $copy.Snapshot) {
                    throw "Managed Skill changed during uninstall and was preserved: $($paths.Target)"
                }
                Move-DirectoryWithRetry $paths.Target $paths.Backup
                $skillRecord['status'] = 'published'
                Write-Journal $journalPath $journal
                Invoke-TestFault 'skillUpdated'
            }
            $journal.phase = 'committed'
            Write-Journal $journalPath $journal
            Invoke-TestCrash 'committed'
            try {
                Invoke-TestFault 'committedCleanup'
                Remove-VerifiedInstallDirectory $backup $state.Snapshot
                foreach ($skillRecord in @($journal.skills)) {
                    $paths = Get-SkillTransactionPaths ([string]$skillRecord.host) $customSkillsRoot ([string]$skillRecord.skill) $transactionId
                    Remove-VerifiedSkillDirectory $paths.Backup ([string]$skillRecord.skill) ([string]$skillRecord.oldSnapshot)
                }
                Remove-Item -LiteralPath $journalPath -Force
            }
            catch {
                Write-Warning "The uninstall is committed, but removing its backups remains pending; run install.ps1 -Uninstall again to finish: $($_.Exception.Message)"
            }
            # Host registrations are not transactional; they are removed once the removal is final.
            Unregister-OwnedMcp $executable @($state.McpRegistrations)
            Write-Host "$($script:DisplayName) $($state.CliVersion) was removed from $installRoot."
            if ($skillCopies.Count -ne 0) { Write-Host "Removed $($skillCopies.Count) pristine bundled Agent Skill package(s)." }
            Write-Host 'Restart terminals and AI agents so that they stop using the removed installation.'
        }
    }
    catch {
        $failure = $_
        if ($null -ne $journal -and (Test-Path -LiteralPath $journalPath -PathType Leaf)) {
            try { Recover-PendingTransaction $journalPath $installRoot $lock.InstallParent $lock.TargetKey }
            catch {
                throw "Uninstall failed: $($failure.Exception.Message) Recovery also stopped safely: $($_.Exception.Message) Inspect '$journalPath' and sibling backup paths; no unverified tree was deleted."
            }
        }
        throw $failure
    }
    finally { Exit-InstallLock $lock }
    if ($RemoveConfiguration) { Remove-OwnedConfiguration }
}

# Dot-sourcing exposes the ownership primitives to black-box contract tests
# without resolving a package or mutating installation state.
if ($isDotSourced) { return }

$script:ScriptParameters = $PSBoundParameters
$statusContext = $null
if (-not [string]::IsNullOrWhiteSpace($StatusPath)) { $statusContext = Start-InstallerStatus $StatusPath }
$cleanupDirectory = ''
try {
    $cleanupDirectory = Resolve-CleanupRoot $CleanupRoot
    if ($Uninstall) {
        if ($Update) { throw '-Uninstall cannot be combined with -Update.' }
        Uninstall-Installation
    }
    else {
        if ($RemoveConfiguration) { throw '-RemoveConfiguration applies only to -Uninstall.' }
        # The package defaults to the installer's own directory, read here because Windows
        # PowerShell leaves $PSScriptRoot empty in parameter defaults. Without a package there,
        # as under 'irm | iex', the installer downloads the latest release.
        if (-not $PSBoundParameters.ContainsKey('PackageRoot')) {
            $PackageRoot = $PSScriptRoot
            if ([string]::IsNullOrWhiteSpace($PackageRoot) -or -not (Test-Path -LiteralPath (Join-Path $PackageRoot $script:ExecutableName) -PathType Leaf)) {
                if (-not [string]::IsNullOrWhiteSpace($cleanupDirectory)) { throw '-CleanupRoot applies only to an installer run beside its package.' }
                $cleanupDirectory = Join-Path ([IO.Path]::GetTempPath()) ("$($script:ProductId)-install-" + [Guid]::NewGuid().ToString('N'))
                $PackageRoot = Get-ReleasePackage $cleanupDirectory
            }
        }
        Install-Release
    }
    Set-InstallerStatus $statusContext 'succeeded' $null
}
catch {
    Set-InstallerStatus $statusContext 'failed' $_.Exception.Message
    throw
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($cleanupDirectory)) {
        try { Remove-DirectoryWithRetry $cleanupDirectory $true }
        catch { Write-Warning "Temporary update staging remains at '$cleanupDirectory'." }
    }
    if ($null -ne $statusContext -and $statusContext.Transcript) {
        try { Stop-Transcript | Out-Null } catch { }
    }
}
