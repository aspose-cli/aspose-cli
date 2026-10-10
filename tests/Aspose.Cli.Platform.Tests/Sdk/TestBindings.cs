using System.CommandLine;
using Aspose.Cli.Architecture.Tests;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Sdk.Tests;

/// <summary>
/// Product bindings for SDK tests over the SDK write pipeline that
/// <see cref="ProductBinding.Create{TSession, TDocument}"/> gives products, but for an engine to
/// which licensing does not apply: no license source is read and no evaluation mark is found.
/// </summary>
internal static class TestBindings
{
    /// <summary>An activation context that asks for evaluation mode, so no license source is read.</summary>
    public static ProductActivationContext Context(string workDirectory, ResourceBudgetLedger? budgets = null)
    {
        ResourceBudgetLedger ledger = budgets ?? TestBudgets.Create();
        return new ProductActivationContext
        {
            WorkDirectory = workDirectory,
            LicensePath = null,
            EvaluationRequested = true,
            ConfigDirectory = workDirectory,
            EnvironmentVariable = static _ => null,
            SafeFileWriter = new SafeFileWriter(ledger),
            ResourceBudgets = ledger,
        };
    }

    /// <summary>A binding of <paramref name="session"/>, whose font environment is <paramref name="fonts"/> or one that finds every font.</summary>
    public static ProductBinding<TSession> Create<TSession>(
        string productId,
        Func<TSession> session,
        Func<IFontEnvironment>? fonts = null,
        ProductActivationContext? context = null)
        where TSession : class =>
        new(
            productId,
            new Lazy<TSession>(session),
            NoLicense.Instance,
            new Lazy<IFontEnvironment>(() => fonts?.Invoke() ?? new AvailableFonts()),
            new OutputPipeline<object>(NoLicense.Instance, new NoEvaluationMarks(), (context ?? Context(Path.GetTempPath())).SafeFileWriter));

    private sealed class NoLicense : ILicenseGate
    {
        public static NoLicense Instance { get; } = new();

        public bool IsApplicable => false;

        public LicenseResolution Resolution => LicenseResolution.None;

        public string Identity => "none";

        public LicenseState EnsureApplied() => LicenseState.NotApplicable;
    }

    private sealed class NoEvaluationMarks : IEvaluationProfile<object>
    {
        public EvaluationMarks Inspect(object document) => EvaluationMarks.None;
    }

    private sealed class AvailableFonts : IFontEnvironment
    {
        public FontListResult ListFonts() => new() { Sources = [] };

        public FontCheckResult CheckFonts(string filePath, FontCheckRequest request) => new()
        {
            Source = new SourceInfo { Path = filePath, Format = "test", SizeBytes = 0 },
            AllAvailable = true,
            Fonts = [],
        };

        public IDisposable UseFonts(FontSearchProfile profile) => new NoScope();

        private sealed class NoScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}

/// <summary>A test product, <c>test</c>, around a menu of test commands.</summary>
internal static class TestProducts
{
    public static ProductManifest Manifest { get; } = new()
    {
        Id = "test",
        DisplayName = "Test",
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

    /// <summary>The product command whose menu <paramref name="menu"/> adds and whose commands run through <paramref name="runner"/>.</summary>
    public static Command Command<TSession>(
        TestCommandRunner runner,
        Func<ProductDefinitionBuilder<TSession>, ProductDefinitionBuilder<TSession>> menu)
        where TSession : class =>
        menu(Extensibility.Product.Define<TSession>(Manifest).Describe("Tests."))
            .Formats([])
            .Diagnostics([])
            .Json(new ProductJsonDefinition(Manifest.Id, SdkJsonContext.Default))
            .View(new TestProductViewAdapter<TSession>())
            .Activator(static _ => throw new InvalidOperationException("The tests bind through their runner."))
            .Build()
            .CreateCommand(runner.Run);
}

/// <summary>
/// The host side of SDK command tests: it runs each product command with a fresh binding and
/// keeps the result or the error of the last run.
/// </summary>
/// <param name="workDirectory">The invocation working directory.</param>
/// <param name="bind">Binds the product for one run.</param>
/// <param name="readEnvironment">The environment secrets of every run.</param>
internal sealed class TestCommandRunner(
    string workDirectory,
    Func<ProductActivationContext, ProductBinding> bind,
    Func<string, string?> readEnvironment)
{
    public ResultEnvelope? Result { get; private set; }

    public Exception? Error { get; set; }

    public int Run(ParseResult parse, Func<ProductCommandScope, ResultEnvelope> run)
    {
        ResourceBudgetLedger budgets = TestBudgets.Create();
        try
        {
            Result = run(new ProductCommandScope(
                bind(TestBindings.Context(workDirectory, budgets)),
                new PathResolver(workDirectory),
                budgets.Inputs,
                readEnvironment));
        }
        catch (Exception exception)
        {
            Result = null;
            Error = exception;
        }

        return 0;
    }
}
