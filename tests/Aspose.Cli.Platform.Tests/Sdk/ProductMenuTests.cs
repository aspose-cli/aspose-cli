using System.CommandLine;
using System.Reflection;
using System.Text;
using Aspose.Cli.Architecture.Tests;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Sdk.Tests;
using Aspose.Cli.Sdk.Views;
using Aspose.Cli.TestKit;
using Xunit;
using ExtProduct = Aspose.Cli.Sdk.Extensibility.Product;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>
/// A product menu pairs each command definition with its static handler; the SDK builds the
/// command tree in menu order, runs every command in one fixed order, and registers each result
/// type's table renderer once.
/// </summary>
public sealed class ProductMenuTests : IDisposable
{
    private static readonly InputDocument Document = new("Document to open.", "the document");

    private static readonly ProductManifest Manifest = new()
    {
        Id = "menu",
        DisplayName = "Menu",
        Operations = [],
        Engine = new ProductEngineCapabilities
        {
            Id = "aspose",
            Sdk = "Aspose.Test",
            SdkVersion = "1.0.0",
            LicenseApplicable = false,
            LicenseRequired = false,
            SupportsFontDiagnostics = true,
        },
        AvailableEngines = ["aspose"],
    };

    // What the commands, guard and font scope of one test did, in order. The tests of a class
    // run one at a time.
    private static readonly List<string> Log = [];

    private readonly TempDirectory _temp = new();
    private readonly TestHosts _hosts;

    public ProductMenuTests()
    {
        Log.Clear();
        File.WriteAllText(_temp.File("doc.test"), "document");
        File.WriteAllText(_temp.File("other.test"), "other");
        Directory.CreateDirectory(_temp.File("fonts"));
        _hosts = new TestHosts(_temp.Path, () => ProductBinding.CreateLicenseFree<TestSession>(
            Manifest.Id, _ => new TestSession(Log), _ => new LoggingFonts()));
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Menu_BuildsTheProductCommandInMenuOrder()
    {
        Command product = Menu().Build().CreateCommand(_hosts);

        Assert.Equal(("menu", "A product in a menu."), (product.Name, product.Description));
        Assert.Equal(["info", "query", "edit"], product.Subcommands.Select(static command => command.Name));
        Assert.Equal(["read", "search"], product.Subcommands[1].Subcommands.Select(static command => command.Name));
        Assert.Equal("Query the document.", product.Subcommands[1].Description);
        Assert.True(product.TryGetHelpMetadata(out CommandHelpMetadata? help));
        Assert.Equal(["aspose-cli menu info doc.test"], help!.Examples);
        Assert.Equal(["aspose-cli docs menu/editing"], help.LearnMore.Select(static link => link.Command));
        Assert.True(product.Subcommands[0].TryGetHelpMetadata(out CommandHelpMetadata? info));
        Assert.Equal(["aspose-cli menu info doc.test --font-dir fonts"], info!.Examples);
        Assert.Equal(
            ["--ops", "--if-match", "--dry-run", "--best-effort", "--strict", "--out", "--overwrite", "--in-place", "--backup"],
            product.Subcommands[2].Options.Select(static option => option.Name).Take(9));
    }

    [Fact]
    public void Command_ChecksTheFontsThenBindsThenRunsTheGuardedHandlerAndFinishesInsideTheFontScope()
    {
        Command product = Menu().Build().CreateCommand(_hosts);

        TextResult result = Assert.IsType<TextResult>(Run(product, "info", "doc.test", "--font-dir", "fonts", "--password-env", "LEFT"));

        Assert.Equal(_temp.File("doc.test") + "|a|finished", result.Value);
        Assert.Equal(
            ["bind", "fonts " + _temp.File("fonts"), "guard >", "handler", "guard <", "finish", "fonts end"],
            Log);
    }

    [Fact]
    public void Command_ReportsAMissingInputThenABadFontDirectoryBeforeBindingReadsAnything()
    {
        Command product = Menu().Build().CreateCommand(_hosts);

        CliException missing = RunFailing(product, "info", "missing.test", "--font-dir", "nope", "--fail");
        CliException fonts = RunFailing(product, "info", "doc.test", "--font-dir", "nope", "--fail");

        Assert.Equal(ErrorCodes.FileNotFound, missing.Code);
        Assert.Equal(ErrorCodes.OptionInvalid, fonts.Code);
        Assert.Equal("--font-dir", fonts.Details!["option"]!.GetValue<string>());
        Assert.Empty(Log);
        Assert.Equal(ErrorCodes.UsageError, RunFailing(product, "info", "doc.test", "--fail").Code);
        Assert.Equal(["bind"], Log);
    }

    [Fact]
    public void Command_RestatesAPasswordErrorWithTheOptionOfTheInputItNames()
    {
        Command product = ExtProduct.Define<TestSession>(Manifest)
            .Describe("A product in a menu.")
            .Command(CompareCommand.Create, TestHandlers.Compare)
            .Complete()
            .Build()
            .CreateCommand(_hosts);

        CliException error = RunFailing(product, "compare", "doc.test", "other.test");

        Assert.Equal(ErrorCodes.PasswordRequired, error.Code);
        Assert.Equal("right", error.Details!["input"]!.GetValue<string>());
        Assert.Contains("--right-password-env", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Outputs_RegisterOneRendererPerResultTypeAndTheExtraTypesADefinitionNames()
    {
        ProductDefinition definition = Menu().Build();

        Assert.Equal(
            [typeof(TextResult), typeof(FoundResult), typeof(EditedResult)],
            definition.Outputs.Select(static output => output.ResultType));
    }

    [Fact]
    public void Outputs_RefuseTwoDifferentRenderersOfOneResultType()
    {
        ProductDefinition definition = ExtProduct.Define<TestSession>(Manifest)
            .Describe("A product in a menu.")
            .Command(InfoCommand.Create, TestHandlers.Info)
            .Command(OtherTextCommand.Create, TestHandlers.Info)
            .Complete()
            .Build();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => definition.Outputs);
        Assert.Contains(typeof(TextResult).FullName!, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RequiresADescriptionForAMenu()
    {
        ProductDefinitionBuilder<TestSession> builder = ExtProduct.Define<TestSession>(Manifest)
            .Command(InfoCommand.Create, TestHandlers.Info)
            .Complete();

        Assert.Contains("Describe", Assert.Throws<InvalidOperationException>(builder.Build).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Edit_ChecksUsageBeforeReadingTheOperationsThenBindsTheBatch()
    {
        Command product = Menu().Build().CreateCommand(_hosts);

        CliException strict = RunFailing(product, "edit", "doc.test", "--ops", "missing.json", "--strict");
        EditedResult edited = Assert.IsType<EditedResult>(
            Run(product, "edit", "doc.test", "--ops", """{"ops":[{"op":"set","value":7}]}""", "--dry-run"));

        Assert.Equal(ErrorCodes.OptionInvalid, strict.Code);
        Assert.Equal("--strict", strict.Details!["option"]!.GetValue<string>());
        Assert.Equal((1, true), (edited.Operations, edited.DryRun));
    }

    [Fact]
    public void EncryptPassword_UsesTheFormatTheProductDetectsInTheEditedInput()
    {
        static ProductDefinitionBuilder<TestSession> Product(Func<string, string?>? detect)
        {
            ProductDefinitionBuilder<TestSession> builder = ExtProduct.Define<TestSession>(Manifest)
                .Describe("A product in a menu.")
                .Command(ProtectCommand.Create, TestHandlers.Protect)
                .Complete();
            return detect is null ? builder : builder.DetectFormat(detect);
        }

        Command judged = Product(null).Build().CreateCommand(_hosts);
        Command detected = Product(static _ => "old").Build().CreateCommand(_hosts);

        Assert.Equal("secret", Assert.IsType<TextResult>(Run(judged, "protect", "doc.test", "--encrypt", "secret")).Value);
        CliException refused = RunFailing(detected, "protect", "doc.test", "--encrypt", "secret");
        Assert.Equal(ErrorCodes.OptionInvalid, refused.Code);
        Assert.Equal("--encrypt", refused.Details!["option"]!.GetValue<string>());
    }

    [Fact]
    public void Guard_WrapsEveryViewAdapterCall()
    {
        ProductViewDefinition view = Menu().Build().View;
        ProductBinding<TestSession> binding = ProductBinding.CreateLicenseFree<TestSession>(
            Manifest.Id, static _ => new TestSession(Log));
        var request = new ViewRenderRequest { View = "document", MaxPartCount = 1, Purpose = ViewPurpose.Evidence };

        ViewManifest rendered = view.Render(binding, "doc.test", request, new NoArtifacts());
        _ = view.Assess(binding, "doc.test", request, rendered);

        Assert.Equal(["guard >", "guard <", "guard >", "guard <"], Log);
    }

    /// <summary>
    /// A failure is attributed to the product whose module defined it, not to the assembly of its
    /// session type, which may be a type the product does not own.
    /// </summary>
    [Fact]
    public void Catalog_AttributesAProductToItsModuleAssembly()
    {
        ProductDefinition definition = ExtProduct.Define<StringBuilder>(Manifest)
            .Describe("A product in a menu.")
            .Command(InfoCommand.Create, static (StringBuilder _, InfoRequest _) => new TextResult("x"))
            .Formats([])
            .Diagnostics([])
            .Json(new ProductJsonDefinition(Manifest.Id, SdkJsonContext.Default))
            .View(new TestProductViewAdapter<StringBuilder>())
            .Activator(static _ => throw new InvalidOperationException("Not activated."))
            .Build();
        Assert.Throws<InvalidOperationException>(() => definition.ModuleAssembly);

        ProductCatalog catalog = ProductCatalog.Build([new StaticModule(definition)]);
        CliException? error = EngineFailureTranslator.Create(catalog).Translate(Caught(static () => Assert.Fail("engine choked")));

        Assert.Equal(typeof(ProductMenuTests).Assembly, Assert.Single(catalog.Products).ModuleAssembly);
        Assert.StartsWith("Menu failed inside its document engine", error?.Message, StringComparison.Ordinal);
    }

    private static ProductDefinitionBuilder<TestSession> Menu() =>
        ExtProduct.Define<TestSession>(Manifest)
            .Describe("A product in a menu.", static () => new CommandHelp(
                ["menu info doc.test"],
                [CommandHelpLink.Docs(Manifest, "editing", "editing")]))
            .Guard(static (session, run) =>
            {
                session.Log.Add("guard >");
                object? result = run();
                session.Log.Add("guard <");
                return result;
            })
            .Command(InfoCommand.Create, TestHandlers.Info)
            .Group("query", "Query the document.", static query => query
                .Command(ReadCommand.Create, TestHandlers.Info)
                .Command(SearchCommand.Create, TestHandlers.Search))
            .Command(EditCommand.Create, TestHandlers.Edit)
            .Complete();

    private ResultEnvelope Run(Command product, params string[] arguments)
    {
        _hosts.Error = null;
        ParseResult parse = new RootCommand { product }.Parse(["menu", .. arguments]);
        Assert.Empty(parse.Errors);
        parse.Invoke();
        return _hosts.Error is { } error ? throw error : _hosts.Result!;
    }

    private CliException RunFailing(Command product, params string[] arguments)
    {
        _hosts.Error = null;
        ParseResult parse = new RootCommand { product }.Parse(["menu", .. arguments]);
        Assert.Empty(parse.Errors);
        parse.Invoke();
        return Assert.IsType<CliException>(_hosts.Error);
    }

    private static Exception Caught(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException("The action did not throw.");
    }

    public sealed record TestSession(List<string> Log);

    public sealed record InfoRequest
    {
        public required string Input { get; init; }

        public string? Password { get; init; }
    }

    public sealed record CompareRequest(string Input, string Other);

    public sealed record EditRequest(int Operations, bool DryRun);

    public sealed record ProtectRequest(string? Password);

#pragma warning disable APCLI003 // Test results, not product JSON roots.
    public sealed record TextResult(string Value) : ResultEnvelope("test/text", 1);

    public sealed record FoundResult() : ResultEnvelope("test/found", 1);

    public sealed record EditedResult(int Operations, bool DryRun) : ResultEnvelope("test/edited", 1);
#pragma warning restore APCLI003

    private static class TestHandlers
    {
        public static TextResult Info(TestSession session, InfoRequest request)
        {
            session.Log.Add("handler");
            return new TextResult(request.Input + "|" + request.Password);
        }

        public static ResultEnvelope Search(TestSession session, InfoRequest request) => new FoundResult();

        public static TextResult Compare(TestSession session, CompareRequest request) =>
            throw CliErrors.PasswordRequired(request.Other);

        public static EditedResult Edit(TestSession session, EditRequest request) =>
            new(request.Operations, request.DryRun);

        public static TextResult Protect(TestSession session, ProtectRequest request) => new(request.Password ?? string.Empty);
    }

    private static class TestTables
    {
        public static void Text(TextResult result, TableSurface table)
        {
        }

        public static void Found(FoundResult result, TableSurface table)
        {
        }

        public static void Edited(EditedResult result, TableSurface table)
        {
        }
    }

    private static class InfoCommand
    {
        public static CommandDefinition<InfoRequest, TextResult> Create()
        {
            var fail = new Option<bool>("--fail") { Description = "Fail the usage check." };
            return new(
                "info",
                "Shows the document.",
                new CommandTraits { Input = Document, UsesFonts = true },
                [fail],
                (parse, standard) =>
                {
                    Log.Add("bind");
                    return parse.GetValue(fail)
                        ? throw CliErrors.Usage(["--fail was given."])
                        : new InfoRequest { Input = standard.Input, Password = standard.InputPassword?.Reveal() };
                },
                TestTables.Text)
            {
                Finish = static (_, _, result, _) =>
                {
                    Log.Add("finish");
                    return result with { Value = result.Value + "|finished" };
                },
                Examples = ["menu info doc.test --font-dir fonts"],
            };
        }
    }
    private static class ReadCommand
    {
        public static CommandDefinition<InfoRequest, TextResult> Create() => new(
            "read",
            "Reads the document.",
            new CommandTraits { Input = Document },
            [],
            static (_, standard) => new InfoRequest { Input = standard.Input },
            TestTables.Text);
    }

    /// <summary>A command whose handler returns one of several result types, each with its own renderer.</summary>
    private static class SearchCommand
    {
        public static CommandDefinition<InfoRequest, ResultEnvelope> Create() => new(
            "search",
            "Searches the document.",
            new CommandTraits { Input = Document },
            [],
            static (_, standard) => new InfoRequest { Input = standard.Input },
            table: null)
        {
            Renderers = [ProductOutputDefinition.Create<TextResult>(TestTables.Text), ProductOutputDefinition.Create<FoundResult>(TestTables.Found)],
        };
    }

    /// <summary>Renders the result type <see cref="InfoCommand"/> renders, with another method.</summary>
    private static class OtherTextCommand
    {
        public static CommandDefinition<InfoRequest, TextResult> Create() => new(
            "other",
            "Reads the document again.",
            new CommandTraits { Input = Document },
            [],
            static (_, standard) => new InfoRequest { Input = standard.Input },
            static (_, _) => { });
    }

    private static class CompareCommand
    {
        public static CommandDefinition<CompareRequest, TextResult> Create() => new(
            "compare",
            "Compares two documents.",
            new CommandTraits
            {
                Input = new InputDocument("Baseline.", "the baseline", "left"),
                Other = new InputDocument("Candidate.", "the candidate", "right"),
            },
            [],
            static (_, standard) => new CompareRequest(standard.Input, standard.Other),
            TestTables.Text);
    }

    private static class EditCommand
    {
        public static CommandDefinition<EditRequest, EditedResult> Create()
        {
            var strict = new Option<bool>("--strict") { Description = "Refuse every operation document file." };
            var edit = new BoundedEditCommand<TestOp, TestBatch>(new BoundedEditDefinition<TestOp, TestBatch>
            {
                Contracts = TestContracts.Json,
                Writes = [FormatDescriptor.Declare("tst", FormatUse.Input | FormatUse.Convert, 0, 0, null, false, ".test")],
            });
            return EditDefinition.Create<TestOp, TestBatch, EditRequest, EditedResult>(
                edit,
                "Edits the document.",
                new CommandTraits { Input = Document },
                [strict],
                static (_, batch, _) => new EditRequest(batch.Batch.Ops.Count, batch.Options.DryRun),
                TestTables.Edited,
                checkUsage: parse =>
                {
                    if (parse.GetValue(strict))
                    {
                        throw CliErrors.OptionInvalid("--strict", "refuses every operation document", "Omit --strict.");
                    }
                });
        }
    }

    /// <summary>An edit whose output extension names a protectable and an unprotectable format.</summary>
    private static class ProtectCommand
    {
        public static CommandDefinition<ProtectRequest, TextResult> Create() => new(
            "protect",
            "Protects the document.",
            new CommandTraits
            {
                Input = Document,
                Output = OutputTarget.Mutation(
                [
                    FormatDescriptor.Declare("tst", FormatUse.Input | FormatUse.Convert, 0, 0, null, false, ".test") with { Protectable = true },
                    FormatDescriptor.Declare("old", FormatUse.Input | FormatUse.Convert, 1, 1, null, false, ".test"),
                ]),
                Encrypt = new EncryptedOutput("the output document"),
            },
            [],
            static (_, standard) => new ProtectRequest(standard.EncryptPassword()?.Reveal()),
            TestTables.Text);
    }

    private sealed class LoggingFonts : IFontEnvironment
    {
        public FontListResult ListFonts() => throw new NotSupportedException();

        public FontCheckResult CheckFonts(string filePath, FontCheckRequest request) => throw new NotSupportedException();

        public IDisposable UseFonts(FontSearchProfile profile)
        {
            Log.Add("fonts " + string.Join(';', profile.Directories));
            return new Scope();
        }

        private sealed class Scope : IDisposable
        {
            public void Dispose() => Log.Add("fonts end");
        }
    }

    private sealed class NoArtifacts : IViewArtifactSink
    {
        public void Write(string relativePath, Action<Stream> contentWriter) =>
            throw new InvalidOperationException("The test view writes no artifacts.");

        public void WriteText(string relativePath, string content) =>
            throw new InvalidOperationException("The test view writes no artifacts.");
    }

    private sealed class StaticModule(ProductDefinition definition) : IProductModule
    {
        public ProductDefinition Define() => definition;
    }

    /// <summary>The host side of the tests: a binding per run, and the result or the error of the last run.</summary>
    private sealed class TestHosts(string workDirectory, Func<ProductBinding> binding) : IProductCommandHostFactory
    {
        public ResultEnvelope? Result { get; private set; }

        public Exception? Error { get; set; }

        public IProductCommandHost<TPort> Create<TPort>(string productId)
            where TPort : class =>
            new Host<TPort>(this);

        private ProductCommandContext<TPort> Context<TPort>()
            where TPort : class => new()
            {
                Binding = (ProductBinding<TPort>)binding(),
                Paths = new PathResolver(workDirectory),
                Inputs = TestBudgets.Create().Inputs,
                ReadEnvironment = static name => name == "LEFT" ? "a" : null,
            };

        private sealed class Host<TPort>(TestHosts owner) : IProductCommandHost<TPort>
            where TPort : class
        {
            public int Run(ParseResult parseResult, Func<ProductCommandContext<TPort>, ResultEnvelope> handler)
            {
                try
                {
                    owner.Result = handler(owner.Context<TPort>());
                }
                catch (Exception exception)
                {
                    owner.Result = null;
                    owner.Error = exception;
                }

                return 0;
            }
        }
    }
}

/// <summary>Completes a test product definition around its menu.</summary>
internal static class ProductMenuTestDefinitions
{
    public static ProductDefinitionBuilder<ProductMenuTests.TestSession> Complete(
        this ProductDefinitionBuilder<ProductMenuTests.TestSession> builder) =>
        builder
            .Formats([])
            .Diagnostics([])
            .Json(new ProductJsonDefinition("menu", SdkJsonContext.Default))
            .View(new TestProductViewAdapter<ProductMenuTests.TestSession>())
            .Activator(static _ => throw new InvalidOperationException("The tests bind through their host."));
}
