# Slides chart title acceptance: SLIDES-CHART-TITLE

This is an explicitly invoked upstream SDK acceptance check. It is outside the normal
regression discovery under `tests/acceptance`; it adds no project or dependency.
It asserts the desired fidelity and **fails on Aspose.Slides.NET6.CrossPlatform 26.9.0**.
There is no skip, accepted broken baseline, or production compensation.

## Fixture and expectation

`input.pptx` is a fully fictional two-slide presentation. Slide 1 says “Edit this title
only”. Slide 2 has one editable clustered-column chart, categories A/B/C, series CNY,
values 12500/18750/0, and an explicit zero minimum. Its automatic title is displayed
above the plot in PowerPoint 16.0. Category colors and the A/B/C legend are present.
The input was saved by PowerPoint `SaveCopyAs` after independent synthetic authoring;
that save preserved the chart's effective appearance. `input-chart.xml` retains the
source chart part and `expected.json` identifies the fixture and expected semantics.
The Office-produced package retains negative axis IDs; this is a compatibility
reproduction, not an assertion of complete ISO schema validity.

The original setup and independent observations are reproducible from the retained
source: open `input.pptx` in PowerPoint read-only, inspect slide 2, and compare the
SDK output in that same viewer. The title occupies space above the plot; no chart
setting is to be changed before saving. Expected source plot inside top/height were
47.352519685 / 228.745826772 points, with automatic major unit 5000. SDK 26.9 output
instead gave 11.987480315 / 264.110866142 and 2000 in that same PowerPoint environment.
Those geometry values are evidence, not constants pinned by the verifier.

## Run

After a normal repository build, use its commercial SDK assembly and a licensed,
working SDK font environment. Run from the repository root with PowerShell 7:

```powershell
$env:ASPOSE_SLIDES_LICENSE_PATH = 'C:/private/Aspose.Slides.lic'
./tests/acceptance/slides-sdk-fidelity/reproduce.ps1 `
  -SdkAssembly ./src/Aspose.Cli.Product.Slides/bin/Release/net10.0/Aspose.Slides.dll `
  -Output ./artifacts/slides-sdk-fidelity/output.pptx
```

The native drawing library shipped with that SDK must be discoverable. Choose a new
output path for each run. `-KeepThumbnail` tests the documented `RefreshThumbnail=false`
option; it did not mitigate this defect. No CLI assembly or product engine is loaded.
The document-operation core is the documented API usage:

```csharp
using var presentation = new Presentation(input);
presentation.Save(output, SaveFormat.Pptx);
```

The verifier returns exit 1 when fidelity fails, exit 0 only when every desired
assertion passes. Reproduction setup/SDK exceptions return exit 2 and are not a
fidelity pass. You can verify a retained native SDK output independently:

```powershell
./tests/acceptance/slides-sdk-fidelity/verify.ps1 -Presentation ./artifacts/slides-sdk-fidelity/output.pptx
```

On the acceptance workstation, bare SDK saving encountered malformed per-user font
registry metadata before document publication. That environment failure was diagnosed
separately; this shipped repro does not change or override the registry. After font
initialization was isolated in a diagnostic process, the native SDK and production
font-initialization control produced identical changed chart XML. The CLI's font
initialization, chart getters, template application and mutation code were excluded
as causes. Bulky diagnostic outputs remain under `artifacts`, not in this fixture.

## Handling

KNOWN-ISSUES.md describes the CLI's handling of this defect under the gate's id, and the code
that handles it names the id. While this gate exits 1 the release proceeds; when it exits 0,
delete the issue, its handling and this gate. Do not make it pass by rewriting input or output,
or by weakening its assertion.

Official API references:

- [Saving presentations](https://docs.aspose.com/slides/net/save-presentation/)
- [PPTX save options](https://reference.aspose.com/slides/net/aspose.slides.export/ipptxoptions/)
- [Load options](https://reference.aspose.com/slides/net/aspose.slides/loadoptions/)
- [Custom font initialization](https://docs.aspose.com/slides/net/custom-font/)
