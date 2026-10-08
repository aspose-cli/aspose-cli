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
                CommonSchemaIds.SchemaList,
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
        ProductBinding<ITestPort> binding =
            ProductBinding.CreateLicenseFree<ITestPort>(
                "free-test",
                static gate => new TestPort(gate));

        Assert.False(binding.LicenseGate.IsApplicable);
        Assert.Equal(LicenseResolution.None, binding.LicenseGate.Resolution);
        Assert.Equal(
            LicenseState.NotApplicable,
            binding.LicenseGate.EnsureApplied());
        Assert.Equal(
            LicenseModes.NotApplicable,
            EnvelopeParts.License(
                binding.LicenseGate.EnsureApplied()).Mode);
        Assert.Null(EnvelopeParts.OutputWarnings(
            binding.LicenseGate.EnsureApplied()));
        Assert.False(binding.HasFontEnvironment);
        Assert.Null(binding.FontEnvironment);
        Assert.Same(binding.LicenseGate, binding.Port.LicenseGate);
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
        ProductDefinition definition = ExtProduct.Define<ITestPort>(
                new ProductManifest
                {
                    Id = "free-test",
                    DisplayName = "Free Test",
                    Operations = [],
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
            .View(new TestProductViewAdapter<ITestPort>())
            .Output<TestResult>(static (_, _) => { })
            .Commands(_ => new Command("free-test"))
            .Activator(static _ =>
                ProductBinding.CreateLicenseFree<ITestPort>(
                    "free-test",
                    static gate => new TestPort(gate)))
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

    private interface ITestPort
    {
        ILicenseGate LicenseGate { get; }
    }

    private sealed class TestPort(ILicenseGate licenseGate) : ITestPort
    {
        public ILicenseGate LicenseGate { get; } = licenseGate;
    }

#pragma warning disable APCLI003 // A test result, not a product JSON root.
    private sealed record TestResult() : ResultEnvelope("test/result", 1);
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
