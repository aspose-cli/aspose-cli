using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.Sdk.Serialization;
using System.CommandLine;
using System.Diagnostics;
using Xunit;
using ExtProduct = Aspose.Cli.Sdk.Extensibility.Product;

namespace Aspose.Cli.Architecture.Tests;

public sealed class ProductFileRouterTests
{
    [Fact]
    public async Task UnknownExtension_RequiresOneUniqueStrongMatch()
    {
        ProductCatalog catalog = ProductCatalog.Build(
        [
            Module("one", ".one", Match(FileRecognitionKind.NoMatch)),
            Module("two", ".two", Match(FileRecognitionKind.Match)),
        ]);
        string path = CreateFile(".unknown");
        try
        {
            ProductDefinition result =
                await new ProductFileRouter(catalog).RouteAsync(path);

            Assert.Equal("two", result.Manifest.Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task KnownExtension_RejectsConflictingStrongContent()
    {
        ProductCatalog catalog = ProductCatalog.Build(
        [
            Module("owner", ".owned", Match(FileRecognitionKind.NoMatch)),
            Module("detector", ".detected", Match(FileRecognitionKind.Match)),
        ]);
        string path = CreateFile(".owned");
        try
        {
            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(catalog).RouteAsync(path));

            Assert.Equal(ErrorCodes.FormatMismatch, error.Code);
            Assert.Equal(ExitCode.FormatError, error.ExitCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task KnownExtension_NoMatchFailsClosedWithoutFallback()
    {
        ProductCatalog catalog = ProductCatalog.Build(
        [
            Module("owner", ".owned", Match(FileRecognitionKind.NoMatch)),
        ]);
        string path = CreateFile(".owned");
        try
        {
            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(catalog).RouteAsync(path));

            Assert.Equal(ErrorCodes.FormatMismatch, error.Code);
            Assert.Contains("explicitly", error.Hint, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task MultipleStrongMatches_AreAmbiguous()
    {
        ProductCatalog catalog = ProductCatalog.Build(
        [
            Module("one", ".one", Match(FileRecognitionKind.Match)),
            Module("two", ".two", Match(FileRecognitionKind.Match)),
        ]);
        string path = CreateFile(".unknown");
        try
        {
            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(catalog).RouteAsync(path));

            Assert.Equal(ErrorCodes.FormatAmbiguous, error.Code);
            Assert.Equal(ExitCode.FormatError, error.ExitCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(FileRecognitionKind.Encrypted)]
    [InlineData(FileRecognitionKind.Indeterminate)]
    public async Task UncertainProbe_FailsClosedForKnownExtensionOwner(
        FileRecognitionKind kind)
    {
        ProductCatalog catalog = ProductCatalog.Build(
        [
            Module("owner", ".owned", Match(kind)),
            Module("other", ".other", Match(FileRecognitionKind.Match)),
        ]);
        string path = CreateFile(".owned");
        try
        {
            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(catalog).RouteAsync(path));

            Assert.Equal(ErrorCodes.FormatMismatch, error.Code);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task PrefixAndTimeout_AreBounded()
    {
        int observedPrefix = -1;
        var bounded = new StaticRecognizer((file, _) =>
        {
            observedPrefix = file.Prefix.Length;
            return ValueTask.FromResult(new FileRecognition
            {
                Kind = FileRecognitionKind.Match,
            });
        });
        ProductCatalog prefixRegistry =
            ProductCatalog.Build([Module("bounded", ".bounded", bounded)]);
        string unknown = CreateFile(".unknown", 128);
        try
        {
            _ = await new ProductFileRouter(
                prefixRegistry,
                new FileProbeOptions
                {
                    MaxPrefixBytes = 8,
                    RecognizerTimeout = TimeSpan.FromSeconds(1),
                }).RouteAsync(unknown);
            Assert.Equal(8, observedPrefix);
        }
        finally
        {
            File.Delete(unknown);
        }

        var slow = new StaticRecognizer(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            return new FileRecognition { Kind = FileRecognitionKind.Match };
        });
        ProductCatalog timeoutRegistry =
            ProductCatalog.Build([Module("owner", ".owned", slow)]);
        string owned = CreateFile(".owned");
        try
        {
            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(
                    timeoutRegistry,
                    new FileProbeOptions
                    {
                        RecognizerTimeout = TimeSpan.FromMilliseconds(10),
                    }).RouteAsync(owned));
            Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
        }
        finally
        {
            File.Delete(owned);
        }
    }

    [Fact]
    public async Task CallerCancellation_IsNotConvertedToAnUncertainProbe()
    {
        var blocking = new StaticRecognizer(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new FileRecognition { Kind = FileRecognitionKind.Match };
        });
        ProductCatalog catalog =
            ProductCatalog.Build([Module("owner", ".owned", blocking)]);
        string path = CreateFile(".owned");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await new ProductFileRouter(catalog)
                    .RouteAsync(path, cancellation.Token));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AllRecognizersShareOneAbsoluteBudget()
    {
        var slow = new StaticRecognizer(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new FileRecognition { Kind = FileRecognitionKind.Match };
        });
        ProductCatalog catalog = ProductCatalog.Build(
        [
            Module("one", ".one", slow),
            Module("two", ".two", slow),
            Module("three", ".three", slow),
            Module("four", ".four", slow),
        ]);
        string path = CreateFile(".unknown");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(
                    catalog,
                    new FileProbeOptions
                    {
                        RecognizerTimeout = TimeSpan.FromMilliseconds(50),
                        MaxConcurrency = 2,
                    }).RouteAsync(path));

            Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(1),
                $"Routing took {stopwatch.Elapsed} instead of one shared budget.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OperationSpecificRouteIsEnforcedByResolverAndRegistry()
    {
        var recognizer = Match(FileRecognitionKind.Match);
        ProductDefinitionBuilder<ITestPort> builder = ExtProduct.Define<ITestPort>(
                new ProductManifest
                {
                    Id = "preview-only",
                    DisplayName = "preview-only",
                    Operations = [],
                    Engine = new ProductEngineCapabilities
                    {
                        Id = "aspose",
                        Sdk = "Aspose.Test",
                        SdkVersion = "1.0.0",
                        LicenseApplicable = true,
                        LicenseRequired = false,
                        SupportsFontDiagnostics = true,
                    },
                    AvailableEngines = ["aspose"],
                })
            .Formats(
            [
                new FormatDescriptor(
                    "sample",
                    FormatUse.Input,
                    ".sample")
                {
                    Ownership = RouteOwnership.Default,
                    InputOrder = 0,
                    Operations = ["preview"],
                    Recognizer = recognizer,
                },
            ]);
        Complete(builder, "preview-only");
        ProductDefinition definition = builder.Build();
        ProductCatalog catalog =
            ProductCatalog.Build([new StaticModule(definition)]);
        Assert.True(catalog.TryGetDefaultOwner(
            ".sample",
            "preview",
            out _));
        Assert.False(catalog.TryGetDefaultOwner(
            ".sample",
            "app",
            out _));

        string path = CreateFile(".sample");
        try
        {
            FileRouteResult resolved = await new ProductFileRouter(catalog)
                .ResolveAsync(new FileRouteRequest(path)
                {
                    Operation = "preview",
                });
            Assert.Equal("preview-only", resolved.Product.Manifest.Id);

            CliException rejected = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(catalog)
                    .ResolveAsync(new FileRouteRequest(path)
                    {
                        Operation = "app",
                    }));
            Assert.Equal(ErrorCodes.FormatMismatch, rejected.Code);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GenericFormatRejectsUnisolatedNonCooperativeRecognizer()
    {
        ProductDefinitionBuilder<ITestPort> builder = ExtProduct.Define<ITestPort>(
                new ProductManifest
                {
                    Id = "unsafe",
                    DisplayName = "unsafe",
                    Operations = [],
                    Engine = new ProductEngineCapabilities
                    {
                        Id = "aspose",
                        Sdk = "Aspose.Test",
                        SdkVersion = "1.0.0",
                        LicenseApplicable = true,
                        LicenseRequired = false,
                        SupportsFontDiagnostics = true,
                    },
                    AvailableEngines = ["aspose"],
                })
            .Formats(
            [
                new FormatDescriptor("unsafe", FormatUse.Input, ".unsafe")
                {
                    Ownership = RouteOwnership.Default,
                    Recognizer = new UnclassifiedRecognizer(),
                },
            ]);
        Complete(builder, "unsafe");
        ProductDefinition definition = builder.Build();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => ProductCatalog.Build([new StaticModule(definition)]));
        Assert.Contains(
            "terminable worker boundary",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultOwnedFormatRequiresExactlyOneRecognitionMechanism()
    {
        ProductDefinitionBuilder<ITestPort> missing = ExtProduct.Define<ITestPort>(
                Manifest("missing"))
            .Formats(
            [
                new FormatDescriptor("sample", FormatUse.Input, ".sample")
                {
                    Ownership = RouteOwnership.Default,
                },
            ]);
        Complete(missing, "missing");
        InvalidOperationException missingError =
            Assert.Throws<InvalidOperationException>(() => missing.Build());
        Assert.Contains("recognition", missingError.Message, StringComparison.OrdinalIgnoreCase);

        ProductDefinitionBuilder<ITestPort> mixed = ExtProduct.Define<ITestPort>(
                Manifest("mixed"))
            .Formats(
            [
                new FormatDescriptor("sample", FormatUse.Input, ".sample")
                {
                    Ownership = RouteOwnership.Default,
                    Recognition = FileFormatRecognition.Match(
                        FileProbePattern.AsciiBytesAt(0, "sample"),
                        "sample signature"),
                    Recognizer = Match(FileRecognitionKind.Match),
                },
            ]);
        Complete(mixed, "mixed");
        InvalidOperationException mixedError =
            Assert.Throws<InvalidOperationException>(() => mixed.Build());
        Assert.Contains("cannot mix", mixedError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtensionlessDeclarativeTieIsIndeterminate()
    {
        FileProbePattern signature = FileProbePattern.AsciiBytesAt(0, "same");
        IReadOnlyList<FormatDescriptor> formats = FileFormatRecognition.AttachTo(
        [
            new FormatDescriptor("alpha", FormatUse.Input, ".alpha")
            {
                Ownership = RouteOwnership.Default,
            },
            new FormatDescriptor("beta", FormatUse.Input, ".beta")
            {
                Ownership = RouteOwnership.Default,
            },
        ],
        new Dictionary<string, FileFormatRecognition>(StringComparer.Ordinal)
        {
            ["alpha"] = FileFormatRecognition.Match(signature, "same signature"),
            ["beta"] = FileFormatRecognition.Match(signature, "same signature"),
        });
        ProductDefinitionBuilder<ITestPort> builder = ExtProduct.Define<ITestPort>(
                Manifest("ambiguous-format"))
            .Formats(formats);
        Complete(builder, "ambiguous-format");
        ProductDefinition definition = builder.Build();
        ProductCatalog catalog =
            ProductCatalog.Build([new StaticModule(definition)]);
        string path = CreateFile(".unknown", "same"u8.ToArray());
        try
        {
            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(catalog).RouteAsync(path));
            Assert.Equal(ErrorCodes.FormatMismatch, error.Code);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TruncatedZipWithoutDeclaredMarkerRemainsIndeterminateAtForty()
    {
        IReadOnlyList<FormatDescriptor> formats = FileFormatRecognition.AttachTo(
        [
            new FormatDescriptor("package", FormatUse.Input, ".package")
            {
                Ownership = RouteOwnership.Default,
            },
        ],
        new Dictionary<string, FileFormatRecognition>(StringComparer.Ordinal)
        {
            ["package"] = FileFormatRecognition.Match(
                FileProbePattern.ZipContainsAny("required/marker.xml"),
                "package marker"),
        });
        ProductDefinitionBuilder<ITestPort> builder = ExtProduct.Define<ITestPort>(
                Manifest("package"))
            .Formats(formats);
        Complete(builder, "package");
        ProductDefinition definition = builder.Build();
        ProductCatalog catalog =
            ProductCatalog.Build([new StaticModule(definition)]);
        string path = CreateFile(
            ".package",
            [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00]);
        try
        {
            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(catalog).RouteAsync(path));
            Assert.Equal(ErrorCodes.FormatMismatch, error.Code);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DeclarativePatternSnapshotsCallerOwnedArrays()
    {
        byte[] signature = [0x43, 0x4C, 0x49];
        FileFormatRecognition recognition = FileFormatRecognition.Match(
            FileProbePattern.BytesAt(0, signature),
            "CLI signature");
        signature[0] = 0x00;
        IReadOnlyList<FormatDescriptor> formats = FileFormatRecognition.AttachTo(
        [
            new FormatDescriptor("sample", FormatUse.Input, ".sample")
            {
                Ownership = RouteOwnership.Default,
            },
        ],
        new Dictionary<string, FileFormatRecognition>(StringComparer.Ordinal)
        {
            ["sample"] = recognition,
        });
        ProductDefinitionBuilder<ITestPort> builder = ExtProduct.Define<ITestPort>(
                Manifest("snapshot"))
            .Formats(formats);
        Complete(builder, "snapshot");
        ProductDefinition definition = builder.Build();
        string path = CreateFile(".sample", [0x43, 0x4C, 0x49]);
        try
        {
            ProductDefinition routed = await new ProductFileRouter(
                    ProductCatalog.Build([new StaticModule(definition)]))
                .RouteAsync(path);
            Assert.Equal("snapshot", routed.Manifest.Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DeclarativeOffsetsCannotOverflowBoundedPrefixChecks()
    {
        IReadOnlyList<FormatDescriptor> formats = FileFormatRecognition.AttachTo(
        [
            new FormatDescriptor("sample", FormatUse.Input, ".sample")
            {
                Ownership = RouteOwnership.Default,
            },
        ],
        new Dictionary<string, FileFormatRecognition>(StringComparer.Ordinal)
        {
            ["sample"] = FileFormatRecognition.FirstOf(
                FileFormatRecognition.Match(
                    FileProbePattern.BytesAt(int.MaxValue, 0x43),
                    "far byte"),
                FileFormatRecognition.Match(
                    FileProbePattern.UInt32LittleEndianGreaterThan(
                        int.MaxValue,
                        0),
                    "far integer")),
        });
        ProductDefinitionBuilder<ITestPort> builder = ExtProduct.Define<ITestPort>(
                Manifest("bounded-offset"))
            .Formats(formats);
        Complete(builder, "bounded-offset");
        ProductDefinition definition = builder.Build();
        string path = CreateFile(".sample", [0x43]);
        try
        {
            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(
                        ProductCatalog.Build([new StaticModule(definition)]))
                    .RouteAsync(path));
            Assert.Equal(ErrorCodes.FormatMismatch, error.Code);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static IProductModule Module(
        string id,
        string extension,
        IFileRecognizer recognizer)
    {
        ProductDefinitionBuilder<ITestPort> builder = ExtProduct.Define<ITestPort>(
            new ProductManifest
            {
                Id = id,
                DisplayName = id,
                Operations = [],
                Engine = new ProductEngineCapabilities
                {
                    Id = "aspose",
                    Sdk = "Aspose.Test",
                    SdkVersion = "1.0.0",
                    LicenseApplicable = true,
                    LicenseRequired = false,
                    SupportsFontDiagnostics = true,
                },
                AvailableEngines = ["aspose"],
            })
            .Formats(
            [
                FormatDescriptor.Input(
                    id,
                    0,
                    RouteOwnership.Default,
                    extension) with
                {
                    Recognizer = recognizer,
                },
            ]);
        Complete(builder, id);
        return new StaticModule(builder.Build());
    }

    private static void Complete(
        ProductDefinitionBuilder<ITestPort> builder,
        string id)
    {
        builder
            .Diagnostics([])
            .Json(new ProductJsonDefinition(id, SdkJsonContext.Default))
            .Preview(new TestProductPreviewAdapter<ITestPort>())
            .Review(new TestProductReviewAdapter<ITestPort>())
            .Commands(_ => new Command(id))
            .Activator(static _ =>
                throw new InvalidOperationException(
                    "Routing tests must not activate product ports."));
        switch (id)
        {
            case "one":
                builder.Output<TestResultOne>(static (_, _) => { });
                break;
            case "two":
                builder.Output<TestResultTwo>(static (_, _) => { });
                break;
            case "three":
                builder.Output<TestResultThree>(static (_, _) => { });
                break;
            case "four":
                builder.Output<TestResultFour>(static (_, _) => { });
                break;
            case "owner":
                builder.Output<TestResultOwner>(static (_, _) => { });
                break;
            case "detector":
                builder.Output<TestResultDetector>(static (_, _) => { });
                break;
            default:
                builder.Output<TestResultOther>(static (_, _) => { });
                break;
        }
    }

    private static ProductManifest Manifest(string id) => new()
    {
        Id = id,
        DisplayName = id,
        Operations = [],
        Engine = new ProductEngineCapabilities
        {
            Id = "aspose",
            Sdk = "Aspose.Test",
            SdkVersion = "1.0.0",
            LicenseApplicable = true,
            LicenseRequired = false,
            SupportsFontDiagnostics = true,
        },
        AvailableEngines = ["aspose"],
    };

    private static IFileRecognizer Match(FileRecognitionKind kind) =>
        new StaticRecognizer((_, _) => ValueTask.FromResult(
            new FileRecognition { Kind = kind }));

    private static string CreateFile(string extension, int length = 16)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"aspose-router-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, Enumerable.Range(0, length)
            .Select(static value => (byte)value)
            .ToArray());
        return path;
    }

    private static string CreateFile(string extension, byte[] content)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"aspose-router-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, content);
        return path;
    }

    private interface ITestPort;

    private sealed record TestResultOne() : ResultEnvelope("test/one", 1);

    private sealed record TestResultTwo() : ResultEnvelope("test/two", 1);

    private sealed record TestResultThree() : ResultEnvelope("test/three", 1);

    private sealed record TestResultFour() : ResultEnvelope("test/four", 1);

    private sealed record TestResultOwner() : ResultEnvelope("test/owner", 1);

    private sealed record TestResultDetector() : ResultEnvelope("test/detector", 1);

    private sealed record TestResultOther() : ResultEnvelope("test/other", 1);

    private sealed class StaticModule(ProductDefinition definition) : IProductModule
    {
        public ProductDefinition Define() => definition;
    }

    private sealed class StaticRecognizer(
        Func<FileProbeSession, CancellationToken, ValueTask<FileRecognition>> recognize)
        : IFileRecognizer
    {
        public FileRecognizerDescriptor Descriptor { get; } = new()
        {
            Strategy = "test-cooperative",
            Version = "1",
            MaxProbeBytes = 64 * 1024,
            EvidenceTypes = ["test"],
            CooperativeCancellation = true,
        };

        public ValueTask<FileRecognition> RecognizeAsync(
            FileProbeSession file,
            CancellationToken cancellationToken) =>
            recognize(file, cancellationToken);
    }

    private sealed class UnclassifiedRecognizer : IFileRecognizer
    {
        public ValueTask<FileRecognition> RecognizeAsync(
            FileProbeSession file,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new FileRecognition
            {
                Kind = FileRecognitionKind.Indeterminate,
            });
    }
}
