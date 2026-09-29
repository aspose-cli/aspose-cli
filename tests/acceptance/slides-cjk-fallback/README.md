# Slides CJK fallback acceptance: SLIDES-CJK-FALLBACK

This explicitly invoked gate asserts that Aspose.Slides draws one Chinese run in one font when
the run's own font has no Chinese glyphs. It **fails on Aspose.Slides.NET6.CrossPlatform 26.9.0**.
It is outside normal regression discovery under `tests/acceptance`, adds no project or
dependency, and neither skips failures nor accepts the broken result as its baseline.

## Run

Use PowerShell 7 on Windows with the SimSun and MS Gothic fonts installed (the defect needs both;
without them the script exits 2) after a normal repository build. A license is optional. Run from
the repository root:

```powershell
$env:ASPOSE_SLIDES_LICENSE_PATH = 'C:/private/Aspose.Slides.lic'
./tests/acceptance/slides-cjk-fallback/reproduce.ps1 `
  -SdkAssembly ./src/Aspose.Cli/bin/Release/net10.0/Aspose.Slides.dll `
  -OutputDirectory ./artifacts/slides-cjk-fallback/run-01
```

Choose a fresh output directory on every run. The script draws one Chinese run in a new
presentation, whose theme fonts have no Chinese glyphs, through the public SDK only:

```csharp
using var presentation = new Presentation();
presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 600, 80).TextFrame.Text = "问题与对策应收账款余额";
presentation.Slides[0].WriteAsSvg(stream);
```

It then collects the `font-family` of every SVG text element that holds Chinese characters. No CLI
assembly or product engine is loaded. `result.json` records the SDK version and hash and the font
families. The script returns **0 when the run uses one font**, **1 when it uses several** and **2
when the check could not run**.

## Observed behavior

With SDK 26.9 the run alternates glyph by glyph between MS Gothic and SimSun: characters MS Gothic
has are drawn in it, simplified characters it lacks (对, 账, 疗) in SimSun, so one word mixes stroke
weights. PDF export and raster rendering do the same. A Chinese language tag on the run replaces
SimSun with Microsoft JhengHei, a Traditional Chinese font, and still alternates. PowerPoint draws
the run in one East Asian font. `LoadOptions.DefaultAsianFont` does not change the result, and the
default `FontsManager.FontFallBackRulesCollection` is empty.

## Handling

KNOWN-ISSUES.md describes the CLI's handling of this defect under the gate's id, and the code
that handles it names the id. While this gate exits 1 the release proceeds; when it exits 0,
delete the issue, its handling and this gate. Do not make it pass by rewriting input or output,
or by weakening its assertion.

Official references:

- [Font fallback](https://docs.aspose.com/slides/net/fallback-font/)
- [FontFallBackRule](https://reference.aspose.com/slides/net/aspose.slides/fontfallbackrule/)
