# Slides font registry acceptance: SLIDES-FONT-REGISTRY

This explicitly invoked gate asserts that Aspose.Slides saves a presentation when the per-user
Fonts registry key holds a value that is not a string. It **fails on
Aspose.Slides.NET6.CrossPlatform 26.9.0** in both licensed and evaluation modes. It is outside
normal regression discovery under `tests/acceptance`, adds no project or dependency, and neither
skips failures nor accepts the broken result as its baseline.

## Run

Use PowerShell 7 on Windows after a normal repository build. A license is optional; the defect
does not depend on it. Run from the repository root:

```powershell
$env:ASPOSE_SLIDES_LICENSE_PATH = 'C:/private/Aspose.Slides.lic'
./tests/acceptance/slides-font-registry/reproduce.ps1 `
  -SdkAssembly ./src/Aspose.Cli/bin/Release/net10.0/Aspose.Slides.dll `
  -OutputDirectory ./artifacts/slides-font-registry/run-01
```

Choose a fresh output directory on every run. The script never writes the user's registry: it
loads an application hive file in the output directory with `RegLoadAppKey` and redirects
`HKEY_CURRENT_USER` to it for its own process with `RegOverridePredefKey`. The hive's
`SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts` key holds one `REG_SZ` font entry and one
`REG_DWORD` value, and the script confirms the redirection is visible before it loads the SDK.
It then saves a new presentation with one text shape through the public SDK only:

```csharp
using var presentation = new Presentation();
presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 300, 50).TextFrame.Text = "Font registry gate";
presentation.Save(outputPath, SaveFormat.Pptx);
```

No CLI assembly or product engine is loaded. `result.json` records the SDK version and hash, the
redirected Fonts values and the failure. The script returns **0 when the save succeeds**, **1
when it fails with `InvalidCastException`** and **2 when the check could not run**, including
when the redirection does not take effect or the save fails for another reason.

## Observed behavior

With SDK 26.9 font initialization casts every value of the per-user Fonts key to `string`: a
`REG_DWORD` value throws `InvalidCastException` (`Int32` to `String`) and a `REG_BINARY` value
throws it for `Byte[]`, on load, save and rendering alike. `REG_SZ` and `REG_EXPAND_SZ` values, an
empty key and a missing key work. `FontsLoader.GetFontFolders` is not affected.

## Handling

KNOWN-ISSUES.md describes the CLI's handling of this defect under the gate's id, and the code
that handles it names the id. While this gate exits 1 the release proceeds; when it exits 0,
delete the issue, its handling and this gate. Do not make it pass by rewriting input or output,
or by weakening its assertion.

Official references:

- [Custom font initialization](https://docs.aspose.com/slides/net/custom-font/)
- [FontsLoader](https://reference.aspose.com/slides/net/aspose.slides/fontsloader/)
