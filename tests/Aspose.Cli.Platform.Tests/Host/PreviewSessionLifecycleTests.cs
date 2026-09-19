using Aspose.Cli.Host.Preview;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.TestKit;
using System.Text.Json;
using Xunit;

namespace Aspose.Cli.Host.Tests.PreviewInfrastructure;

public sealed class PreviewSessionLifecycleTests
{
    [Fact]
    public void InitialRender_IsBoundedWhileArtifactsAreWritten()
    {
        using var temp = new TempDirectory();
        string input = temp.File("document.txt");
        File.WriteAllText(input, "ready");
        using OperationDeadline deadline = OperationDeadline.Start(null);
        var budgets = new ResourceBudgetLedger(
            deadline,
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                [ResourceBudgetKinds.InputBytes] = 1_048_576,
            });
        using var store = new PreviewVersionStore(temp.File("versions"));
        using var hub = new LiveEventHub(TimeSpan.FromHours(1));
        using var session = new PreviewSession(
            input,
            budgets,
            context =>
            {
                context.Artifacts.Write("oversized.bin", stream =>
                    stream.SetLength(
                        LocalServiceResourceLimits.Resolve()
                            .MaximumSnapshotFileBytes
                        + 1));
                return new PreviewRenderOutcome("oversized.bin", "text", 5);
            },
            store,
            hub,
            TimeSpan.FromMilliseconds(25));

        CliException failure = Assert.Throws<CliException>(session.RenderInitial);

        Assert.Equal(ErrorCodes.PreviewBudgetExceeded, failure.Code);
        Assert.Null(session.Current);
        Assert.Empty(Directory.EnumerateDirectories(temp.File("versions")));
    }

    [Fact]
    public void AtomicChanges_PreserveLastGoodSnapshotAndRecover()
    {
        using var temp = new TempDirectory();
        string input = temp.File("document.txt");
        File.WriteAllText(input, "one");
        using OperationDeadline deadline = OperationDeadline.Start(null);
        var budgets = new ResourceBudgetLedger(
            deadline,
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                [ResourceBudgetKinds.InputBytes] = 1_048_576,
            });
        using var store = new PreviewVersionStore(temp.File("versions"));
        using var hub = new LiveEventHub(TimeSpan.FromHours(1));
        using var session = new PreviewSession(
            input,
            budgets,
            context => Render(input, context.Artifacts),
            store,
            hub,
            TimeSpan.FromMilliseconds(25));

        int failedRevision = 0;
        session.Diagnostic += message =>
        {
            if (message.StartsWith("render failed:", StringComparison.Ordinal))
            {
                Interlocked.CompareExchange(ref failedRevision, session.Revision, 0);
            }
        };
        session.RenderInitial();
        Assert.Contains("one", session.Current!.InlineHtml, StringComparison.Ordinal);

        ReplaceAtomically(input, "two");
        WaitUntil(() => session.Current?.InlineHtml?.Contains(
            "two",
            StringComparison.Ordinal) == true);
        PreviewSnapshot lastGood = session.Current!;

        ReplaceAtomically(input, "broken");
        // A revision is assigned before rendering; wait for the failed round to finish.
        WaitUntil(() => Volatile.Read(ref failedRevision) > lastGood.Revision);
        Assert.Same(lastGood, session.Current);
        Assert.Contains("two", session.Current!.InlineHtml, StringComparison.Ordinal);

        ReplaceAtomically(input, "three");
        WaitUntil(() => session.Current?.InlineHtml?.Contains(
            "three",
            StringComparison.Ordinal) == true);
        Assert.True(session.Current!.Revision > Volatile.Read(ref failedRevision));
    }

    [Fact]
    public void PublishedSnapshot_IsBoundToItsRevisionAcrossRefreshes()
    {
        using var temp = new TempDirectory();
        string input = temp.File("document.txt");
        File.WriteAllText(input, "one");
        using OperationDeadline deadline = OperationDeadline.Start(null);
        var budgets = new ResourceBudgetLedger(
            deadline,
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                [ResourceBudgetKinds.InputBytes] = 1_048_576,
            });
        using var store = new PreviewVersionStore(temp.File("versions"));
        using var hub = new LiveEventHub(TimeSpan.FromHours(1));
        using var session = new PreviewSession(
            input,
            budgets,
            context => Render(input, context.Artifacts),
            store,
            hub,
            TimeSpan.FromMilliseconds(25));

        session.RenderInitial();
        PreviewPublishedState published = session.Published;
        int initialRevision = published.Revision;
        Assert.Same(session.Current, published.Snapshot);
        Assert.Equal(published.Snapshot!.Revision, published.Revision);
        Assert.Equal(initialRevision, session.Status.Revision);

        ReplaceAtomically(input, "two");
        WaitUntil(() => session.Current?.Revision > initialRevision);
        published = session.Published;
        Assert.Same(session.Current, published.Snapshot);
        Assert.Equal(published.Snapshot!.Revision, published.Revision);
        Assert.Equal(published.Revision, session.Status.Revision);
    }

    [Fact]
    public void ControlStatus_ReturnsOnlyTheAuthenticatedRevision()
    {
        string id = Guid.NewGuid().ToString("N");
        string nonce = Guid.NewGuid().ToString("N");
        string token = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
        using var endpoint = new PreviewControlEndpoint(
            id,
            nonce,
            token,
            requestStop: static () => { },
            readStatus: static () => new PreviewRevisionStatus(7));
        endpoint.Start();
        var marker = new PreviewSessionMarker(
            id,
            token,
            Environment.ProcessId,
            StartTicksUtc: 1,
            Port: 1,
            Url: "http://127.0.0.1:1/",
            File: "document.txt",
            View: "default",
            Product: "test",
            Metadata: new ResultEnvelopeMetadata(),
            Nonce: nonce);

        LocalServiceControlResponse response = PreviewControlEndpoint.Status(marker);
        PreviewRevisionStatus actual = response.Result!.Value.Deserialize(
            PreviewLocalServiceJsonContext.Default.PreviewRevisionStatus)!;

        Assert.True(response.Ok);
        Assert.Equal(7, actual.Revision);
        Assert.DoesNotContain(token, response.Result.Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Dispose_WhenARendererDoesNotReturn_IsTimeBounded()
    {
        using var temp = new TempDirectory();
        string input = temp.File("document.txt");
        File.WriteAllText(input, "ready");
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        using OperationDeadline deadline = OperationDeadline.Start(null);
        var budgets = new ResourceBudgetLedger(
            deadline,
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                [ResourceBudgetKinds.InputBytes] = 1_048_576,
            });
        using var store = new PreviewVersionStore(temp.File("versions"));
        using var hub = new LiveEventHub(TimeSpan.FromHours(1));
        var session = new PreviewSession(
            input,
            budgets,
            context =>
            {
                if (File.ReadAllText(input) == "blocked")
                {
                    entered.Set();
                    Assert.True(resume.Wait(TimeSpan.FromSeconds(10)));
                }
                return Render(input, context.Artifacts);
            },
            store,
            hub,
            TimeSpan.FromMilliseconds(10));
        session.RenderInitial();
        ReplaceAtomically(input, "blocked");
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));

        var timer = System.Diagnostics.Stopwatch.StartNew();
        session.Dispose();
        timer.Stop();

        Assert.True(
            timer.Elapsed < PreviewSession.DefaultStopTimeout + TimeSpan.FromSeconds(1),
            $"Preview disposal exceeded its bound: {timer.Elapsed}.");
        resume.Set();
        session.WaitUntilStopped();
    }

    [Fact]
    public void ResourceCleanup_KeepsOwnedStoresUntilABlockedRendererStops()
    {
        using var temp = new TempDirectory();
        string input = temp.File("document.txt");
        File.WriteAllText(input, "ready");
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        using OperationDeadline deadline = OperationDeadline.Start(null);
        var budgets = new ResourceBudgetLedger(
            deadline,
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                [ResourceBudgetKinds.InputBytes] = 1_048_576,
            });
        PreviewSessionStorage storage = PreviewSessionStorage.Create(
            temp.File("sessions"),
            static (_, _) => false,
            static path => LocalFileCleanup.DeleteDirectory(path));
        var store = new PreviewVersionStore(storage.Root);
        var hub = new LiveEventHub(TimeSpan.FromHours(1));
        var session = new PreviewSession(
            input,
            budgets,
            context =>
            {
                if (File.ReadAllText(input) == "blocked")
                {
                    entered.Set();
                    Assert.True(resume.Wait(TimeSpan.FromSeconds(10)));
                }
                return Render(input, context.Artifacts);
            },
            store,
            hub,
            TimeSpan.FromMilliseconds(10));
        session.RenderInitial();
        ReplaceAtomically(input, "blocked");
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));

        PreviewResourceCleanup.Dispose(
            session,
            hub,
            store,
            storage);

        Assert.True(Directory.Exists(storage.Root));
        resume.Set();
        WaitUntil(() => !Directory.Exists(storage.Root));
    }

    private static PreviewRenderOutcome Render(
        string input,
        IPreviewArtifactSink artifacts)
    {
        string content = File.ReadAllText(input);
        if (content == "broken")
        {
            throw new InvalidDataException("The intermediate document is incomplete.");
        }

        artifacts.WriteText("index.html", $"<html><body>{content}</body></html>");
        return new PreviewRenderOutcome("index.html", "text", content.Length);
    }

    private static void ReplaceAtomically(string target, string content)
    {
        string sibling = target + ".replacement";
        File.WriteAllText(sibling, content);
        File.Move(sibling, target, overwrite: true);
    }

    private static void WaitUntil(Func<bool> condition) =>
        Assert.True(
            SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(10)),
            "The preview session did not observe the file replacement in time.");
}
