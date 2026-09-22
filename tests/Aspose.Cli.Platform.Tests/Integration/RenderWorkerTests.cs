using System.Diagnostics;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// The viewer service renders through a warm child process: every product is
/// served by the same worker, a worker that cannot answer is replaced, and a
/// license change recycles it because an engine cannot swap a license.
/// </summary>
public sealed class RenderWorkerTests : IDisposable
{
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromMinutes(2);
    private readonly TempWorkspace _workspace = new();
    private readonly string _storage = PrivateUserStorage.CreateTemporaryDirectory("viewer-test");

    public void Dispose()
    {
        _workspace.Dispose();
        try { Directory.Delete(_storage, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Render_ServesEveryProductFromOneWarmWorker()
    {
        CreateDocument("report.docx");
        CreateWorkbook("book.xlsx");
        using var supervisor = new RenderWorkerSupervisor(StartInfo, RenderTimeout);

        (RenderWorkerResponse document, string documentOutput) = Render(supervisor, "report.docx", presentation: true);
        int worker = supervisor.ProcessId!.Value;
        (RenderWorkerResponse workbook, _) = Render(supervisor, "book.xlsx");

        Assert.True(document.Ok, document.Message);
        Assert.Equal("words", document.Product);
        Assert.Equal("pages", document.View);
        Assert.Contains("definePresenter", document.PresenterScript!, StringComparison.Ordinal);
        Assert.True(workbook.Ok, workbook.Message);
        Assert.Equal("cells", workbook.Product);
        Assert.Null(workbook.PresenterScript);
        Assert.Equal(worker, supervisor.ProcessId);

        JsonNode view = JsonNode.Parse(File.ReadAllText(Path.Combine(documentOutput, "view.json")))!;
        Assert.Equal(document.TotalParts, view["totalParts"]!.GetValue<int>());
        Assert.All(view["parts"]!.AsArray(), part =>
        {
            Assert.StartsWith("sha256:", part!["digest"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(documentOutput, part["file"]!.GetValue<string>())));
        });
    }

    [Fact]
    public void PreviewSnapshot_PreservesTheVerifiedResourceOrigin()
    {
        File.WriteAllBytes(_workspace.File("local.png"), Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        File.WriteAllText(_workspace.File("relative.md"), "# Relative image\n\n![local](local.png)");
        using var supervisor = new RenderWorkerSupervisor(StartInfo, RenderTimeout);
        var direct = Render(supervisor, "relative.md");
        Assert.True(direct.Response.Ok, direct.Response.Message);
        using var documents = new ViewerDocuments(supervisor, ViewerStorage.Create(), LocalServiceResourceLimits.Resolve());
        LiveDocument snapshot = documents.Open(_workspace.File("relative.md"), new LiveDocumentOptions());
        JsonNode manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(direct.Output, "view.json")))!;
        Assert.Equal(manifest["parts"]!.AsArray().Select(part => part!["digest"]!.GetValue<string>()).Order(),
            snapshot.Current!.Digests.Values.Order());
    }

    [Fact]
    public void Render_WhenTheDocumentIsUnreadable_ReportsTheProductError()
    {
        File.WriteAllText(_workspace.File("broken.docx"), "not a document");
        using var supervisor = new RenderWorkerSupervisor(StartInfo, RenderTimeout);

        (RenderWorkerResponse response, string output) = Render(supervisor, "broken.docx");

        Assert.False(response.Ok);
        Assert.NotNull(response.Code);
        Assert.DoesNotContain(output, response.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(supervisor.ProcessId);
    }

    [Fact]
    public void Render_WhenTheLicenseChanges_RecyclesTheWorker()
    {
        CreateDocument("report.docx");
        using var supervisor = new RenderWorkerSupervisor(StartInfo, RenderTimeout);
        Assert.True(Render(supervisor, "report.docx").Response.Ok);
        int evaluationWorker = supervisor.ProcessId!.Value;

        Directory.CreateDirectory(_workspace.ConfigDirectory);
        File.WriteAllText(LicenseResolver.SharedUserLicensePath(_workspace.ConfigDirectory), "<License/>");
        (RenderWorkerResponse response, _) = Render(supervisor, "report.docx");

        Assert.NotEqual(evaluationWorker, supervisor.ProcessId);
        Assert.False(response.Ok);
        Assert.Equal(ErrorCodes.LicenseInvalid.Name, response.Code);
    }

    [Fact]
    public void Render_WhenTheWorkerRunsPastItsBound_KillsItAndReportsTheTimeout()
    {
        CreateDocument("report.docx");
        using var supervisor = new RenderWorkerSupervisor(StartInfo, TimeSpan.FromMilliseconds(250));

        (RenderWorkerResponse response, _) = Render(supervisor, "report.docx");

        Assert.False(response.Ok);
        Assert.Equal(ErrorCodes.OperationTimeout.Name, response.Code);
        Assert.Null(supervisor.ProcessId);
    }

    private (RenderWorkerResponse Response, string Output) Render(
        RenderWorkerSupervisor supervisor,
        string file,
        bool presentation = false)
    {
        string output = PrivateUserStorage.EnsureDirectory(
            Path.Combine(_storage, Guid.NewGuid().ToString("N")));
        RenderWorkerResponse response = supervisor.Render(new RenderWorkerRequest
        {
            Id = 0,
            Source = _workspace.File(file),
            Output = output,
            MaxParts = 8,
            TimeoutMs = (int)RenderTimeout.TotalMilliseconds,
            Presentation = presentation,
        });
        return (response, output);
    }

    private ProcessStartInfo StartInfo()
    {
        var start = new ProcessStartInfo(CliRunner.ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _workspace.Path,
        };
        CliEnvironment.Evaluation(Directory.GetParent(_workspace.ConfigDirectory)!.FullName)
            .Apply(start.Environment);
        return start;
    }

    private void CreateDocument(string file)
    {
        File.WriteAllText(_workspace.File("report.md"), "# Report\n\nOne paragraph of prose.\n");
        Succeed(_workspace.Run("words", "create", file, "--markdown", "report.md", "--output", "json"));
    }

    private void CreateWorkbook(string file)
    {
        Succeed(_workspace.Run("cells", "create", file, "--sheets", "Data", "--output", "json"));
        Succeed(_workspace.Run("cells", "edit", file, "--in-place", "--set", "Data!A1=Region", "--output", "json"));
    }

    private static void Succeed(CliResult result) => Assert.True(result.ExitCode == 0, result.StdErr);
}
