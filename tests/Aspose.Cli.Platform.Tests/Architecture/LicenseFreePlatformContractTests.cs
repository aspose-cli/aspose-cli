using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Aspose.Cli.Host;
using Aspose.Cli.Host.Commands;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Serialization;
using Xunit;
using ExtProduct = Aspose.Cli.Sdk.Extensibility.Product;
using Aspose.Cli.Platform.Tests;

namespace Aspose.Cli.Architecture.Tests;

[Collection("Console capture")]
public sealed class LicenseFreePlatformContractTests
{
    [Fact]
    public void LicenseFreeCatalog_IsHonestAndRejectsProvisioningBeforeFileAccess()
    {
        ProductCatalog catalog = Catalog();
        var host = new HostContext(catalog);
        RootCommand root = RootCommandFactory.Create(host, out _);

        Assert.DoesNotContain(
            root.Subcommands,
            static command => command.Name == "fonts");

        Assert.DoesNotContain(
            root.Subcommands,
            static command => command.Name == "license");
        Assert.DoesNotContain(
            root.Options,
            static option => option.Name == "--license");

        ParseResult licenseParse = root.Parse(["license", "status"]);
        Assert.NotEmpty(licenseParse.Errors);
        ParseResult licenseOptionParse = root.Parse(["doctor", "--license", "missing.lic"]);
        Assert.NotEmpty(licenseOptionParse.Errors);

        InvocationResult schemaJson = Invoke(root, "schema", "--output", "json");
        Assert.Equal(0, schemaJson.ExitCode);
        using (JsonDocument schema = JsonDocument.Parse(schemaJson.StandardOutput))
        {
            Assert.Equal(
                ResultEnvelope.SchemaUri("common", "schema-list"),
                schema.RootElement.GetProperty("schema").GetString());
            Assert.Contains(
                "v2/common/schema-list",
                schema.RootElement.GetProperty("schemas")
                    .EnumerateArray()
                    .Select(item => item.GetString()));
        }

        InvocationResult schemaTable = Invoke(root, "schema", "--output", "table");
        Assert.Contains("v2/common/schema-list", schemaTable.StandardOutput, StringComparison.Ordinal);
        InvocationResult schemaMarkdown = Invoke(root, "schema", "--output", "markdown");
        Assert.Contains("| id |", schemaMarkdown.StandardOutput, StringComparison.Ordinal);

        InvocationResult schemaRaw = Invoke(root, "schema", "v2/common/error");
        Assert.Equal(0, schemaRaw.ExitCode);
        using JsonDocument raw = JsonDocument.Parse(schemaRaw.StandardOutput);
        Assert.Equal(
            "https://schemas.aspose.com/aspose-cli/v2/common/error.schema.json",
            raw.RootElement.GetProperty("$id").GetString());

        InvocationResult capabilities = Invoke(root, "capabilities", "--output", "json");
        Assert.Equal(0, capabilities.ExitCode);
        using (JsonDocument capabilityJson = JsonDocument.Parse(capabilities.StandardOutput))
        {
            JsonElement capabilityRoot = capabilityJson.RootElement;
            JsonElement engine = capabilityRoot.GetProperty("products")[0]
                .GetProperty("engine");
            Assert.False(engine.GetProperty("licenseApplicable").GetBoolean());
            Assert.DoesNotContain(
                capabilityRoot.GetProperty("diagnostics").EnumerateArray(),
                diagnostic =>
                    diagnostic.GetProperty("code").GetString()!.StartsWith(
                        "LICENSE_",
                        StringComparison.Ordinal)
                    || diagnostic.GetProperty("code").GetString() is
                        "EVALUATION_LIMIT" or "EVAL_MODE");
        }

        InvocationResult doctor = Invoke(
            root,
            "doctor",
            "--output",
            "json");
        Assert.Equal(0, doctor.ExitCode);
        using JsonDocument doctorJson = JsonDocument.Parse(doctor.StandardOutput);
        Assert.DoesNotContain(
            doctorJson.RootElement.GetProperty("checks").EnumerateArray(),
            item => item.GetProperty("name").GetString() == "license");
    }

    [Fact]
    public void LicenseFreeBinding_DoesNotReportEvaluationOrCreateFontDiagnostics()
    {
        ProductBinding<ITestSession> binding =
            ProductBinding.CreateLicenseFree<ITestSession>(
                "free-test",
                static gate => new TestSession(gate));

        Assert.False(binding.LicenseGate.IsApplicable);
        Assert.Equal(LicenseResolution.None, binding.LicenseGate.Resolution);
        Assert.Equal(
            LicenseState.NotApplicable,
            binding.LicenseGate.EnsureApplied());
        Assert.Equal(
            LicenseModes.NotApplicable,
            EnvelopeParts.License(
                binding.LicenseGate.EnsureApplied()).Mode);
        // Without a write pipeline nothing discloses evaluation output.
        Assert.Null(binding.Publishing);
        Assert.False(binding.HasFontEnvironment);
        Assert.Null(binding.FontEnvironment);
        Assert.Same(binding.LicenseGate, binding.Session.LicenseGate);
    }

    /// <summary>
    /// The diagnostics only licensing can produce: the license errors, the license commands'
    /// refusal and the evaluation warnings. Whether a diagnostic belongs here is declared, not
    /// guessed from how its code is spelled, so EVAL_INPUT_TRUNCATED is left out too.
    /// </summary>
    private static readonly string[] LicenseDiagnostics =
    [
        "EVALUATION_LIMIT", "EVAL_INPUT_MARKED", "EVAL_INPUT_TRUNCATED", "EVAL_MODE",
        "LICENSE_FILE_NOT_FOUND", "LICENSE_INVALID", "LICENSE_NOT_APPLICABLE",
    ];

    [Fact]
    public void LicenseFreeCapabilities_LeaveOutExactlyTheLicenseDiagnostics()
    {
        string[] licensed = DiagnosticCodes(Catalog(licensingApplicable: true));
        string[] free = DiagnosticCodes(Catalog());

        Assert.True(LicenseDiagnostics.All(licensed.Contains),
            "A licensed build lists every license diagnostic; missing: "
            + string.Join(", ", LicenseDiagnostics.Where(code => !licensed.Contains(code))));
        // The live build's license errors are all in the list, so it stays complete.
        string[] liveLicenseErrors =
        [
            .. Aspose.Cli.TestKit.Scenarios.CliCatalog.Current.Document["diagnostics"]!.AsArray()
                .Where(static diagnostic => diagnostic!["category"]!.GetValue<string>() == "license")
                .Select(static diagnostic => diagnostic!["code"]!.GetValue<string>()),
        ];
        Assert.True(liveLicenseErrors.All(LicenseDiagnostics.Contains),
            "Add the new license errors to this test's list: " + string.Join(", ", liveLicenseErrors.Except(LicenseDiagnostics)));

        string[] expected = [.. licensed.Except(LicenseDiagnostics, StringComparer.Ordinal)];
        Assert.True(expected.SequenceEqual(free, StringComparer.Ordinal),
            "A build without licensing lists every diagnostic except the license ones. Listed but license-only: ["
            + string.Join(", ", free.Intersect(LicenseDiagnostics, StringComparer.Ordinal))
            + "]; left out but not license-only: [" + string.Join(", ", expected.Except(free, StringComparer.Ordinal)) + "].");
    }

    private static string[] DiagnosticCodes(ProductCatalog catalog)
    {
        RootCommand root = RootCommandFactory.Create(new HostContext(catalog), out _);
        InvocationResult capabilities = Invoke(root, "capabilities", "--output", "json");
        Assert.Equal(0, capabilities.ExitCode);
        using JsonDocument document = JsonDocument.Parse(capabilities.StandardOutput);
        return
        [
            .. document.RootElement.GetProperty("diagnostics").EnumerateArray()
                .Select(static diagnostic => diagnostic.GetProperty("code").GetString()!),
        ];
    }

    [Fact]
    public void LicenseAwareCatalog_RegistersLicenseSurface()
    {
        ProductCatalog catalog = Catalog(licensingApplicable: true);
        var host = new HostContext(catalog);
        RootCommand root = RootCommandFactory.Create(host, out _);

        Assert.Contains(
            root.Subcommands,
            static command => command.Name == "license");
        Assert.Contains(
            root.Options,
            static option => option.Name == "--license");
    }

    private static ProductCatalog Catalog(bool licensingApplicable = false)
    {
        ProductDefinition definition = ExtProduct.Define<ITestSession>(
                new ProductManifest
                {
                    Id = "free-test",
                    DisplayName = "Free Test",
                    Engine = new ProductEngineCapabilities
                    {
                        Id = "foss",
                        Sdk = "Test.FOSS",
                        SdkVersion = "1.0.0",
                        LicenseApplicable = licensingApplicable,
                        LicenseRequired = false,
                        SupportsFontDiagnostics = licensingApplicable,
                    },
                    AvailableEngines = ["foss"],
                })
            .Formats(
            [
                FormatDescriptor.Input(
                    "free-test",
                    0,
                    RouteOwnership.Explicit,
                    ".free"),
            ])
            .Diagnostics([])
            .Json(new ProductJsonDefinition("free-test", SdkJsonContext.Default))
            .View(new TestProductViewAdapter<ITestSession>())
            .WithCommand<ITestSession, TestResult>()
            .Activator(static _ =>
                ProductBinding.CreateLicenseFree<ITestSession>(
                    "free-test",
                    static gate => new TestSession(gate)))
            .Build();
        return ProductCatalog.Build([new StaticModule(definition)]);
    }

    private static InvocationResult Invoke(
        RootCommand root,
        params string[] arguments)
    {
        TextWriter originalOutput = Console.Out;
        TextWriter originalError = Console.Error;
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            int exitCode = root.Parse(arguments).Invoke();
            return new InvocationResult(
                exitCode,
                output.ToString(),
                error.ToString());
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
    }

    private interface ITestSession
    {
        ILicenseGate LicenseGate { get; }
    }

    private sealed class TestSession(ILicenseGate licenseGate) : ITestSession
    {
        public ILicenseGate LicenseGate { get; } = licenseGate;
    }

#pragma warning disable APCLI003 // A test result, not a product JSON root.
    private sealed record TestResult() : TestResultEnvelope("result");
#pragma warning restore APCLI003

    private sealed class StaticModule(ProductDefinition definition) : IProductModule
    {
        public ProductDefinition Define() => definition;
    }

    private sealed record InvocationResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}

[CollectionDefinition("Console capture", DisableParallelization = true)]
public sealed class ConsoleCaptureCollection;
