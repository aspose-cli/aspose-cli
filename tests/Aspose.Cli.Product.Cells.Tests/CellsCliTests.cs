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
