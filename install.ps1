<#
.SYNOPSIS
Installs or upgrades a verified Aspose CLI Windows release.
#>
[CmdletBinding()]
param(
    [string] $PackageRoot = $PSScriptRoot,
    [string] $InstallDirectory,
    [switch] $SkipPath,
    [switch] $SkipSkills,
    [string] $SkillsRoot,
    [string] $LicensePath,
    [ValidateSet('cells', 'pdf', 'slides', 'words')]
    [string] $LicenseProduct,
    [switch] $SkipLicensePrompt,
    [switch] $SkipMcp,

    [switch] $DevelopmentPackage,

    [int] $WaitForProcessId,

    [string] $CleanupRoot
)

$isDotSourced = $MyInvocation.InvocationName -ceq '.'
if (-not $DevelopmentPackage -and -not $isDotSourced) {
    if ([string]::IsNullOrWhiteSpace($PSCommandPath)) {
        throw 'Customer installation requires a signed install.ps1 file.'
    }
    Import-Module `
        (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Security\Microsoft.PowerShell.Security.psd1') `
        -ErrorAction Stop
    $installerSignature = Get-AuthenticodeSignature -FilePath $PSCommandPath
    if ([string]$installerSignature.Status -cne 'Valid') {
        throw "Customer installer Authenticode signature is not valid ($($installerSignature.Status)). Run the released script with ExecutionPolicy AllSigned, or use scripts/install-local.ps1 for a source build."
    }
}

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

    public static class ReleaseSignature {
        private static readonly byte[] P256SpkiPrefix = new byte[] {
            0x30,0x59,0x30,0x13,0x06,0x07,0x2A,0x86,0x48,0xCE,0x3D,0x02,0x01,
            0x06,0x08,0x2A,0x86,0x48,0xCE,0x3D,0x03,0x01,0x07,0x03,0x42,0x00,0x04
        };

        public static string GetKeyId(string pem) {
            byte[] spki = DecodePublicKey(pem);
            using (var sha = System.Security.Cryptography.SHA256.Create()) {
                return System.BitConverter.ToString(sha.ComputeHash(spki)).Replace("-", "").ToLowerInvariant();
            }
        }

        public static bool Verify(string pem, byte[] content, byte[] derSignature) {
            byte[] spki = DecodePublicKey(pem);
            byte[] blob = new byte[72];
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(0x31534345), 0, blob, 0, 4);
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(32), 0, blob, 4, 4);
            System.Buffer.BlockCopy(spki, P256SpkiPrefix.Length, blob, 8, 64);
            using (var key = System.Security.Cryptography.CngKey.Import(blob, System.Security.Cryptography.CngKeyBlobFormat.EccPublicBlob))
            using (var verifier = new System.Security.Cryptography.ECDsaCng(key)) {
                return verifier.VerifyData(content, DerToP1363(derSignature), System.Security.Cryptography.HashAlgorithmName.SHA256);
            }
        }

        private static byte[] DecodePublicKey(string pem) {
            const string begin = "-----BEGIN PUBLIC KEY-----";
            const string end = "-----END PUBLIC KEY-----";
            if (pem == null || !pem.Contains(begin) || !pem.Contains(end)) { throw new System.Security.Cryptography.CryptographicException("Release public key PEM is invalid."); }
            string value = pem.Replace(begin, "").Replace(end, "").Replace("\r", "").Replace("\n", "").Replace(" ", "").Replace("\t", "");
            byte[] spki = System.Convert.FromBase64String(value);
            if (spki.Length != P256SpkiPrefix.Length + 64) { throw new System.Security.Cryptography.CryptographicException("Release public key is not ECDSA P-256."); }
            for (int i = 0; i != P256SpkiPrefix.Length; i++) {
                if (spki[i] != P256SpkiPrefix[i]) { throw new System.Security.Cryptography.CryptographicException("Release public key is not ECDSA P-256."); }
            }
            return spki;
        }

        private static byte[] DerToP1363(byte[] value) {
            int offset = 0;
            if (ReadByte(value, ref offset) != 0x30) { throw new System.Security.Cryptography.CryptographicException("Release signature DER is invalid."); }
            int sequenceLength = ReadLength(value, ref offset);
            if (sequenceLength != value.Length - offset) { throw new System.Security.Cryptography.CryptographicException("Release signature DER is invalid."); }
            byte[] result = new byte[64];
            ReadInteger(value, ref offset, result, 0);
            ReadInteger(value, ref offset, result, 32);
            if (offset != value.Length) { throw new System.Security.Cryptography.CryptographicException("Release signature DER is invalid."); }
            return result;
        }

        private static void ReadInteger(byte[] value, ref int offset, byte[] target, int targetOffset) {
            if (ReadByte(value, ref offset) != 0x02) { throw new System.Security.Cryptography.CryptographicException("Release signature DER is invalid."); }
            int length = ReadLength(value, ref offset);
            if (length < 1 || length > 33 || offset + length > value.Length || (value[offset] & 0x80) != 0) { throw new System.Security.Cryptography.CryptographicException("Release signature DER is invalid."); }
            if (length > 1 && value[offset] == 0 && (value[offset + 1] & 0x80) == 0) { throw new System.Security.Cryptography.CryptographicException("Release signature DER is invalid."); }
            if (length == 33) {
                if (value[offset] != 0) { throw new System.Security.Cryptography.CryptographicException("Release signature DER is invalid."); }
                offset++; length--;
            }
            System.Buffer.BlockCopy(value, offset, target, targetOffset + 32 - length, length);
            offset += length;
        }

        private static int ReadLength(byte[] value, ref int offset) {
            int length = ReadByte(value, ref offset);
            if (length < 0x80) { return length; }
            if (length != 0x81) { throw new System.Security.Cryptography.CryptographicException("Release signature DER is invalid."); }
            length = ReadByte(value, ref offset);
            if (length < 0x80) { throw new System.Security.Cryptography.CryptographicException("Release signature DER is invalid."); }
            return length;
        }

        private static int ReadByte(byte[] value, ref int offset) {
            if (offset >= value.Length) { throw new System.Security.Cryptography.CryptographicException("Release signature DER is invalid."); }
            return value[offset++];
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
$script:MarkerName = '.aspose-cli-install.json'
$script:PayloadManifestName = '.aspose-cli-payload.json'
$script:BuildManifestName = 'ASPOSE-CLI-BUILD.json'
$script:PackageSignatureManifestName = 'PACKAGE-SIGNATURE.json'
$script:PackageSignatureName = 'PACKAGE-SIGNATURE.sig'
$script:Utf8 = [Text.UTF8Encoding]::new($false)
$script:AllowedEditions = @('commercial')
$script:AllowedSkills = @('aspose-cli-cells', 'aspose-cli-pdf', 'aspose-cli-slides', 'aspose-cli-words')
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
    if ($Text.Length -gt 4MB) {
        throw "$Context exceeds the 4 MiB JSON limit."
    }
    $pattern = '\G(?:(?<ws>\s+)|(?<string>"(?:\\["\\/bfnrt]|\\u[0-9A-Fa-f]{4}|[^"\\\x00-\x1F])*")|(?<number>-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)|(?<literal>true|false|null)|(?<punct>[{}\[\],:]))'
    $tokenizer = [Text.RegularExpressions.Regex]::new(
        $pattern,
        [Text.RegularExpressions.RegexOptions]::CultureInvariant,
        [TimeSpan]::FromSeconds(2))
    $tokens = [Collections.Generic.List[object]]::new()
    $offset = 0
    while ($offset -lt $Text.Length) {
        $match = $tokenizer.Match($Text, $offset)
        if (-not $match.Success -or $match.Index -ne $offset) {
            throw "$Context is not strict JSON near character $offset."
        }
        $offset += $match.Length
        if (-not $match.Groups['ws'].Success) {
            $kind = if ($match.Groups['string'].Success) { 'string' }
                elseif ($match.Groups['number'].Success) { 'scalar' }
                elseif ($match.Groups['literal'].Success) { 'scalar' }
                else { 'punct' }
            $tokens.Add([pscustomobject]@{ Kind = $kind; Text = $match.Value })
            if ($tokens.Count -gt 200000) {
                throw "$Context exceeds the JSON token limit."
            }
        }
    }

    $state = [pscustomobject]@{ Position = 0 }
    $parseValue = $null
    $parseObject = $null
    $parseArray = $null
    $next = {
        if ($state.Position -ge $tokens.Count) { throw "$Context ended unexpectedly." }
        $token = $tokens[$state.Position]
        $state.Position++
        return $token
    }
    $peek = {
        if ($state.Position -ge $tokens.Count) { return $null }
        return $tokens[$state.Position]
    }
    $parseObject = {
        param([int] $Depth)
        if ($Depth -gt 64) { throw "$Context exceeds the JSON depth limit." }
        $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $candidate = & $peek
        if ($null -ne $candidate -and $candidate.Text -ceq '}') { [void](& $next); return }
        while ($true) {
            $keyToken = & $next
            if ($keyToken.Kind -cne 'string') { throw "$Context contains a non-string object key." }
            $key = $keyToken.Text | ConvertFrom-Json
            if (-not $names.Add([string]$key)) { throw "$Context contains duplicate JSON property '$key'." }
            if ((& $next).Text -cne ':') { throw "$Context is missing ':' after '$key'." }
            & $parseValue ($Depth + 1)
            $separator = & $next
            if ($separator.Text -ceq '}') { return }
            if ($separator.Text -cne ',') { throw "$Context is missing ',' between object properties." }
        }
    }
    $parseArray = {
        param([int] $Depth)
        if ($Depth -gt 64) { throw "$Context exceeds the JSON depth limit." }
        $candidate = & $peek
        if ($null -ne $candidate -and $candidate.Text -ceq ']') { [void](& $next); return }
        while ($true) {
            & $parseValue ($Depth + 1)
            $separator = & $next
            if ($separator.Text -ceq ']') { return }
            if ($separator.Text -cne ',') { throw "$Context is missing ',' between array items." }
        }
    }
    $parseValue = {
        param([int] $Depth)
        $token = & $next
        if ($token.Text -ceq '{') { & $parseObject $Depth; return }
        if ($token.Text -ceq '[') { & $parseArray $Depth; return }
        if ($token.Kind -in @('string', 'scalar')) { return }
        throw "$Context contains an invalid JSON value."
    }
    & $parseValue 0
    if ($state.Position -ne $tokens.Count) { throw "$Context has trailing JSON tokens." }
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
        'schemaVersion','productId','edition','runtimeIdentifier',
        'sourceRevision','buildDirty','enginePackages') 'build manifest'
    if (-not (Test-JsonInteger $metadata.schemaVersion 1) -or
        $metadata.productId -cne $script:ProductId -or
        $metadata.edition -cnotin $script:AllowedEditions -or
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
        $Capabilities.edition -cne $Metadata.edition -or
        $Capabilities.sourceRevision -cne $Metadata.sourceRevision -or
        $Capabilities.buildDirty -isnot [bool] -or
        [bool]$Capabilities.buildDirty -ne [bool]$Metadata.buildDirty) {
        throw 'Executable capabilities do not match the verified build manifest.'
    }

    $provenance = @($Metadata.enginePackages)
    Assert-SetEqual @($provenance | ForEach-Object { [string]$_.product }) @($Capabilities.products | ForEach-Object { [string]$_.id }) 'compiled product graph'
    foreach ($engine in $provenance) {
        $actual = @($Capabilities.products | Where-Object { $_.id -ceq [string]$engine.product })
        if ($actual.Count -ne 1 -or $actual[0].engine.sdkVersion -cne [string]$engine.version) { throw 'Executable engine version does not match the signed build provenance.' }
        if ($actual[0].engine.sdk -cne [string]$engine.packageId) { throw 'Executable SDK name does not match the signed build provenance.' }
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

function Assert-CustomerPackageTrust {
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][string] $ChecksumPath,
        [Parameter(Mandatory)] $Inventory,
        [switch] $Development
    )
    $manifestPath = Join-Path $Root $script:PackageSignatureManifestName
    $signaturePath = Join-Path $Root $script:PackageSignatureName
    if ($Development) {
        if ((Test-Path -LiteralPath $manifestPath) -or (Test-Path -LiteralPath $signaturePath)) {
            throw 'Development package mode cannot bypass a customer package signature.'
        }
        return
    }
    foreach ($required in @($script:PackageSignatureManifestName, $script:PackageSignatureName)) {
        if ($required -cnotin @($Inventory.Files.Path)) {
            throw "Customer release package is missing '$required'. Use scripts/install-local.ps1 for unsigned local development builds."
        }
    }

    $manifest = Read-StrictJson $manifestPath 'customer package signature manifest'
    Assert-ExactProperties $manifest @('schemaVersion','productId','algorithm','format','keyId','signedFile') 'customer package signature manifest'
    if (-not (Test-JsonInteger $manifest.schemaVersion 1) -or
        $manifest.productId -isnot [string] -or $manifest.productId -cne $script:ProductId -or
        $manifest.algorithm -isnot [string] -or $manifest.algorithm -cne 'ECDSA-P256-SHA256' -or
        $manifest.format -isnot [string] -or $manifest.format -cne 'rfc3279-der' -or
        $manifest.keyId -isnot [string] -or $manifest.keyId -cnotmatch '^[0-9a-f]{64}$' -or
        $manifest.signedFile -isnot [string] -or $manifest.signedFile -cne 'SHA256SUMS') {
        throw 'Customer package signature metadata is invalid.'
    }

    $trustRingValue = [Environment]::GetEnvironmentVariable('ASPOSE_CLI_RELEASE_TRUSTED_KEYS', 'Process')
    if ([string]::IsNullOrWhiteSpace($trustRingValue)) {
        throw 'Customer release verification requires ASPOSE_CLI_RELEASE_TRUSTED_KEYS. Use scripts/install-local.ps1 only for unsigned local development builds.'
    }
    $trustRingPath = Assert-LocalAbsolutePath ((Resolve-Path -LiteralPath $trustRingValue).Path) 'release trust ring'
    if ((Get-Item -LiteralPath $trustRingPath).Length -gt 64KB) { throw 'The release trust ring exceeds its 64 KiB limit.' }
    $trustRing = Read-StrictJson $trustRingPath 'release trust ring'
    Assert-ExactProperties $trustRing @('keys') 'release trust ring'
    $keys = @($trustRing.keys)
    if ($keys.Count -eq 0 -or $keys.Count -gt 16) { throw 'The release trust ring must contain between 1 and 16 keys.' }
    $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $trustedPem = $null
    foreach ($key in $keys) {
        Assert-ExactProperties $key @('keyId','publicKeyPem') 'release trust-ring key'
        if ($key.keyId -isnot [string] -or $key.keyId -cnotmatch '^[0-9A-Fa-f]{64}$' -or
            $key.publicKeyPem -isnot [string] -or [string]::IsNullOrWhiteSpace($key.publicKeyPem) -or
            $key.publicKeyPem.Length -gt 16KB -or -not $ids.Add([string]$key.keyId)) {
            throw 'The release trust ring contains an invalid or duplicate key.'
        }
        $calculatedId = [AsposeFileInstaller.ReleaseSignature]::GetKeyId([string]$key.publicKeyPem)
        if ($calculatedId -cne ([string]$key.keyId).ToLowerInvariant()) {
            throw 'A release trust-ring key id does not match its public key.'
        }
        if ($calculatedId -ceq [string]$manifest.keyId) { $trustedPem = [string]$key.publicKeyPem }
    }
    if ($null -eq $trustedPem) { throw "No trusted release key is configured for key id '$($manifest.keyId)'." }

    $signatureFile = Get-Item -LiteralPath $signaturePath
    if ($signatureFile.Length -gt 24KB) { throw 'The customer package signature exceeds its encoded size limit.' }
    try { $signature = [Convert]::FromBase64String(([IO.File]::ReadAllText($signaturePath, [Text.Encoding]::ASCII)).Trim()) }
    catch { throw 'The customer package signature is not valid Base64.' }
    if ($signature.Length -eq 0 -or $signature.Length -gt 16KB -or
        -not [AsposeFileInstaller.ReleaseSignature]::Verify(
            $trustedPem,
            [IO.File]::ReadAllBytes($ChecksumPath),
            $signature)) {
        throw 'The customer package detached signature is invalid.'
    }
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
                throw "Directory publication remained blocked after 3 seconds: '$Source' -> '$Destination' ($($_.Exception.Message))"
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



function Get-ManagedInstallState {
    param([Parameter(Mandatory)][string] $Root)
    $inventory = Get-TreeInventory $Root
    $buildMetadata = $null
    $markerPath = Join-Path $Root $script:MarkerName
    $marker = Read-StrictJson $markerPath 'installation marker'
    if ('schemaVersion' -cnotin @($marker.PSObject.Properties.Name)) {
        throw 'Installation marker is missing required property schemaVersion.'
    }
    if (-not (Test-JsonInteger $marker.schemaVersion 2)) {
        throw 'Installation marker schemaVersion must be an integer with a supported value.'
    }
    $schemaVersion = [long]$marker.schemaVersion

        if ('mcpRegistrations' -cnotin @($marker.PSObject.Properties.Name)) { throw 'Installation marker is missing mcpRegistrations.' }
        $markerNames = @('schemaVersion','productId','edition','cliVersion','payloadManifest','payloadManifestSha256','mcpRegistrations')
        Assert-ExactProperties $marker $markerNames 'v2 installation marker'
        if ($marker.productId -isnot [string] -or $marker.productId -cne $script:ProductId -or
            $marker.edition -isnot [string] -or $marker.edition -cnotin $script:AllowedEditions -or
            $marker.cliVersion -isnot [string] -or [string]::IsNullOrWhiteSpace($marker.cliVersion) -or
            $marker.cliVersion -cnotmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$' -or
            $marker.payloadManifest -isnot [string] -or $marker.payloadManifest -cne $script:PayloadManifestName -or
            $marker.payloadManifestSha256 -isnot [string] -or $marker.payloadManifestSha256 -cnotmatch '^[0-9a-f]{64}$' -or
            $marker.mcpRegistrations -isnot [Array]) {
            throw 'The v2 installation marker has invalid ownership fields.'
        }
        $registrations = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($registration in @($marker.mcpRegistrations)) {
            if ($registration -isnot [string] -or $registration -notin @('codex','claude','opencode') -or
                -not $registrations.Add([string]$registration)) {
                throw 'The v2 installation marker contains an invalid or duplicate MCP registration.'
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
        Assert-SetEqual $expected @($inventory.Files.Path) 'v2 managed installation'
        Assert-SetEqual (Get-ExpectedDirectories $expected) @($inventory.Directories) 'v2 managed directories'
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
        Edition = [string]$marker.edition
        CliVersion = [string]$marker.cliVersion
        Snapshot = Get-InventorySnapshot $inventory
        BuildMetadata = $buildMetadata
        McpRegistrations = @(
            if ($schemaVersion -eq 2) {
                @($marker.mcpRegistrations)
            }
        )
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

function Get-UserPath {
    return [Environment]::GetEnvironmentVariable('Path','User')
}

function Set-UserPath {
    param([AllowNull()] $Value)
    [Environment]::SetEnvironmentVariable('Path',$Value,'User')
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
    param($Journal, [string] $TargetKey)
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
    }
    elseif (-not $isOriginal) { throw 'User PATH changed externally; automatic recovery stopped.' }
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

function Get-SkillTransactionKey {
    param([bool] $SkillsSkipped, [string] $CustomRoot)
    if ($SkillsSkipped) { return 'skip' }
    if ([string]::IsNullOrWhiteSpace($CustomRoot)) { return 'detected-hosts' }
    return 'custom-' + (Get-StringSha256 $CustomRoot.ToUpperInvariant())
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
        [Parameter(Mandatory)][string[]] $Arguments
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

function Register-OwnedMcp {
    param(
        [Parameter(Mandatory)][string] $InstallExecutable,
        [Parameter(Mandatory)][string[]] $PreviouslyOwned
    )
    $registered = [Collections.Generic.List[string]]::new()
    $mcpHosts = @(
        [pscustomobject]@{ Name = 'codex'; Executable = 'codex'; Add = @('mcp','add','aspose-cli','--',$InstallExecutable,'mcp','serve') },
        [pscustomobject]@{ Name = 'claude'; Executable = 'claude'; Add = @('mcp','add','aspose-cli','--scope','user','--',$InstallExecutable,'mcp','serve') },
        [pscustomobject]@{ Name = 'opencode'; Executable = 'opencode'; Add = @('mcp','add','aspose-cli','--',$InstallExecutable,'mcp','serve') }
    )
    foreach ($hostSpec in $mcpHosts) {
        $command = Get-Command $hostSpec.Executable -CommandType Application -ErrorAction SilentlyContinue
        if ($null -eq $command) {
            Write-Warning "MCP host '$($hostSpec.Name)' CLI was not found; registration was skipped."
            continue
        }
        try {
            $get = Invoke-OfficialMcp $command.Source @('mcp','get','aspose-cli')
        }
        catch {
            Write-Warning "MCP host '$($hostSpec.Name)' could not be queried safely; registration was skipped."
            continue
        }
        if ($get.ExitCode -eq 0) {
            $reported = @($get.StdOut, $get.StdErr) -join [Environment]::NewLine
            $ownsCurrentExecutable = $reported.IndexOf(
                $InstallExecutable,
                [StringComparison]::OrdinalIgnoreCase) -ge 0
            if ($hostSpec.Name -in $PreviouslyOwned -and $ownsCurrentExecutable) {
                $registered.Add($hostSpec.Name)
            }
            else {
                Write-Warning "MCP host '$($hostSpec.Name)' already has an 'aspose-cli' registration that could not be verified as installer-owned; it was preserved as user-owned."
            }
            continue
        }
        try {
            $add = Invoke-OfficialMcp $command.Source ([string[]]$hostSpec.Add)
        }
        catch {
            Write-Warning "MCP host '$($hostSpec.Name)' registration failed safely and was skipped; the CLI installation remains valid."
            continue
        }
        if ($add.ExitCode -eq 0) {
            $registered.Add($hostSpec.Name)
        }
        else {
            Write-Warning "MCP host '$($hostSpec.Name)' registration failed and was skipped; the CLI installation remains valid."
        }
    }
    return @($registered)
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
        if ($manifest.productId -cne 'aspose-cli-skill' -or $manifest.skill -cne $ExpectedSkill) { throw 'Skill ownership fields do not match the target.' }
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

    return [pscustomobject]@{ Version = $version; Snapshot = Get-InventorySnapshot $inventory }
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

function Invoke-TestFault {
    param([string] $Phase)
    if ($env:ASPOSE_CLI_INSTALL_CRASH -ceq $Phase) { [Environment]::Exit(97) }
    if ($env:ASPOSE_CLI_INSTALL_FAULT -ceq $Phase) { throw "Injected installer failure at '$Phase'." }
}

function Recover-PendingTransaction {
    param(
        [string] $JournalPath,
        [string] $InstallRoot,
        [string] $InstallParent,
        [string] $TargetKey,
        [string] $CustomSkillsRoot,
        [bool] $SkillsSkipped
    )
    if (-not (Test-Path -LiteralPath $JournalPath -PathType Leaf)) { return }
    $journal = Read-StrictJson $JournalPath 'installer transaction journal'
    Assert-ExactProperties $journal @(
        'schemaVersion','productId','targetKey','transactionId','phase','oldSnapshot','newSnapshot',
        'pathState','originalPath','originalPathNull','appliedPathSha256','licenseConfig','skillTargetKey',
        'customSkillsRootExisted','skills','licenses') 'installer transaction journal'
    if (-not (Test-JsonInteger $journal.schemaVersion 3) -or $journal.productId -cne $script:ProductId -or
        $journal.targetKey -cne $TargetKey -or $journal.transactionId -cnotmatch '^[0-9a-f]{32}$' -or
        $journal.phase -cnotin @('prepared','oldMoved','newPublished','committed') -or
        $journal.pathState -cnotin @('none','intent','applied') -or
        $journal.originalPathNull -isnot [bool] -or $journal.customSkillsRootExisted -isnot [bool] -or
        ($journal.pathState -ceq 'none' -and $journal.appliedPathSha256 -cne '') -or
        ($journal.pathState -cne 'none' -and $journal.appliedPathSha256 -cnotmatch '^[0-9a-f]{64}$') -or
        $journal.skillTargetKey -cne (Get-SkillTransactionKey $SkillsSkipped $CustomSkillsRoot)) {
        throw "Installer journal is invalid and was preserved for inspection: $JournalPath"
    }
    $id = [string]$journal.transactionId
    $stage = Join-Path $InstallParent ".aspose-cli-stage-$id"
    $backup = Join-Path $InstallParent ".aspose-cli-backup-$id"
    $skillStageParent = Join-Path $InstallParent ".aspose-cli-skill-stage-$id"
    $licenseConfig = Assert-LocalAbsolutePath (
        Unprotect-PathValue ([string]$journal.licenseConfig) "$TargetKey-license") `
        'journal license configuration directory'
    if ($journal.phase -ceq 'committed') {
        $current = Get-ManagedInstallState $InstallRoot
        if ($current.Snapshot -cne $journal.newSnapshot) { throw "Committed installation changed externally; recovery stopped: $InstallRoot" }
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
        foreach ($licenseRecord in @($journal.licenses)) {
            Assert-ExactProperties $licenseRecord @('order','product','oldSha256','newSha256','status') 'license transaction record'
            if ($licenseRecord.status -cne 'published') { throw 'Committed journal contains an incomplete license record.' }
            $licenseBackup = Join-Path (Join-Path $licenseConfig 'licenses') ".aspose-license-backup-$id-$($licenseRecord.product).lic"
            if (Test-Path -LiteralPath $licenseBackup -PathType Leaf) {
                if (-not $licenseRecord.oldSha256 -or (Get-FileSha256 $licenseBackup) -cne $licenseRecord.oldSha256) { throw "License backup changed externally: $licenseBackup" }
                Remove-Item -LiteralPath $licenseBackup -Force
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
            elseif ($targetState.Snapshot -ceq $skillRecord.newSnapshot) {
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
    foreach ($licenseRecord in @($journal.licenses) | Sort-Object -Property order -Descending) {
        Assert-ExactProperties $licenseRecord @('order','product','oldSha256','newSha256','status') 'license transaction record'
        if ($licenseRecord.product -cnotin $script:AllowedLicenseProducts -or $licenseRecord.status -cnotin @('prepared','published')) { throw 'Installer journal contains an unknown license product.' }
        $target = Join-Path $licenseConfig "licenses\$($licenseRecord.product).lic"
        $licenseBackup = Join-Path (Split-Path -Parent $target) ".aspose-license-backup-$id-$($licenseRecord.product).lic"
        $targetIsOld = $false
        if (Test-Path -LiteralPath $target -PathType Leaf) {
            $targetSha = Get-FileSha256 $target
            if ($targetSha -ceq $licenseRecord.newSha256) { Remove-Item -LiteralPath $target -Force }
            elseif ($licenseRecord.oldSha256 -and $targetSha -ceq $licenseRecord.oldSha256) { $targetIsOld = $true }
            else { throw "Installed license changed externally: $target" }
        }
        if (Test-Path -LiteralPath $licenseBackup -PathType Leaf) {
            if (-not $licenseRecord.oldSha256 -or (Get-FileSha256 $licenseBackup) -cne $licenseRecord.oldSha256) { throw "License backup changed externally: $licenseBackup" }
            if ($targetIsOld) { Remove-Item -LiteralPath $licenseBackup -Force }
            else { [IO.File]::Move($licenseBackup, $target) }
        }
        elseif ($licenseRecord.oldSha256 -and -not $targetIsOld) {
            throw "License backup required for recovery is missing: $licenseBackup"
        }
    }
    Restore-TransactionPath $journal $TargetKey
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
            elseif ($targetState.Snapshot -ceq $newSnapshot) {
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

# Dot-sourcing exposes the ownership primitives to black-box contract tests
# without resolving a package or mutating installation state.
if ($isDotSourced) { return }

if ($SkipSkills -and -not [string]::IsNullOrWhiteSpace($SkillsRoot)) {
    throw '-SkillsRoot conflicts with -SkipSkills.'
}
$cleanupDirectory = ''
if (-not [string]::IsNullOrWhiteSpace($CleanupRoot)) {
    $cleanupDirectory = Assert-LocalAbsolutePath $CleanupRoot 'update staging directory'
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if ((-not $cleanupDirectory.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) -or (-not [IO.Path]::GetFileName($cleanupDirectory).StartsWith('aspose-cli-update-', [StringComparison]::OrdinalIgnoreCase))) {
        throw '-CleanupRoot must be an aspose-cli-update-* directory below the local temporary directory.'
    }
}
$customSkillsRoot = ''
if (-not [string]::IsNullOrWhiteSpace($SkillsRoot)) {
    if ($SkillsRoot -cnotmatch '^[A-Za-z]:[\\/]') {
        throw '-SkillsRoot must be an absolute local fixed-disk directory.'
    }
    $customSkillsRoot = Assert-LocalAbsolutePath $SkillsRoot 'custom Skill root'
    $rootPath = [IO.Path]::GetPathRoot($customSkillsRoot)
    if ($customSkillsRoot.TrimEnd('\').Equals($rootPath.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
        throw '-SkillsRoot may not be a drive root.'
    }
    if ([IO.DriveInfo]::new($rootPath).DriveType -ne [IO.DriveType]::Fixed) {
        throw '-SkillsRoot must be on a fixed local disk.'
    }
    if ((Test-Path -LiteralPath $customSkillsRoot) -and -not (Test-Path -LiteralPath $customSkillsRoot -PathType Container)) {
        throw "Custom Skill root is not a directory: $customSkillsRoot"
    }
}

# Resolve and verify the release package before touching customer state.
$packageDirectory = Assert-LocalAbsolutePath ((Resolve-Path -LiteralPath $PackageRoot).Path) 'package directory'
$sourceExecutable = Join-Path $packageDirectory 'aspose-cli.exe'
$checksumPath = Join-Path $packageDirectory 'SHA256SUMS'
if (-not (Test-Path -LiteralPath $sourceExecutable -PathType Leaf) -or -not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
    throw "Release package must contain aspose-cli.exe and SHA256SUMS: $packageDirectory"
}
$packageInventory = Get-TreeInventory $packageDirectory
$packageTrustFiles = @('SHA256SUMS',$script:PackageSignatureManifestName,$script:PackageSignatureName)
Assert-CustomerPackageTrust $packageDirectory $checksumPath $packageInventory -Development:$DevelopmentPackage
$verifiedFiles = @($packageInventory.Files | Where-Object { $_.Path -cnotin $packageTrustFiles } | Sort-Object Path)
$payloadFiles = @($verifiedFiles | Where-Object { $_.Path -cnotin @('install.cmd','install.ps1') })
$checksums = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($line in Get-Content -LiteralPath $checksumPath) {
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
if ($capabilities.edition -cnotin $script:AllowedEditions) { throw "Packaged executable reports unknown edition '$($capabilities.edition)'." }
$releaseIndicators = @('install.cmd','install.ps1',$script:BuildManifestName) |
    Where-Object { $_ -cin @($packageInventory.Files.Path) }
if (@($releaseIndicators).Count -ne 0) {
    if (-not $DevelopmentPackage -and 'install.cmd' -cin @($packageInventory.Files.Path)) {
        throw "Customer release packages must not contain the unsigned development entry 'install.cmd'."
    }
    $requiredReleaseFiles = @('install.ps1',$script:BuildManifestName)
    if ($DevelopmentPackage) { $requiredReleaseFiles += 'install.cmd' }
    foreach ($required in $requiredReleaseFiles) {
        if ($required -cnotin @($packageInventory.Files.Path)) { throw "Release package is missing '$required'." }
    }
    $packageBuildMetadata = Read-BuildMetadata $packageDirectory
    Assert-CapabilitiesMatchBuildMetadata $capabilities $packageBuildMetadata
}

$localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData, [Environment+SpecialFolderOption]::DoNotVerify)
$installRoot = Assert-LocalAbsolutePath $(if ([string]::IsNullOrWhiteSpace($InstallDirectory)) { Join-Path $localAppData 'Aspose\CLI' } else { $InstallDirectory }) 'install directory'
$installParent = Split-Path -Parent $installRoot
if (Test-IsSameOrChildPath $installRoot $packageDirectory -or Test-IsSameOrChildPath $packageDirectory $installRoot) { throw 'Package and install directories may not overlap.' }
if (-not [string]::IsNullOrWhiteSpace($customSkillsRoot) -and
    ((Test-IsSameOrChildPath $customSkillsRoot $packageDirectory) -or (Test-IsSameOrChildPath $packageDirectory $customSkillsRoot) -or
     (Test-IsSameOrChildPath $customSkillsRoot $installRoot) -or (Test-IsSameOrChildPath $installRoot $customSkillsRoot))) {
    throw 'Custom Skill root may not overlap the package or install directory.'
}
[IO.Directory]::CreateDirectory($installParent) | Out-Null
$targetKey = (Get-StringSha256 $installRoot.ToUpperInvariant()).Substring(0,16)

$userIdentity = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$userHashAlgorithm = [Security.Cryptography.SHA256]::Create()
try { $userLockHash = ([BitConverter]::ToString($userHashAlgorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($userIdentity)))).Replace('-','').ToLowerInvariant() }
finally { $userHashAlgorithm.Dispose() }
$installStateMutex = [Threading.Mutex]::new($false, "Global\AsposeCli.Install.$userLockHash")
$installStateHeld = $false
try { $installStateHeld = $installStateMutex.WaitOne([TimeSpan]::FromMinutes(5)) }
catch [Threading.AbandonedMutexException] { $installStateHeld = $true }
if (-not $installStateHeld) { $installStateMutex.Dispose(); throw 'Another installer is updating the current user state. Retry after it completes.' }

$lockPath = Join-Path $installParent ".aspose-cli-install-$targetKey.lock"
$journalPath = Join-Path $installParent ".aspose-cli-transaction-$targetKey.json"
try {
    $installLock = [IO.FileStream]::new($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
}
catch { $installStateMutex.ReleaseMutex(); $installStateMutex.Dispose(); throw "Another installation is using '$installRoot'. Retry after it finishes." }

$transactionId = [Guid]::NewGuid().ToString('N')
$stage = Join-Path $installParent ".aspose-cli-stage-$transactionId"
$backup = Join-Path $installParent ".aspose-cli-backup-$transactionId"
$licenseStage = Join-Path $installParent ".aspose-cli-license-stage-$transactionId"
$journal = $null
$existingState = $null
$oldUserPath = $null
$rollbackComplete = $false
try {
    Recover-PendingTransaction $journalPath $installRoot $installParent $targetKey $customSkillsRoot ([bool]$SkipSkills)
    if (Test-Path -LiteralPath $installRoot -PathType Container) {
        if (@(Get-ChildItem -LiteralPath $installRoot -Force).Count -ne 0) { $existingState = Get-ManagedInstallState $installRoot }
    }
    elseif (Test-Path -LiteralPath $installRoot) { throw "Install target is not a directory: $installRoot" }

    [IO.Directory]::CreateDirectory($stage) | Out-Null
    foreach ($payload in $payloadFiles) {
        $destination = Join-Path $stage $payload.Path.Replace('/', '\')
        [IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null
        Copy-Item -LiteralPath $payload.FullPath -Destination $destination
    }
    $payloadManifest = [ordered]@{
        schemaVersion = 1
        productId = $script:ProductId
        files = @($payloadFiles | ForEach-Object { [ordered]@{ path = $_.Path; size = $_.Size; sha256 = $_.Sha256 } })
    }
    $payloadManifestPath = Join-Path $stage $script:PayloadManifestName
    Write-JsonAtomic $payloadManifestPath $payloadManifest
    $marker = [ordered]@{
        schemaVersion = 2
        productId = $script:ProductId
        edition = [string]$capabilities.edition
        cliVersion = [string]$capabilities.cliVersion
        payloadManifest = $script:PayloadManifestName
        payloadManifestSha256 = Get-FileSha256 $payloadManifestPath
        mcpRegistrations = @(
            if ($null -ne $existingState) { @($existingState.McpRegistrations) }
        )
    }
    Write-JsonAtomic (Join-Path $stage $script:MarkerName) $marker
    $newState = Get-ManagedInstallState $stage

    if ($LicenseProduct -and [string]::IsNullOrWhiteSpace($LicensePath)) { throw '-LicenseProduct requires -LicensePath.' }
    if ($capabilities.edition -ceq 'free' -and (-not [string]::IsNullOrWhiteSpace($LicensePath) -or $LicenseProduct)) { throw 'Free edition does not accept license installation options.' }
    if ($capabilities.edition -ceq 'commercial' -and [string]::IsNullOrWhiteSpace($LicensePath) -and -not $SkipLicensePrompt) {
        $LicensePath = Read-Host 'Optional Commercial .lic path (press Enter to continue in evaluation mode)'
    }
    $stagedLicenses = @()
    if (-not [string]::IsNullOrWhiteSpace($LicensePath)) {
        $resolvedLicense = Assert-LocalAbsolutePath ((Resolve-Path -LiteralPath $LicensePath).Path) 'license file'
        if (-not (Test-Path -LiteralPath $resolvedLicense -PathType Leaf)) { throw "License file is missing: $resolvedLicense" }
        [IO.Directory]::CreateDirectory($licenseStage) | Out-Null
        $previousConfig = [Environment]::GetEnvironmentVariable('ASPOSE_CLI_CONFIG_DIR','Process')
        [Environment]::SetEnvironmentVariable('ASPOSE_CLI_CONFIG_DIR',$licenseStage,'Process')
        try {
            $arguments = @('license','install',$resolvedLicense)
            if ($LicenseProduct) { $arguments += @('--product',$LicenseProduct) }
            $arguments += @('--output','json')
            $licenseResult = Invoke-CliChildProcess (Join-Path $stage 'aspose-cli.exe') $arguments
            if ($licenseResult.ExitCode -ne 0) { throw "License validation failed with exit code $($licenseResult.ExitCode): $(Get-ChildProcessDiagnostic $licenseResult)" }
        }
        finally { [Environment]::SetEnvironmentVariable('ASPOSE_CLI_CONFIG_DIR',$previousConfig,'Process') }
        foreach ($file in @(Get-ChildItem -LiteralPath (Join-Path $licenseStage 'licenses') -File -ErrorAction SilentlyContinue)) {
            $product = [IO.Path]::GetFileNameWithoutExtension($file.Name).ToLowerInvariant()
            if ($product -cnotin $script:AllowedLicenseProducts) { throw "License validation produced an unknown product '$product'." }
            $stagedLicenses += [pscustomobject]@{ Product = $product; Path = $file.FullName; Sha256 = Get-FileSha256 $file.FullName }
        }
        if ($stagedLicenses.Count -eq 0) { throw 'License validation did not produce a validated product license.' }
    }

    if ($null -ne $existingState) {
        # The package executable is the caller-selected, checksum-verified
        # authority. Never execute the replaceable old installation merely
        # because its marker and manifest are self-consistent.
        $serviceExecutable = Join-Path $stage 'aspose-cli.exe'
        foreach ($arguments in @(@('app','stop','--output','json'), @('preview','stop','--all','--output','json'))) {
            $stopResult = Invoke-CliChildProcess $serviceExecutable $arguments
            if ($stopResult.ExitCode -ne 0) { throw "Existing $($arguments[0]) service could not be stopped safely with exit code $($stopResult.ExitCode): $(Get-ChildProcessDiagnostic $stopResult)" }
        }
        $rechecked = Get-ManagedInstallState $installRoot
        if ($rechecked.Snapshot -cne $existingState.Snapshot) { throw 'Existing installation changed while services were stopping.' }
    }

    $configRootValue = [Environment]::GetEnvironmentVariable('ASPOSE_CLI_CONFIG_DIR')
    if ([string]::IsNullOrWhiteSpace($configRootValue)) {
        $configRootValue = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData, [Environment+SpecialFolderOption]::DoNotVerify)) 'aspose-cli'
    }
    $configRoot = Assert-LocalAbsolutePath $configRootValue 'application configuration directory'

    $configOwnerPath = Join-Path $configRoot '.aspose-cli-config.json'
    if (Test-Path -LiteralPath $configOwnerPath -PathType Leaf) {
        $configOwner = Read-StrictJson $configOwnerPath 'configuration ownership'
        if ($configOwner.productId -cne $script:ProductId) { throw 'Configuration directory belongs to another CLI. Choose a separate configuration directory.' }
    }

    $oldUserPath = Get-UserPath
    $journal = [ordered]@{
        schemaVersion = 3
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
        licenseConfig = Protect-PathValue $configRoot "$targetKey-license"
        skillTargetKey = Get-SkillTransactionKey ([bool]$SkipSkills) $customSkillsRoot
        customSkillsRootExisted = (-not [string]::IsNullOrWhiteSpace($customSkillsRoot) -and (Test-Path -LiteralPath $customSkillsRoot -PathType Container))
        skills = @()
        licenses = @()
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

    if (-not $SkipPath) {
        $entries = @()
        foreach ($entry in @(($oldUserPath -split ';'))) {
            $trimmed = $entry.Trim().Trim('"').TrimEnd('\')
            if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }
            $same = $false
            try { $same = [IO.Path]::GetFullPath($trimmed).TrimEnd('\').Equals($installRoot.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase) } catch { $same = $false }
            if (-not $same) { $entries += $entry.Trim() }
        }
        $updatedPath = (@($entries) + $installRoot) -join ';'
        Set-TransactionalUserPath $journalPath $journal $updatedPath
    }

    foreach ($license in $stagedLicenses) {
        $licenseDirectory = Join-Path $configRoot 'licenses'
        [IO.Directory]::CreateDirectory($licenseDirectory) | Out-Null
        $target = Join-Path $licenseDirectory "$($license.Product).lic"
        $licenseBackup = Join-Path $licenseDirectory ".aspose-license-backup-$transactionId-$($license.Product).lic"
        $oldSha = ''
        if (Test-Path -LiteralPath $target -PathType Leaf) { $oldSha = Get-FileSha256 $target }
        $licenseRecord = [ordered]@{
            order = $journal.licenses.Count
            product = $license.Product
            oldSha256 = $oldSha
            newSha256 = $license.Sha256
            status = 'prepared'
        }
        $journal.licenses += $licenseRecord
        Write-Journal $journalPath $journal
        if ($oldSha) { [IO.File]::Move($target,$licenseBackup) }
        Copy-Item -LiteralPath $license.Path -Destination $target
        if ((Get-FileSha256 $target) -cne $license.Sha256) { throw "Installed license failed verification: $target" }
        $licenseRecord['status'] = 'published'
        Write-Journal $journalPath $journal
        Invoke-TestFault 'licenseUpdated'
    }

    $installedSkills = 0
    if (-not $SkipSkills) {
        $skillListResult = Invoke-CliChildProcess (Join-Path $installRoot 'aspose-cli.exe') @('skill','list','--output','json')
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
        $skillStageParent = Join-Path $installParent ".aspose-cli-skill-stage-$transactionId"
        $expectedSkills = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
        if ($skillTargets.Count -ne 0) {
            foreach ($skill in @($skillList.skills)) {
                if ($skill.name -cnotin $script:AllowedSkills) { throw "Executable reported unknown Skill '$($skill.name)'." }
                $probeResult = Invoke-CliChildProcess (Join-Path $installRoot 'aspose-cli.exe') @('skill','install',[string]$skill.name,'--target',$skillStageParent,'--output','json')
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
                $skillResult = Invoke-CliChildProcess (Join-Path $installRoot 'aspose-cli.exe') $skillArguments
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
    if ($final.Snapshot -cne $newState.Snapshot -or $final.Edition -cne $capabilities.edition) { throw 'Final installed CLI validation failed.' }
    $journal.phase = 'committed'
    Write-Journal $journalPath $journal
    # Once committed, an injected ordinary failure must not report a rollbackable
    # error. A hard-exit hook remains so recovery of committed-but-not-cleaned
    # transactions can be exercised without lying about transaction outcome.
    if ($env:ASPOSE_CLI_INSTALL_CRASH -ceq 'committed') { [Environment]::Exit(97) }

    if (Test-Path -LiteralPath $backup -PathType Container) { Remove-VerifiedInstallDirectory $backup $existingState.Snapshot }
    foreach ($skillRecord in @($journal.skills)) {
        $paths = Get-SkillTransactionPaths ([string]$skillRecord.host) $customSkillsRoot ([string]$skillRecord.skill) $transactionId
        if (Test-Path -LiteralPath $paths.Backup -PathType Container) {
            $paths = Get-SkillTransactionPaths ([string]$skillRecord.host) $customSkillsRoot ([string]$skillRecord.skill) $transactionId
            Remove-VerifiedSkillDirectory $paths.Backup ([string]$skillRecord.skill) ([string]$skillRecord.oldSnapshot)
        }
    }
    foreach ($licenseRecord in @($journal.licenses)) {
        $licenseBackup = Join-Path (Join-Path $configRoot 'licenses') ".aspose-license-backup-$transactionId-$($licenseRecord.product).lic"
        if (Test-Path -LiteralPath $licenseBackup -PathType Leaf) {
            if ($licenseRecord.oldSha256 -and (Get-FileSha256 $licenseBackup) -cne $licenseRecord.oldSha256) { throw "License backup changed before cleanup: $licenseBackup" }
            Remove-Item -LiteralPath $licenseBackup -Force
        }
    }

    if (-not $SkipMcp) {
        $mcpRegistrations = Register-OwnedMcp (Join-Path $installRoot 'aspose-cli.exe') @($newState.McpRegistrations)
        try {
            $installedMarkerPath = Join-Path $installRoot $script:MarkerName
            $installedMarker = Read-StrictJson $installedMarkerPath 'installation marker'
            $installedMarker.mcpRegistrations = @($mcpRegistrations)
            Write-JsonAtomic $installedMarkerPath $installedMarker
        }
        catch {
            Write-Warning "MCP ownership marker could not be updated; the CLI installation remains valid: $($_.Exception.Message)"
        }
    }
    Remove-Item -LiteralPath $journalPath -Force
    $rollbackComplete = $true

    Write-Host "Aspose CLI $($capabilities.cliVersion) ($($capabilities.edition)) installed to $installRoot"
    if (-not $SkipPath) { Write-Host 'The user PATH contains exactly one install-directory entry; restart terminals and AI agents to pick it up.' }
    if ($installedSkills -ne 0) { Write-Host "Installed or updated $installedSkills pristine bundled Agent Skill package(s)." }
    if ($capabilities.edition -ceq 'commercial' -and $stagedLicenses.Count -eq 0) { Write-Host 'Commercial evaluation mode is active. Install a license later with: aspose-cli license install <path-to-license.lic>' }
}
catch {
    $failure = $_
    try {
        if ($null -ne $journal -and (Test-Path -LiteralPath $journalPath -PathType Leaf)) {
            Recover-PendingTransaction $journalPath $installRoot $installParent $targetKey $customSkillsRoot ([bool]$SkipSkills)
        }
        elseif (Test-Path -LiteralPath $stage -PathType Container) {
            $stageState = Get-ManagedInstallState $stage
            Remove-VerifiedInstallDirectory $stage $stageState.Snapshot
        }
        $rollbackComplete = $true
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
    if ($null -ne $installLock) { $installLock.Dispose() }
    if ($installStateHeld) { $installStateMutex.ReleaseMutex(); $installStateMutex.Dispose() }
    try { Remove-Item -LiteralPath $lockPath -Force -ErrorAction SilentlyContinue } catch { }
    if (-not [string]::IsNullOrWhiteSpace($cleanupDirectory)) {
        try { Remove-DirectoryWithRetry $cleanupDirectory $true }
        catch { Write-Warning "Temporary update staging remains at '$cleanupDirectory'." }
    }
}
