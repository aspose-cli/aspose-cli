using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests;

/// <summary>
/// <see cref="InputOpening"/> is the one way a loader opens an input: it re-verifies the input's
/// admission before the engine runs, tells the write pipeline how the opened document is
/// protected, disposes the document when that fails, and records nothing for a candidate the
/// invocation generated.
/// </summary>
public sealed class InputOpeningTests
{
    /// <summary>
    /// The format declarations of a product that reads .bin files, whose convert also reads .md
    /// files, and that writes .txt files.
    /// </summary>
    private static readonly IReadOnlyList<FormatDescriptor> Reads =
    [
        FormatDescriptor.Declare("bin", FormatUse.Input, ".bin"),
        FormatDescriptor.Declare("md", FormatUse.ConvertInput, ".md"),
        FormatDescriptor.Declare("txt", FormatUse.Convert, ".txt"),
    ];

    /// <summary>The format declarations of another product of the catalog, which reads .doc files.</summary>
    private static readonly IReadOnlyList<FormatDescriptor> Other = [FormatDescriptor.Declare("doc", FormatUse.Input, ".doc")];

    [Theory]
    [InlineData("in.txt", false, ".bin")]
    [InlineData("in.md", false, ".bin")]
    [InlineData("in.doc", false, ".bin")]
    [InlineData("in.doc", true, ".bin,.md")]
    public void Open_AnExtensionThatNamesAFormatTheCommandDoesNotRead_IsRefusedBeforeTheFileIsRead(string name, bool convert, string reads)
    {
        using var temp = new TempDirectory();
        string path = temp.File(name);
        File.WriteAllBytes(path, new byte[4]);
        bool opened = false;
        Input Open()
        {
            opened = true;
            return new Input(new InputProtection(false));
        }

        CliException error = Assert.Throws<CliException>(() => convert
            ? InputOpening.OpenForConvert(Pipeline(Other), TestBudgets.Create(), Reads, path, Open)
            : InputOpening.Open(Pipeline(Other), TestBudgets.Create(), Reads, path, Open));

        Assert.Equal(ErrorCodes.FormatUnsupported, error.Code);
        Assert.Equal(reads.Split(','), error.Details!["available"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.False(opened);
    }

    [Theory]
    [InlineData("in.md", true)]
    [InlineData("in", false)]
    [InlineData("in.unknown", false)]
    public void Open_AnExtensionTheCommandReadsNoFormatDeclaresOrNone_IsLeftToTheEngine(string name, bool convert)
    {
        using var temp = new TempDirectory();
        string path = temp.File(name);
        File.WriteAllBytes(path, new byte[4]);

        using Input input = convert
            ? InputOpening.OpenForConvert(Pipeline(Other), TestBudgets.Create(), Reads, path, static () => new Input(new InputProtection(false)))
            : InputOpening.Open(Pipeline(Other), TestBudgets.Create(), Reads, path, static () => new Input(new InputProtection(false)));

        Assert.False(input.Disposed);
    }

    [Fact]
    public void Open_AboveTheInputByteLimit_IsRefusedBeforeTheEngineRuns()
    {
        using var temp = new TempDirectory();
        string path = temp.File("in.bin");
        File.WriteAllBytes(path, new byte[16]);
        ResourceBudgetLedger budgets = TestBudgets.Create(new Dictionary<string, long>
        {
            [ResourceBudgetKinds.InputBytes] = 8,
        });
        bool opened = false;

        CliException error = Assert.Throws<CliException>(() => InputOpening.Open(Pipeline(), budgets, Reads, path, () =>
        {
            opened = true;
            return new Input(new InputProtection(false));
        }));

        Assert.Equal(ErrorCodes.FileTooLarge, error.Code);
        Assert.False(opened);
    }

    [Fact]
    public void Open_AnInputChangedAfterAdmission_IsRefusedBeforeTheEngineRuns()
    {
        using var temp = new TempDirectory();
        string path = temp.File("in.bin");
        File.WriteAllBytes(path, new byte[4]);
        ResourceBudgetLedger budgets = TestBudgets.Create();
        budgets.AdmitFile(path);
        File.WriteAllBytes(path, new byte[6]);
        bool opened = false;

        CliException error = Assert.Throws<CliException>(() => InputOpening.Open(Pipeline(), budgets, Reads, path, () =>
        {
            opened = true;
            return new Input(new InputProtection(false));
        }));

        Assert.Equal(ErrorCodes.InputChanged, error.Code);
        Assert.False(opened);
    }

    [Theory]
    [InlineData(true, "ENCRYPTION_REMOVED")]
    [InlineData(false, null)]
    public void Open_RecordsTheProtectionOfTheOpenedInput(bool encrypted, string? expected)
    {
        using var temp = new TempDirectory();
        string path = temp.File("in.bin");
        File.WriteAllBytes(path, new byte[4]);
        OutputPipeline<object> pipeline = Pipeline();

        using Input input = InputOpening.Open(pipeline, TestBudgets.Create(), Reads, path, () => new Input(new InputProtection(encrypted)));
        pipeline.Write(temp.File("out.txt"), new FormatDescriptor("txt", FormatUse.Convert, ".txt"), false, null, Save);

        Assert.False(input.Disposed);
        Assert.Equal(expected, pipeline.EncryptionRemoved()?.Code.Name);
    }

    [Fact]
    public void Open_WhenRecordingFails_DisposesTheOpenedInput()
    {
        using var temp = new TempDirectory();
        string path = temp.File("in.bin");
        File.WriteAllBytes(path, new byte[4]);
        var input = new Input(protection: null);

        Assert.Throws<InvalidOperationException>(() => InputOpening.Open(Pipeline(), TestBudgets.Create(), Reads, path, () => input));

        Assert.True(input.Disposed);
    }

    [Fact]
    public void OpenPublished_NeitherAdmitsNorRecords()
    {
        using var temp = new TempDirectory();
        OutputPipeline<object> pipeline = Pipeline();

        // The candidate does not exist on disk: admission would refuse it.
        using Input input = InputOpening.OpenPublished(() => new Input(new InputProtection(true)));
        pipeline.Write(temp.File("out.txt"), new FormatDescriptor("txt", FormatUse.Convert, ".txt"), false, null, Save);

        Assert.Null(pipeline.EncryptionRemoved());
    }

    private static void Save(string path) => File.WriteAllText(path, "saved");

    private static OutputPipeline<object> Pipeline(IReadOnlyList<FormatDescriptor>? declared = null) =>
        new(new LicensedGate(), new NoMarks(), TestBudgets.Writer()) { DeclaredFormats = declared ?? [] };

    /// <summary>An opened input whose protection getter fails when it has none.</summary>
    private sealed class Input(InputProtection? protection) : IOpenedInput, IDisposable
    {
        public bool Disposed { get; private set; }

        public InputProtection Protection => protection ?? throw new InvalidOperationException("No protection.");

        public void Dispose() => Disposed = true;
    }

    private sealed class NoMarks : IEvaluationProfile<object>
    {
        public string Engine => "Aspose.Test";

        public EvaluationMarks Inspect(object document) => EvaluationMarks.None;
    }

    private sealed class LicensedGate : ILicenseGate
    {
        public LicenseResolution Resolution => LicenseResolution.None;

        public string Identity => "test";

        public LicenseState EnsureApplied() => LicenseState.Licensed;
    }
}
