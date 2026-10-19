using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO;
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

        CliException error = Assert.Throws<CliException>(() => InputOpening.Open(Pipeline(), budgets, path, () =>
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

        CliException error = Assert.Throws<CliException>(() => InputOpening.Open(Pipeline(), budgets, path, () =>
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

        using Input input = InputOpening.Open(pipeline, TestBudgets.Create(), path, () => new Input(new InputProtection(encrypted)));
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

        Assert.Throws<InvalidOperationException>(() => InputOpening.Open(Pipeline(), TestBudgets.Create(), path, () => input));

        Assert.True(input.Disposed);
    }

    [Fact]
    public void Open_WithoutAPipeline_OpensTheInputAsIs()
    {
        using var temp = new TempDirectory();
        string path = temp.File("in.bin");
        File.WriteAllBytes(path, new byte[4]);
        var input = new Input(protection: null);

        Assert.Same(input, InputOpening.Open(outputs: null, TestBudgets.Create(), path, () => input));
        Assert.False(input.Disposed);
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

    private static OutputPipeline<object> Pipeline() =>
        new(new LicensedGate(), new NoMarks(), TestBudgets.Writer());

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
