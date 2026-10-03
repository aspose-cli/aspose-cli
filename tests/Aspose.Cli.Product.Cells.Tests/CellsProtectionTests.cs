using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// Sheet and structure protection through the built CLI: inspect reports it, and an edit that
/// goes through it warns PROTECTION_NOT_ENFORCED, because the engine does not enforce it. The
/// tests share one protected book and write every edit to its own output.
/// </summary>
public sealed class CellsProtectionTests(CellsProtectionTests.ProtectedBook book) : IClassFixture<CellsProtectionTests.ProtectedBook>
{
    private const string PasswordVariable = "ASPOSE_CLI_TEST_SHEET_PASSWORD";
    private const string OtherPasswordVariable = "ASPOSE_CLI_TEST_OTHER_PASSWORD";

    [Fact]
    public void Inspect_ReportsSheetAndStructureProtection()
    {
        Assert.DoesNotContain(Warnings(book.Created), static warning => Code(warning) == "PROTECTION_NOT_ENFORCED");

        JsonNode workbook = book.Workspace.Run("cells", "inspect", "book.xlsx", "--output", "json").Json()["workbook"]!;

        Assert.True(workbook["structureProtected"]!.GetValue<bool>());
        Assert.False(workbook["structurePasswordProtected"]!.GetValue<bool>());
        JsonNode data = Sheet(workbook, "Data");
        Assert.True(data["protected"]!.GetValue<bool>());
        Assert.True(data["passwordProtected"]!.GetValue<bool>());
        JsonNode open = Sheet(workbook, "Open");
        Assert.False(open["protected"]!.GetValue<bool>());
        Assert.False(open["passwordProtected"]!.GetValue<bool>());
    }

    [Fact]
    public void Edit_WarnsOnlyWhenItGoesThroughProtection()
    {
        Warning sheet = Unenforced(book.Edit("protected-sheet.xlsx", """{"op":"set_values","sheet":"Data","range":"A1","values":[[1]]}"""));
        Assert.Equal("'Data'", sheet.Location);
        Assert.Contains("protected sheet 'Data'", sheet.Message, StringComparison.Ordinal);
        Assert.Contains("keeps the protection", sheet.Hint, StringComparison.Ordinal);

        Warning structure = Unenforced(book.Edit("protected-structure.csv", """{"op":"add_sheet","name":"Extra"}"""));
        Assert.Null(structure.Location);
        Assert.Contains("the protected workbook structure", structure.Message, StringComparison.Ordinal);
        Assert.Contains("A csv output does not keep the protection", structure.Hint, StringComparison.Ordinal);

        // Neither an unprotected sheet nor one the batch unprotected first goes through protection.
        Assert.DoesNotContain(
            Warnings(book.Edit("unprotected.xlsx",
                $$"""{"op":"set_values","sheet":"Open","range":"A1","values":[[1]]},{"op":"unprotect_sheet","sheet":"Data","passwordEnv":"{{PasswordVariable}}"},{"op":"set_values","sheet":"Data","range":"A1","values":[[1]]}""")),
            static warning => Code(warning) == "PROTECTION_NOT_ENFORCED");
    }

    [Fact]
    public void Protect_WarnsOnlyWhenItReprotectsWhatIsProtected()
    {
        Warning sheet = Unenforced(book.Edit("reprotected-sheet.xlsx",
            $$"""{"op":"protect_sheet","sheet":"Data","passwordEnv":"{{OtherPasswordVariable}}","allow":["sort"]}"""));
        Assert.Equal("'Data'", sheet.Location);
        Assert.Contains("protected sheet 'Data'", sheet.Message, StringComparison.Ordinal);
        Assert.Contains("protect_sheet kept the existing password of sheet 'Data'", sheet.Message, StringComparison.Ordinal);

        // Protecting the unprotected sheet 'Open' goes through nothing; protecting the structure again does.
        Warning structure = Unenforced(book.Edit("reprotected-structure.xlsx",
            $$"""{"op":"protect_sheet","sheet":"Open"},{"op":"protect_workbook","passwordEnv":"{{OtherPasswordVariable}}"}"""));
        Assert.Null(structure.Location);
        Assert.Contains("The edit changed the protected workbook structure;", structure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("'Open'", structure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("kept the existing password", structure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unprotect_RefusesAWrongOrMissingPasswordAsAPasswordError()
    {
        JsonNode wrong = Refused(book.Run("unprotect-wrong.xlsx",
            $$"""{"op":"unprotect_sheet","sheet":"Data","passwordEnv":"{{OtherPasswordVariable}}"}"""));
        Assert.Equal("PASSWORD_INVALID", wrong["code"]!.GetValue<string>());
        Assert.Contains("sheet 'Data'", wrong["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("\"passwordEnv\"", wrong["hint"]!.GetValue<string>(), StringComparison.Ordinal);

        JsonNode missing = Refused(book.Run("unprotect-missing.xlsx", """{"op":"unprotect_sheet","sheet":"Data"}"""));
        Assert.Equal("PASSWORD_REQUIRED", missing["code"]!.GetValue<string>());

        // The structure has no password, so unprotecting it needs none; a wrong one is refused alike.
        JsonNode structure = Refused(book.Run("unprotect-structure.xlsx",
            $$"""{"op":"protect_workbook","passwordEnv":"{{PasswordVariable}}"},{"op":"unprotect_workbook","passwordEnv":"{{OtherPasswordVariable}}"}"""));
        Assert.Equal("PASSWORD_INVALID", structure["code"]!.GetValue<string>());
        Assert.Contains("workbook structure", structure["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    private static JsonNode Refused(CliResult result)
    {
        Assert.Equal(3, result.ExitCode);
        return JsonNode.Parse(result.StdErr)!["error"]!;
    }

    private static Warning Unenforced(JsonNode result)
    {
        JsonNode warning = Assert.Single(Warnings(result), static warning => Code(warning) == "PROTECTION_NOT_ENFORCED");
        return new Warning(
            warning["location"]?.GetValue<string>(),
            warning["message"]!.GetValue<string>(),
            warning["hint"]!.GetValue<string>());
    }

    private static JsonNode[] Warnings(JsonNode result) =>
        [.. (result["warnings"]?.AsArray() ?? []).Select(static warning => warning!)];

    private static string Code(JsonNode warning) => warning["code"]!.GetValue<string>();

    private static JsonNode Sheet(JsonNode workbook, string name) =>
        Assert.Single(workbook["sheets"]!.AsArray(), sheet => sheet!["name"]!.GetValue<string>() == name)!;

    private sealed record Warning(string? Location, string Message, string Hint);

    /// <summary>
    /// book.xlsx with sheet 'Data' protected by a password, sheet 'Open' unprotected and the
    /// structure protected without one; <see cref="Created"/> is the result of protecting it.
    /// </summary>
    public sealed class ProtectedBook : IDisposable
    {
        public ProtectedBook()
        {
            Assert.Equal(0, Workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data,Open").ExitCode);
            Created = Edit(null,
                $$"""{"op":"protect_sheet","sheet":"Data","passwordEnv":"{{PasswordVariable}}"},{"op":"protect_workbook"}""");
        }

        public TempWorkspace Workspace { get; } = new();

        public JsonNode Created { get; }

        /// <summary>Edits book.xlsx into <paramref name="output"/>, or in place when it is null.</summary>
        public JsonNode Edit(string? output, string ops) => Run(output, ops).Json();

        /// <summary>Runs the edit of <see cref="Edit"/> without requiring it to succeed.</summary>
        public CliResult Run(string? output, string ops)
        {
            string[] target = output is null ? ["--in-place"] : ["--out", output];
            return Workspace.RunWithEnv(
                new Dictionary<string, string?> { [PasswordVariable] = "sheet-secret", [OtherPasswordVariable] = "other-secret" },
                ["cells", "edit", "book.xlsx", .. target, "--output", "json", "--ops", $$"""{"ops":[{{ops}}]}"""]);
        }

        public void Dispose() => Workspace.Dispose();
    }
}
