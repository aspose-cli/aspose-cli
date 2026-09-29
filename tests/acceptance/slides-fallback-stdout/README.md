# Slides fallback standard output acceptance: SLIDES-FALLBACK-STDOUT

This explicitly invoked gate asserts that Aspose.Slides writes nothing to standard output when it
renders with a font fallback rule. It **fails on Aspose.Slides.NET6.CrossPlatform 26.9.0** in both
licensed and evaluation modes. It is outside normal regression discovery under
`tests/acceptance`, adds no project or dependency, and neither skips failures nor accepts the
broken result as its baseline.

## Run

Use PowerShell 7 after a normal repository build. A license is optional. Run from the repository
root:

```powershell
$env:ASPOSE_SLIDES_LICENSE_PATH = 'C:/private/Aspose.Slides.lic'
./tests/acceptance/slides-fallback-stdout/reproduce.ps1 `
  -SdkAssembly ./src/Aspose.Cli/bin/Release/net10.0/Aspose.Slides.dll `
  -OutputDirectory ./artifacts/slides-fallback-stdout/run-01
```

Choose a fresh output directory on every run. With `Console.Out` captured, the script adds one
fallback rule and exports a slide with Chinese text through the public SDK only:

```csharp
presentation.FontsManager.FontFallBackRulesCollection.Add(new FontFallBackRule(0x4E00, 0x9FFF, "SimSun"));
presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 600, 80).TextFrame.Text = "回退规则门禁";
presentation.Save(outputPath, SaveFormat.Pdf);
```

No CLI assembly or product engine is loaded. `result.json` records the SDK version and hash and
what was written. The script returns **0 when nothing was written to standard output**, **1 when
something was** and **2 when the check could not run**.

## Observed behavior

With SDK 26.9 every export or rendering that uses a fallback rule writes `Updating of Inner rules:`
and one `... Rule: 0x4E00-9FFF` line per rule to `Console.Out`, on every presentation and every
export. Adding the rule and `FontsManager.GetFonts` write nothing.

## Handling

KNOWN-ISSUES.md describes the CLI's handling of this defect under the gate's id, and the code
that handles it names the id. While this gate exits 1 the release proceeds; when it exits 0,
delete the issue, its handling and this gate. Do not make it pass by rewriting input or output,
or by weakening its assertion.
