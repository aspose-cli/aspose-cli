using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>Cells composition, routing, schema, and package invariants.</summary>
public sealed class CellsModuleTests
    : ProductContractTests<CellsModule>
{
    protected override IReadOnlyList<ResultEnvelope> CanonicalResults =>
        CellsContractSamples.Results;

    protected override IReadOnlyList<ProductSchemaSample> CanonicalInputs =>
        CellsContractSamples.Inputs;

    [Fact]
    public void OperationBatch_RejectsNullOperations()
    {
        var error = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            OpsParser.Parse("""{"ops":null}"""));
        Assert.Equal(Aspose.Cli.Sdk.Errors.ErrorCodes.OpsInvalid, error.Code);
    }

    [Fact]
    public void OperationObjects_RejectUnknownNestedFields() =>
        AssertOperationObjectIsStrict<Op>(
            """{"op":"format_range","range":"A1","style":{"bold":true}}""", "style");

    [Fact]
    public Task EveryDefaultInputFormatHasPositiveRoutingEvidence() =>
        ProductRoutingContract.AssertPositiveRoutesAsync<CellsModule>(
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["xlsx"] = ProductRoutingContract.ZipMarker("xl/workbook.xml"),
                ["xlsm"] = ProductRoutingContract.ZipMarker("xl/workbook.xml"),
                ["xlsb"] = ProductRoutingContract.ZipMarker("xl/workbook.bin"),
                ["xls"] = ProductRoutingContract.CompoundFile(),
                ["ods"] = ProductRoutingContract.ZipMarker(
                    "application/vnd.oasis.opendocument.spreadsheet"),
                ["csv"] = ProductRoutingContract.Utf8("Name,Value\nA,1\n"),
                ["tsv"] = ProductRoutingContract.Utf8("Name\tValue\nA\t1\n"),
            });

    [Fact]
    public void CanonicalOps_CoverEveryRegisteredOperation() =>
        Assert.Equal(
            OpNames.All.Order(StringComparer.Ordinal),
            CellsContractSamples.Ops.Ops
                .Select(static operation => operation.OpName)
                .Order(StringComparer.Ordinal));

    [Fact]
    public void ManagedSkiaSharp_MatchesTheLinuxNativePin()
    {
        Version managed = ThreePartVersion(SkiaSharpInformationalVersion());
        Version pinned = ThreePartVersion(LinuxNativePin());

        Assert.True(
            managed == pinned,
            $"Managed SkiaSharp is {managed} but SkiaSharp.NativeAssets.Linux is pinned at {pinned} in " +
            "the product catalog. Bump the Cells supplemental package version in " +
            "eng/products.json, or the Linux render native goes out of step.");
    }

    private static string SkiaSharpInformationalVersion()
    {
        _ = typeof(Aspose.Cells.Workbook);
        Assembly skiaSharp = Assembly.Load("SkiaSharp");
        return skiaSharp.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? skiaSharp.GetName().Version?.ToString()
            ?? throw new InvalidOperationException("SkiaSharp exposes no version.");
    }

    private static string LinuxNativePin([CallerFilePath] string thisFile = "")
    {
        using JsonDocument catalog = JsonDocument.Parse(
            File.ReadAllText(ProductsPath(thisFile)));
        JsonElement cells = catalog.RootElement
            .GetProperty("products")
            .EnumerateArray()
            .Single(static product =>
                product.GetProperty("id").GetString() == "cells");
        if (cells.GetProperty("supplementalPackages").TryGetProperty(
            "SkiaSharp.NativeAssets.Linux",
            out JsonElement version))
        {
            return version.GetString()!;
        }
        throw new InvalidOperationException(
            "SkiaSharp.NativeAssets.Linux pin not found in eng/products.json.");
    }

    private static string ProductsPath(string thisFile)
    {
        for (DirectoryInfo? directory = new FileInfo(thisFile).Directory;
             directory is not null;
             directory = directory.Parent)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "eng",
                "products.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            "Could not locate eng/products.json by walking up from the test source.");
    }

    private static Version ThreePartVersion(string raw)
    {
        Match match = Regex.Match(raw, @"^(\d+)\.(\d+)\.(\d+)");
        if (!match.Success)
        {
            throw new FormatException($"Unrecognized version string '{raw}'.");
        }

        return new Version(
            int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[2].Value),
            int.Parse(match.Groups[3].Value));
    }
}
