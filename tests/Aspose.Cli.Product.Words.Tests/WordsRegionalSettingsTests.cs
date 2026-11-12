using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// What a command computes does not change with the machine's regional settings: under German
/// ones, which write a decimal comma, an edit that inserts formula fields saves their results as
/// under en-US. Each edit is a CLI process started under the culture it names
/// (<see cref="TempWorkspace.RunUnderCulture"/>); the reads of what it wrote are not.
/// </summary>
public sealed class WordsRegionalSettingsTests : IDisposable
{
    private const string Fields =
        """
        {"ops":[
          {"op":"insert_field","at":{"block":1},"position":"after","code":"= 1.5 + 2.25"},
          {"op":"insert_field","at":{"block":1},"position":"after","code":"= 1234.5 \\# \"#,##0.00\""}
        ]}
        """;

    private readonly TempWorkspace _workspace = new();

    public WordsRegionalSettingsTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void FormulaFieldsInsertedUnderGermanRegionalSettings_HoldTheResultsOfEnglish()
    {
        Assert.Equal(0, _workspace.Run("words", "create", "letter.docx").ExitCode);

        string[] english = Inserted("en-US", "english.docx");

        Assert.Equal(["= 1234.5 \\# \"#,##0.00\" -> 1,234.50", "= 1.5 + 2.25 -> 3.75"], english);
        Assert.Equal(english, Inserted("de-DE", "german.docx"));
    }

    /// <summary>
    /// Inserts the formula fields with <c>--verify</c> in a process under <paramref name="culture"/>
    /// and returns each formula field's code and saved result, as <c>words inspect</c> reports them.
    /// </summary>
    private string[] Inserted(string culture, string output)
    {
        CliResult edited = _workspace.RunUnderCulture(culture,
            "words", "edit", "letter.docx", "--ops", Fields, "--verify", "--out", output, "--output-format", "json");
        Assert.True(edited.ExitCode == 0, $"{culture}: {edited.StdErr}{edited.StdOut}");
        Assert.True(edited.Json()["verification"]!["ok"]!.GetValue<bool>(), edited.StdOut);

        CliResult inspected = _workspace.Run("words", "inspect", output, "--detail", "fields", "--output-format", "json");
        Assert.True(inspected.ExitCode == 0, inspected.StdErr);
        return [.. inspected.Json()["fields"]!.AsArray()
            .Where(static field => field!["kind"]!.GetValue<string>() == "FieldFormula")
            .Select(static field => $"{field!["code"]} -> {field["result"]}")];
    }
}
