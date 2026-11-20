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
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Sdk.Tests;
using Aspose.Cli.TestKit;
using Xunit;
using ExtProduct = Aspose.Cli.Sdk.Extensibility.Product;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>
/// A product menu pairs each command definition with its static handler; the SDK builds the
/// command tree in menu order and runs every command in one fixed order.
/// </summary>
public sealed class ProductMenuTests : IDisposable
{
    private static readonly InputDocument Document = new("Document to open.", "the document");

    private static readonly ProductManifest Manifest = new()
    {
        Id = "menu",
        DisplayName = "Menu",
        Engine = new Aspose.Cli.Sdk.Extensibility.ProductEngine
        {
            Sdk = "Aspose.Test",
            SdkVersion = "1.0.0",
        },
    };

    // What the commands, guard and font scope of one test did, in order. The tests of a class
    // run one at a time.
    private static readonly List<string> Log = [];

    private readonly TempDirectory _temp = new();
    private readonly TestCommandRunner _hosts;

    public ProductMenuTests()
    {
        Log.Clear();
        File.WriteAllText(_temp.File("doc.test"), "document");
        File.WriteAllText(_temp.File("other.test"), "other");
        Directory.CreateDirectory(_temp.File("fonts"));
        _hosts = new TestCommandRunner(
            _temp.Path,
            static context => TestBindings.Create(Manifest.Id, static () => new TestSession(Log), static () => new LoggingFonts(), context),
            static name => name == "LEFT" ? "a" : null);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Menu_BuildsTheProductCommandInMenuOrder()
    {
        Command product = Menu().Build().CreateCommand(_hosts.Run);

        Assert.Equal(("menu", "A product in a menu."), (product.Name, product.Description));
        Assert.Equal(["inspect", "read", "search"], product.Subcommands.Select(static command => command.Name));
        Assert.True(product.TryGetHelpMetadata(out CommandHelpMetadata? help));
        Assert.Equal(["aspose-cli menu inspect doc.test"], help!.Examples);
        Assert.Equal([$"aspose-cli schema {Aspose.Cli.Sdk.Contracts.ResultEnvelope.CatalogId("menu", "inspect-result")}"], help.LearnMore.Select(static link => link.Command));
        Assert.True(product.Subcommands[0].TryGetHelpMetadata(out CommandHelpMetadata? info));
        Assert.Equal(["aspose-cli menu inspect doc.test --font-dir fonts"], info!.Examples);
    }

    [Fact]
    public void Command_ChecksTheFontsThenBindsThenRunsTheGuardedHandlerAndFinishesInsideTheFontScope()
    {
        Command product = Menu().Build().CreateCommand(_hosts.Run);

        TextResult result = Assert.IsType<TextResult>(Run(product, "inspect", "doc.test", "--font-dir", "fonts", "--password-env", "LEFT"));

        Assert.Equal(_temp.File("doc.test") + "|a|finished", result.Value);
        Assert.Equal(
            ["bind", "fonts " + _temp.File("fonts"), "guard >", "handler", "guard <", "finish", "fonts end"],
            Log);
    }

    [Fact]
    public void Command_ReportsAMissingInputThenABadFontDirectoryBeforeBindingReadsAnything()
    {
        Command product = Menu().Build().CreateCommand(_hosts.Run);

        CliException missing = RunFailing(product, "inspect", "missing.test", "--font-dir", "nope", "--fail");
        CliException fonts = RunFailing(product, "inspect", "doc.test", "--font-dir", "nope", "--fail");

        Assert.Equal(ErrorCodes.FileNotFound, missing.Code);
        Assert.Equal(ErrorCodes.OptionInvalid, fonts.Code);
        Assert.Equal("--font-dir", fonts.Details!["option"]!.GetValue<string>());
        Assert.Empty(Log);
        Assert.Equal(ErrorCodes.UsageError, RunFailing(product, "inspect", "doc.test", "--fail").Code);
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
            .CreateCommand(_hosts.Run);

        CliException error = RunFailing(product, "compare", "doc.test", "other.test");

        Assert.Equal(ErrorCodes.PasswordRequired, error.Code);
        Assert.Equal("right", error.Details!["argument"]!.GetValue<string>());
    }

    [Fact]
    public void Build_RequiresADescriptionForAMenu()
    {
        ProductDefinitionBuilder<TestSession> builder = ExtProduct.Define<TestSession>(Manifest)
            .Command(InspectCommand.Create, TestHandlers.Info)
            .Complete();

        Assert.Contains("Describe", Assert.Throws<InvalidOperationException>(builder.Build).Message, StringComparison.Ordinal);
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

        Command judged = Product(null).Build().CreateCommand(_hosts.Run);
        Command detected = Product(static _ => "old").Build().CreateCommand(_hosts.Run);

        Assert.Equal("secret", Assert.IsType<TextResult>(Run(judged, "protect", "doc.test", "--encrypt", "secret")).Value);
        CliException refused = RunFailing(detected, "protect", "doc.test", "--encrypt", "secret");
        Assert.Equal(ErrorCodes.OptionInvalid, refused.Code);
        Assert.Equal("--encrypt", refused.Details!["option"]!.GetValue<string>());
    }

    [Fact]
    public void EncryptPassword_IgnoresTheDetectorForACommandThatDoesNotEditItsInput()
    {
        Command product = ExtProduct.Define<TestSession>(Manifest)
            .Describe("A product in a menu.")
            .Command(ProtectedCopyCommand.Create, TestHandlers.Protect)
            .Complete()
            .DetectFormat(static _ => throw new InvalidOperationException("Only an edit detects its input's format."))
            .Build()
            .CreateCommand(_hosts.Run);

        TextResult copied = Assert.IsType<TextResult>(
            Run(product, "copy", "doc.test", "--out", "copy.test", "--encrypt", "secret"));

        Assert.Equal("secret", copied.Value);
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
            .Command(InspectCommand.Create, static (StringBuilder _, InfoRequest _) => new TextResult("x"))
            .Formats([])
            .Diagnostics([])
            .Json(new ProductJsonDefinition(Manifest.Id, SdkJsonContext.Default))
            .Activator(static _ => throw new InvalidOperationException("Not activated."))
            .Build();

        ProductCatalog catalog = ProductCatalog.Build([new StaticModule(definition)]);
        CliException? error = EngineFailureTranslator.Create(catalog).Translate(Caught(static () => Assert.Fail("engine choked")));

        Assert.Equal(typeof(ProductMenuTests).Assembly, Assert.Single(catalog.Resources.Products).ResourceAssembly);
        Assert.StartsWith("Menu failed inside its document engine", error?.Message, StringComparison.Ordinal);
    }

    private static ProductDefinitionBuilder<TestSession> Menu() =>
        ExtProduct.Define<TestSession>(Manifest)
            .Describe("A product in a menu.", static () => new CommandHelp(
                ["menu inspect doc.test"],
                [CommandHelpLink.Schema(Manifest, "inspect-result", "the inspect result")]))
            .Guard(static (session, run) =>
            {
                session.Log.Add("guard >");
                object? result = run();
                session.Log.Add("guard <");
                return result;
            })
            .Command(InspectCommand.Create, TestHandlers.Info)
            .Command(ReadCommand.Create, TestHandlers.Info)
            .Command(SearchCommand.Create, TestHandlers.Search)
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

    public sealed record ProtectRequest(string? Password);

#pragma warning disable APCLI003 // Test results, not product JSON roots.
    public sealed record TextResult(string Value) : TestResultEnvelope("text");

    public sealed record FoundResult() : TestResultEnvelope("found");

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

        public static TextResult Protect(TestSession session, ProtectRequest request) => new(request.Password ?? string.Empty);
    }

    private static class InspectCommand
    {
        public static CommandDefinition<InfoRequest, TextResult> Create()
        {
            var fail = new Option<bool>("--fail") { Description = "Fail the usage check." };
            return new(
                "inspect",
                "Shows the document.",
                new CommandTraits { Input = Document, UsesFonts = true },
                [fail],
                (parse, standard) =>
                {
                    Log.Add("bind");
                    return parse.GetValue(fail)
                        ? throw CliErrors.Usage(["--fail was given."])
                        : new InfoRequest { Input = standard.Input, Password = standard.InputPassword?.Reveal() };
                })
            {
                Finish = static (_, _, result, _) =>
                {
                    Log.Add("finish");
                    return result with { Value = result.Value + "|finished" };
                },
                Examples = ["menu inspect doc.test --font-dir fonts"],
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
            static (_, standard) => new InfoRequest { Input = standard.Input })
        {
        };
    }

    /// <summary>A command whose handler returns one of several result types.</summary>
    private static class SearchCommand
    {
        public static CommandDefinition<InfoRequest, ResultEnvelope> Create() => new(
            "search",
            "Searches the document.",
            new CommandTraits { Input = Document },
            [],
            static (_, standard) => new InfoRequest { Input = standard.Input });
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
            static (_, standard) => new CompareRequest(standard.Input, standard.Other));
    }

    /// <summary>An edit whose output extension names a protectable and an unprotectable format.</summary>
    private static class ProtectCommand
    {
        public static readonly FormatDescriptor[] Writes =
        [
            FormatDescriptor.Declare("tst", FormatUse.Input | FormatUse.Convert, ".test") with { Protectable = true },
            FormatDescriptor.Declare("old", FormatUse.Input | FormatUse.Convert, ".test"),
        ];

        public static CommandDefinition<ProtectRequest, TextResult> Create() => new(
            "protect",
            "Protects the document.",
            new CommandTraits
            {
                Input = Document,
                Output = OutputTarget.Mutation(Writes),
                Encrypt = new EncryptedOutput("the output document"),
            },
            [],
            static (_, standard) => new ProtectRequest(standard.EncryptPassword()?.Reveal()));
    }

    /// <summary>A conversion to the same output formats, which does not keep its input's format.</summary>
    private static class ProtectedCopyCommand
    {
        public static CommandDefinition<ProtectRequest, TextResult> Create() => new(
            "copy",
            "Writes a protected copy of the document.",
            new CommandTraits
            {
                Input = Document,
                Output = OutputTarget.File("Output path.", ProtectCommand.Writes, required: true),
                Encrypt = new EncryptedOutput("the output document"),
            },
            [],
            static (_, standard) => new ProtectRequest(standard.EncryptPassword()?.Reveal()));
    }

    private sealed class LoggingFonts : IFontEnvironment
    {
        public EngineFonts ListFonts() => throw new NotSupportedException();

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

    private sealed class StaticModule(ProductDefinition definition) : IProductModule
    {
        public ProductDefinition Define() => definition;
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
            .Activator(static _ => throw new InvalidOperationException("The tests bind through their host."));
}
