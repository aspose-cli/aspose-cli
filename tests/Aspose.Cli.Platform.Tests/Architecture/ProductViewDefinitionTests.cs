using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Tests;
using Aspose.Cli.Sdk.Views;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

public sealed class ProductViewDefinitionTests
{
    [Fact]
    public void ViewDefinition_RejectsAnUndeclaredViewBeforeProductDispatch()
    {
        ProductViewDefinition views = ProductViewDefinition.Create(
            new TestProductViewAdapter<ITestSession>(),
            "test");
        ProductBinding<ITestSession> binding = TestBindings.Create<ITestSession>("test", static () => new TestSession());

        CliException failure = Assert.Throws<CliException>(() =>
            views.Render(binding, "file.test", Request("missing"), new RejectingSink()));

        Assert.Equal(ErrorCodes.OptionInvalid, failure.Code);
    }

    private static ViewRenderRequest Request(string view) => new()
    {
        View = view,
        MaxPartCount = 1,
        Purpose = ViewPurpose.Evidence,
    };

    private sealed class RejectingSink : IViewArtifactSink
    {
        public void Write(string relativePath, Action<Stream> contentWriter) =>
            throw new InvalidOperationException("The test view writes no artifacts.");

        public void WriteText(string relativePath, string content) =>
            throw new InvalidOperationException("The test view writes no artifacts.");

        public bool TryWritePart(string relativePath, Action<Stream> contentWriter) =>
            throw new InvalidOperationException("The test view writes no artifacts.");
    }

    private interface ITestSession;

    private sealed class TestSession : ITestSession;
}
