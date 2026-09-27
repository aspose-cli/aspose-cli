[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $SdkAssembly,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [string] $LicensePath = $(if ($env:ASPOSE_SLIDES_LICENSE_PATH) { $env:ASPOSE_SLIDES_LICENSE_PATH } else { $env:ASPOSE_LICENSE_PATH })
)

# Exit 0: Slides saved a presentation. Exit 1: the non-string font registry value broke it. Exit 2: the check could not run.
$ErrorActionPreference = 'Stop'
try {
    if (-not $IsWindows) { throw 'This gate reproduces a Windows registry defect; run it on Windows.' }
    $assembly = (Resolve-Path -LiteralPath $SdkAssembly).Path
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    if (Test-Path -LiteralPath $output) { throw 'Choose a fresh output directory; this reproduction does not overwrite files.' }
    [void][IO.Directory]::CreateDirectory($output)

    # HKCU is redirected, for this process only, to an application hive file in the output
    # directory. The user's registry is never written.
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

public static class SlidesFontRegistryGate
{
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegLoadAppKey(string file, out IntPtr key, int access, int options, int reserved);

    [DllImport("advapi32.dll")]
    private static extern int RegOverridePredefKey(IntPtr predefined, IntPtr replacement);

    private const string FontsKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";

    /// <summary>Redirects HKCU to a new hive whose Fonts key holds one string and one DWORD value.</summary>
    public static string[] RedirectCurrentUser(string hiveFile)
    {
        IntPtr hive;
        int status = RegLoadAppKey(hiveFile, out hive, 0xF003F, 0, 0);
        if (status != 0) { throw new InvalidOperationException("RegLoadAppKey failed with " + status); }
        using (var root = RegistryKey.FromHandle(new SafeRegistryHandle(hive, false)))
        using (RegistryKey fonts = root.CreateSubKey(FontsKey))
        {
            fonts.SetValue("Gate Font (TrueType)", @"C:\Windows\Fonts\arial.ttf", RegistryValueKind.String);
            fonts.SetValue("Gate DWORD", 1, RegistryValueKind.DWord);
        }

        status = RegOverridePredefKey(new IntPtr(unchecked((int)0x80000001)), hive);
        if (status != 0) { throw new InvalidOperationException("RegOverridePredefKey failed with " + status); }
        using (RegistryKey seen = Registry.CurrentUser.OpenSubKey(FontsKey))
        {
            if (seen == null) { throw new InvalidOperationException("The redirected Fonts key is not visible."); }
            string[] names = seen.GetValueNames();
            string[] described = new string[names.Length];
            for (int i = 0; i < names.Length; i++) { described[i] = names[i] + ": " + seen.GetValueKind(names[i]); }
            return described;
        }
    }
}
'@
    $values = [SlidesFontRegistryGate]::RedirectCurrentUser((Join-Path $output 'current-user.hive'))
    if ($values -notcontains 'Gate DWORD: DWord') { throw 'The redirected HKCU does not show the DWORD value; the reproduction did not take effect.' }

    # The native drawing library shipped with the SDK must be discoverable.
    $env:PATH = [IO.Path]::GetDirectoryName($assembly) + [IO.Path]::PathSeparator + $env:PATH
    Add-Type -Path $assembly
    if (-not [string]::IsNullOrWhiteSpace($LicensePath)) {
        $license = [Aspose.Slides.License]::new()
        $license.SetLicense((Resolve-Path -LiteralPath $LicensePath).Path)
    }

    # Document-operation core: a text shape makes the save initialize fonts.
    $failure = $null
    $presentation = [Aspose.Slides.Presentation]::new()
    try {
        $shape = $presentation.Slides[0].Shapes.AddAutoShape([Aspose.Slides.ShapeType]::Rectangle, 10, 10, 300, 50)
        $shape.TextFrame.Text = 'Font registry gate'
        $presentation.Save((Join-Path $output 'output.pptx'), [Aspose.Slides.Export.SaveFormat]::Pptx)
    } catch {
        $failure = $_.Exception
        while ($null -ne $failure.InnerException) { $failure = $failure.InnerException }
    } finally { $presentation.Dispose() }

    $result = [ordered]@{
        SdkVersion = [Aspose.Slides.Presentation].Assembly.GetName().Version.ToString()
        SdkSha256 = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
        Licensed = -not [string]::IsNullOrWhiteSpace($LicensePath)
        RedirectedFontValues = $values
        Failure = $(if ($null -ne $failure) { "$($failure.GetType().FullName): $($failure.Message)" } else { $null })
    }
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 2
}

if ($null -eq $failure) { exit 0 }
if ($failure -is [InvalidCastException]) {
    [Console]::Error.WriteLine("A REG_DWORD value in the per-user Fonts key broke the save: $($result.Failure)")
    exit 1
}
[Console]::Error.WriteLine("The save failed for another reason: $($result.Failure)")
exit 2
