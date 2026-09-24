using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

public sealed class CellsCliTests : IDisposable
{
    private const string Secret = "test-secret-must-not-leak";
    private readonly TempWorkspace _workspace = new();
    private readonly List<Process> _previewServices = [];
    private bool _previewRequested;

    [Fact]
    public void CreateEditAndQuery_RoundTripsThroughTheBuiltCli()
    {
        CliResult capabilities = _workspace.Run(
            "capabilities", "cells", "--output", "json");
        Assert.True(capabilities.ExitCode == 0, capabilities.StdErr);
        Assert.Equal(
            ["cells", "cells compare", "cells convert", "cells create", "cells edit", "cells inspect", "cells query", "cells query range", "cells query search", "cells render"],
            JsonNode.Parse(capabilities.StdOut)!["products"]![0]!["commands"]!
                .AsArray()
                .Select(static command => command!["path"]!.GetValue<string>()));

        Assert.Equal(0, _workspace.Run(
            "cells", "create", "book.xlsx", "--sheets", "Data").ExitCode);
        CliResult inspected = _workspace.Run(
            "cells", "inspect", "book.xlsx", "--output", "json");
        Assert.True(inspected.ExitCode == 0, inspected.StdErr);
        string fingerprint = JsonNode.Parse(inspected.StdOut)!["source"]![
            "fingerprint"]!["sha256"]!.GetValue<string>();
        File.WriteAllText(
            _workspace.File("ops.json"),
            """
            {
              "ops": [
                { "op": "set_values", "sheet": "Data", "range": "A1:B2", "values": [["Value","Double"],[21,42]] },
                { "id": "double", "op": "set_formula", "sheet": "Data", "range": "B3", "formula": "=B2*2" }
              ]
            }
            """);

        CliResult edited = _workspace.Run(
            "cells", "edit", "book.xlsx", "--ops", "ops.json",
            "--if-match", fingerprint, "--in-place", "--output", "json");
        CliResult read = _workspace.Run(
            "cells", "query", "range", "book.xlsx", "--sheet", "Data",
            "--range", "B2:B3", "--output", "json");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        Assert.True(read.ExitCode == 0, read.StdErr);
        JsonNode editPayload = JsonNode.Parse(edited.StdOut)!;
        Assert.Equal(fingerprint, editPayload["input"]!["fingerprint"]!["sha256"]!.GetValue<string>());
        Assert.NotEqual(
            fingerprint,
            editPayload["output"]!["fingerprint"]!["sha256"]!.GetValue<string>());
        Assert.Equal("op-0001", editPayload["applied"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("double", editPayload["applied"]![1]!["id"]!.GetValue<string>());
        Assert.Equal(
            ["Data!A1:B2", "Data!B3"],
            editPayload["applied"]!.AsArray()
                .Select(static item => item!["targets"]![0]!.GetValue<string>()));
        JsonNode payload = JsonNode.Parse(read.StdOut)!;
        Assert.Equal(42, payload["sheet"]!["cells"]![0]![0]!["v"]!.GetValue<double>());
        Assert.Equal(84, payload["sheet"]!["cells"]![1]![0]!["v"]!.GetValue<double>());
    }

    [Fact]
    public void QueryRange_ScansAnOverBudgetRegionThroughRunnableNextCommands()
    {
        File.WriteAllLines(
            _workspace.File("grid.csv"),
            Enumerable.Range(1, 10).Select(static row => $"{row},{row * 2},{row * 3}"));

        CliResult summary = _workspace.Run("cells", "query", "range", "grid.csv", "--max-cells", "10", "--output", "json");
        CliResult refused = _workspace.Run(
            "cells", "query", "range", "grid.csv", "--range", "A1:C10", "--max-cells", "10", "--output", "json");

        Assert.True(summary.ExitCode == 0, summary.StdErr);
        string first = JsonNode.Parse(summary.StdOut)!["next"]!.GetValue<string>();
        Assert.EndsWith(" --range A1:C3 --scan-range A1:C10 --scope values --max-cells 10 --output json", first, StringComparison.Ordinal);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("RANGE_TOO_LARGE", error["code"]!.GetValue<string>());
        Assert.Contains(" --range A1:C3 --scan-range A1:C10 ", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);

        CliResult second = _workspace.Run(Tokens(first));
        Assert.True(second.ExitCode == 0, second.StdErr);
        JsonNode page = JsonNode.Parse(second.StdOut)!;
        Assert.Equal("A1:C3", page["sheet"]!["window"]!.GetValue<string>());
        Assert.EndsWith(
            " --range A4:C6 --scan-range A1:C10 --scope values --max-cells 10 --output json",
            page["next"]!.GetValue<string>(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Edit_BestEffortTablePrintsTheFailedOperationErrorAndHint()
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data").ExitCode);

        CliResult edited = _workspace.Run(
            "cells", "edit", "book.xlsx", "--set", "Data!A1=1", "--set", "Missing!A1=2",
            "--best-effort", "--out", "edited.xlsx", "--output", "table");

        Assert.Equal(8, edited.ExitCode);
        Assert.Contains("1 of 2 op(s) applied, 1 failed", edited.StdOut, StringComparison.Ordinal);
        Assert.Contains("[op-0002/1] set_values: failed", edited.StdOut, StringComparison.Ordinal);
        Assert.Contains("      SHEET_NOT_FOUND: ", edited.StdOut, StringComparison.Ordinal);
        Assert.Contains("      hint: ", edited.StdOut, StringComparison.Ordinal);
    }

    /// <summary>Splits a generated command the way a shell would, dropping the executable name.</summary>
    private static string[] Tokens(string command)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool quoted = false;
        bool started = false;
        for (int index = 0; index < command.Length; index++)
        {
            char character = command[index];
            if (quoted && character == '\\' && index + 1 < command.Length && command[index + 1] is '"' or '$' or '`')
            {
                current.Append(command[++index]);
            }
            else if (character == '"')
            {
                quoted = !quoted;
                started = true;
            }
            else if (character == ' ' && !quoted)
            {
                if (started)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    started = false;
                }
            }
            else
            {
                current.Append(character);
                started = true;
            }
        }

        if (started)
        {
            tokens.Add(current.ToString());
        }

        return [.. tokens.Skip(1)];
    }

    /// <summary>
    /// Every CLI child runs in evaluation mode, the only place Cells evaluation is tested:
    /// the in-process engine suite needs a license.
    /// </summary>
    [Fact]
    public void Evaluation_DisclosesTheWatermarkAndRefusesASilentSheetSubstitution()
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--sheets", "Dashboard,Detail").ExitCode);
        Assert.Equal(0, _workspace.Run("cells", "edit", "book.xlsx", "--in-place",
            "--set", "Dashboard!A1=Overview", "--set", "Detail!A1=SO-001").ExitCode);
        File.WriteAllText(_workspace.File("report.csv"), "existing report");

        CliResult refused = _workspace.Run("cells", "convert", "book.xlsx", "--to", "csv",
            "--sheet", "Detail", "--out", "report.csv", "--overwrite", "--output", "json");
        CliResult converted = _workspace.Run("cells", "convert", "book.xlsx", "--to", "csv",
            "--out", "first.csv", "--output", "json");
        CliResult inspected = _workspace.Run("cells", "inspect", "book.xlsx", "--output", "json");

        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("EVALUATION_LIMIT", error["code"]!.GetValue<string>());
        Assert.Equal("Dashboard", error["details"]!["firstSheet"]!.GetValue<string>());
        Assert.Contains("license", error["hint"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(refused.StdOut);
        Assert.Equal("existing report", File.ReadAllText(_workspace.File("report.csv")));
        Assert.True(converted.ExitCode == 0, converted.StdErr);
        JsonNode result = JsonNode.Parse(converted.StdOut)!;
        Assert.Equal("evaluation", result["license"]!["mode"]!.GetValue<string>());
        JsonNode watermark = Assert.Single(result["warnings"]!.AsArray(),
            static warning => warning!["code"]!.GetValue<string>() == "EVAL_MODE")!;
        Assert.False(string.IsNullOrWhiteSpace(watermark["hint"]?.GetValue<string>()));
        Assert.True(inspected.ExitCode == 0, inspected.StdErr);
        Assert.Equal("evaluation", JsonNode.Parse(inspected.StdOut)!["license"]!["mode"]!.GetValue<string>());
    }

    [Fact]
    public void PasswordEnvironmentAndStdin_RoundTripEncryptedOutputWithoutLeaks()
    {
        var variables = new Dictionary<string, string?>
        {
            ["ASPOSE_CLI_TEST_PASSWORD"] = Secret,
        };

        CliResult created = _workspace.RunWithEnv(
            variables,
            "cells", "create", "secret.xlsx", "--sheets", "Data",
            "--encrypt-env", "ASPOSE_CLI_TEST_PASSWORD", "--output", "json");
        CliResult fromEnvironment = _workspace.RunWithEnv(
            variables,
            "cells", "inspect", "secret.xlsx",
            "--password-env", "ASPOSE_CLI_TEST_PASSWORD", "--output", "json");
        CliResult fromStdin = _workspace.RunWithInput(
            Secret + Environment.NewLine,
            "cells", "inspect", "secret.xlsx", "--password-stdin", "--output", "json");
        CliResult withoutPassword = _workspace.Run(
            "cells", "inspect", "secret.xlsx", "--output", "json");

        Assert.Equal(0, created.ExitCode);
        Assert.Equal(0, fromEnvironment.ExitCode);
        Assert.Equal(0, fromStdin.ExitCode);
        Assert.Equal(3, withoutPassword.ExitCode);
        Assert.Contains(
            "PASSWORD_REQUIRED",
            withoutPassword.StdErr,
            StringComparison.Ordinal);
        foreach (string output in new[]
        {
            created.StdOut,
            created.StdErr,
            fromEnvironment.StdOut,
            fromEnvironment.StdErr,
            fromStdin.StdOut,
            fromStdin.StdErr,
            withoutPassword.StdOut,
            withoutPassword.StdErr,
        })
        {
            Assert.DoesNotContain(Secret, output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PreviewStatusAndStop_FormARealCliLifecycle()
    {
        File.WriteAllText(
            _workspace.File("sales.csv"),
            "Region,Revenue\nEast,1200\nWest,900\n");
        _previewRequested = true;
        CliResult started = _workspace.RunWithEnv(
            new Dictionary<string, string?>
            {
                ["ASPOSE_CLI_NO_OPEN"] = "1",
            },
            "preview", "sales.csv", "--output", "json");

        Assert.True(started.ExitCode == 0, started.StdErr);
        JsonNode json = JsonNode.Parse(started.StdOut)!;
        string id = json["id"]!.GetValue<string>();
        RememberService(json);
        Assert.Equal("cells", json["product"]!.GetValue<string>());

        CliResult status = _workspace.Run(
            "preview", "status", id, "--output", "json");
        Assert.True(status.ExitCode == 0, status.StdErr);
        Assert.Contains(id, status.StdOut, StringComparison.Ordinal);

        CliResult stopped = _workspace.Run(
            "preview", "stop", id, "--output", "json");
        Assert.True(stopped.ExitCode == 0, stopped.StdErr);
        Assert.Contains(id, stopped.StdOut, StringComparison.Ordinal);
        Assert.Empty(JsonNode.Parse(stopped.StdOut)!["sessions"]!.AsArray());
    }

    [Fact]
    public void PreviewSheetsView_UsesTheRootLifecycle()
    {
        Assert.Equal(0, _workspace.Run(
            "cells", "create", "preview-sheet.xlsx", "--sheets", "Data").ExitCode);
        CliResult seeded = _workspace.Run(
            "cells", "edit", "preview-sheet.xlsx", "--ops",
            "{\"ops\":[{\"op\":\"set_values\",\"sheet\":\"Data\",\"range\":\"A1\",\"values\":[[\"value\"]]}]}",
            "--in-place", "--output", "json");
        Assert.True(seeded.ExitCode == 0, seeded.StdErr);

        _previewRequested = true;
        CliResult started = _workspace.Run(
            "preview", "preview-sheet.xlsx", "--view", "sheets",
            "--port", "0", "--output", "json");

        Assert.True(started.ExitCode == 0, started.StdErr);
        JsonNode result = JsonNode.Parse(started.StdOut)!;
        string id = result["id"]!.GetValue<string>();
        RememberService(result);
        Assert.Equal("sheets", result["view"]!.GetValue<string>());

        CliResult stopped = _workspace.Run(
            "preview", "stop", id, "--output", "json");
        Assert.True(stopped.ExitCode == 0, stopped.StdErr);
        Assert.Empty(JsonNode.Parse(stopped.StdOut)!["sessions"]!.AsArray());
    }

    private void RememberService(JsonNode started)
    {
        int pid = started["pid"]!.GetValue<int>();
        if (_previewServices.Any(process => process.Id == pid)) { return; }
        Process service = Process.GetProcessById(pid);
        _ = service.Handle;
        _previewServices.Add(service);
    }

    public void Dispose()
    {
        try
        {
            if (_previewRequested)
            {
                try
                {
                    CliResult stopped = _workspace.Run("preview", "stop", "--all", "--output", "json");
                    Assert.True(stopped.ExitCode == 0, stopped.StdErr);
                }
                finally
                {
                    // Closing a document keeps the service and its worker warm.
                    // End this fixture's service before removing its configuration.
                    foreach (Process service in _previewServices)
                    {
                        if (!service.WaitForExit(10_000))
                        {
                            service.Kill(entireProcessTree: true);
                            Assert.True(service.WaitForExit(5_000), "The fixture's viewer service did not exit.");
                        }
                    }
                }
            }
        }
        finally
        {
            foreach (Process service in _previewServices) { service.Dispose(); }
            _workspace.Dispose();
        }
    }
}
