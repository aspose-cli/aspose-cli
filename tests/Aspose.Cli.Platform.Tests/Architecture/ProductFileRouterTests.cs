using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.TestKit;
using System.CommandLine;
using Xunit;
using ExtProduct = Aspose.Cli.Sdk.Extensibility.Product;
using Aspose.Cli.Platform.Tests;

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

            // Content no product recognizes under a product's extension is a damaged file.
            Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
            Assert.Equal(path, error.Details!["path"]!.GetValue<string>());
            Assert.NotNull(error.Explanation);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Detect_NamesEveryProductTheContentMatchesWhateverTheExtension()
    {
        ProductCatalog catalog = ProductCatalog.Build(
        [
            Module("owner", ".owned", Match(FileRecognitionKind.NoMatch)),
            Module("detector", ".detected", new StaticRecognizer((_, _) => ValueTask.FromResult(
                new FileRecognition { Kind = FileRecognitionKind.Match, FormatId = "docx", Confidence = 90 }))),
            // Strong evidence that fits several formats names the product but no format.
            Module("one", ".one", new StaticRecognizer((_, _) => ValueTask.FromResult(
                new FileRecognition { Kind = FileRecognitionKind.Indeterminate, FormatId = "dotm", Confidence = 95 }))),
            Module("two", ".two", Match(FileRecognitionKind.Indeterminate)),
        ]);
        string path = CreateFile(".owned");
        try
        {
            IReadOnlyList<FileDetection> detected = await new ProductFileRouter(catalog).DetectAsync(path);

            Assert.Equal([new FileDetection("one", null), new FileDetection("detector", "docx")], detected);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Detect_ReturnsNothingForAMissingFile() =>
        Assert.Empty(await new ProductFileRouter(ProductCatalog.Build(
            [Module("one", ".one", Match(FileRecognitionKind.Match))]))
            .DetectAsync(Path.Combine(Path.GetTempPath(), $"aspose-router-{Guid.NewGuid():N}.one")));

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
                    // This part pins the prefix bound; the deadline only ends a hang.
                    RecognizerTimeout = TestDeadline.Hang,
                }).RouteAsync(unknown);
            Assert.Equal(8, observedPrefix);
        }
        finally
        {
            File.Delete(unknown);
        }

        var slow = new StaticRecognizer(async (_, cancellationToken) =>
        {
            // It never answers, so only the recognizer timeout ends it, however slow the machine.
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
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
    public async Task RecognizerCompletion_CannotTurnCancellationIntoSuccessfulRouting()
    {
        var late = new StaticRecognizer(async (_, token) =>
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) { }
            return new FileRecognition { Kind = FileRecognitionKind.Match };
        });
        ProductCatalog catalog = ProductCatalog.Build([Module("owner", ".owned", late)]);
        string path = CreateFile(".owned");
        try
        {
            var router = new ProductFileRouter(catalog, new FileProbeOptions
            { RecognizerTimeout = TimeSpan.FromMilliseconds(20) });
            CliException error = await Assert.ThrowsAsync<CliException>(async () => await router.RouteAsync(path));
            Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await router.RouteAsync(path, cancelled.Token));
        }
        finally { File.Delete(path); }
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
        var started = new System.Collections.Concurrent.ConcurrentBag<CancellationToken>();
        var slow = new StaticRecognizer(async (_, cancellationToken) =>
        {
            started.Add(cancellationToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
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
        try
        {
            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(
                    catalog,
                    new FileProbeOptions
                    {
                        RecognizerTimeout = TimeSpan.FromMilliseconds(50),
                        MaxConcurrency = 2,
                    }).RouteAsync(path)).WaitAsync(TimeSpan.FromSeconds(30));

            // Recognizers that never finish end in one bounded timeout, every one of them cancelled;
            // without the budget the routing would never return and the watchdog would fail it.
            Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
            Assert.NotEmpty(started);
            Assert.All(started, static token => Assert.True(token.IsCancellationRequested));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DefaultBudget_RoutesRecognizersSlowedByALoadedMachine()
    {
        // A cold engine load or a busy machine can hold a recognizer well past a second.
        var slow = new StaticRecognizer(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken);
            return new FileRecognition { Kind = FileRecognitionKind.Match };
        });
        ProductCatalog catalog = ProductCatalog.Build([Module("owner", ".owned", slow)]);
        string path = CreateFile(".owned");
        try
        {
            ProductDefinition result = await new ProductFileRouter(catalog).RouteAsync(path);

            Assert.Equal("owner", result.Manifest.Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RoutingTimeout_StatesItsOwnBudgetAndDoesNotPointAtTimeoutOption()
    {
        var hanging = new StaticRecognizer(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new FileRecognition { Kind = FileRecognitionKind.Match };
        });
        ProductCatalog catalog = ProductCatalog.Build([Module("owner", ".owned", hanging)]);
        string path = CreateFile(".owned");
        try
        {
            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(
                    catalog,
                    new FileProbeOptions { RecognizerTimeout = TimeSpan.FromSeconds(2) }).RouteAsync(path));

            Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
            Assert.Equal(2, (int)error.Details!["timeoutSeconds"]!);
            Assert.Equal("file-routing", (string)error.Details!["phase"]!);
            Assert.Contains("2s", error.Message, StringComparison.Ordinal);
            Assert.Contains("routing", error.Message, StringComparison.Ordinal);
            // --timeout does not govern the routing budget, so the hint must not tell the caller to raise it.
            Assert.DoesNotContain("Raise --timeout", error.Hint ?? "", StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GenericFormatRejectsUnisolatedNonCooperativeRecognizer()
    {
        ProductDefinitionBuilder<ITestSession> builder = ExtProduct.Define<ITestSession>(
                new ProductManifest
                {
                    Id = "unsafe",
                    DisplayName = "unsafe",
                    Engine = new ProductEngineCapabilities
                    {
                        Id = "aspose",
                        Sdk = "Aspose.Test",
                        SdkVersion = "1.0.0",
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
        ProductDefinitionBuilder<ITestSession> missing = ExtProduct.Define<ITestSession>(
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

        ProductDefinitionBuilder<ITestSession> mixed = ExtProduct.Define<ITestSession>(
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
        IReadOnlyList<FormatDescriptor> formats =
        [
            new FormatDescriptor("alpha", FormatUse.Input, ".alpha")
            {
                Ownership = RouteOwnership.Default,
                Recognition = FileFormatRecognition.Match(signature, "same signature"),
            },
            new FormatDescriptor("beta", FormatUse.Input, ".beta")
            {
                Ownership = RouteOwnership.Default,
                Recognition = FileFormatRecognition.Match(signature, "same signature"),
            },
        ];
        ProductDefinitionBuilder<ITestSession> builder = ExtProduct.Define<ITestSession>(
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
    public async Task OwnerMismatch_NamesAProductWhoseSignatureFitsSeveralOfItsFormats()
    {
        ProductCatalog catalog = ProductCatalog.Build(
        [
            Module("owner", ".owned", Match(FileRecognitionKind.NoMatch)),
            new StaticModule(TieProduct()),
        ]);
        string path = CreateFile(".owned", "same"u8.ToArray());
        try
        {
            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(catalog).RouteAsync(path));

            Assert.Equal(ErrorCodes.FormatMismatch, error.Code);
            Assert.Equal("ambiguous-format", Assert.Single(error.Details!["detected"]!.AsArray())!.GetValue<string>());
            Assert.Contains("--product ambiguous-format", error.Hint, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExplicitProduct_ResolvesIndeterminateContentButNotAMismatch()
    {
        ProductCatalog catalog = ProductCatalog.Build(
        [
            Module("one", ".one", Match(FileRecognitionKind.Indeterminate)),
            Module("two", ".two", Match(FileRecognitionKind.NoMatch)),
        ]);
        string indeterminate = CreateFile(".one");
        string path = CreateFile(".two");
        try
        {
            FileRouteResult result = await new ProductFileRouter(catalog).ResolveAsync(
                new FileRouteRequest(indeterminate) { ExplicitProductId = "one" });
            Assert.Equal("one", result.Product.Manifest.Id);
            Assert.True(result.Explicit);

            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(catalog).ResolveAsync(
                    new FileRouteRequest(path) { ExplicitProductId = "two" }));
            Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
        }
        finally
        {
            File.Delete(indeterminate);
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExplicitProduct_LeavesAFormatWithoutRecognitionRulesToTheEngine()
    {
        ProductCatalog catalog = ProductCatalog.Build([new StaticModule(TieProduct())]);
        string other = CreateFile(".gamma", "same"u8.ToArray());
        string declared = CreateFile(".alpha", "different"u8.ToArray());
        try
        {
            FileRouteResult result = await new ProductFileRouter(catalog).ResolveAsync(
                new FileRouteRequest(other) { ExplicitProductId = "ambiguous-format" });
            Assert.Equal("ambiguous-format", result.Product.Manifest.Id);
            Assert.True(result.Explicit);

            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(catalog).ResolveAsync(
                    new FileRouteRequest(declared) { ExplicitProductId = "ambiguous-format" }));
            Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
        }
        finally
        {
            File.Delete(other);
            File.Delete(declared);
        }
    }

    [Fact]
    public async Task ExplicitProduct_StillChecksTheRulesOfAFormatItNeverRoutesGenerically()
    {
        ProductDefinitionBuilder<ITestSession> builder = ExtProduct.Define<ITestSession>(Manifest("explicit-rules"))
            .Formats(
            [
                new FormatDescriptor("alpha", FormatUse.Input, ".alpha")
                {
                    Ownership = RouteOwnership.Default,
                    Recognition = FileFormatRecognition.Match(FileProbePattern.AsciiBytesAt(0, "alpha"), "alpha signature"),
                },
                FormatDescriptor.Input("delta", 1, RouteOwnership.Explicit, ".delta") with
                {
                    Recognition = FileFormatRecognition.Match(
                        FileProbePattern.AsciiBytesAt(0, "delta"), "delta signature"),
                },
            ]);
        Complete(builder, "explicit-rules");
        ProductCatalog catalog = ProductCatalog.Build([new StaticModule(builder.Build())]);
        string matching = CreateFile(".delta", "delta"u8.ToArray());
        string contradicting = CreateFile(".delta", "other"u8.ToArray());
        try
        {
            FileRouteResult result = await new ProductFileRouter(catalog).ResolveAsync(
                new FileRouteRequest(matching) { ExplicitProductId = "explicit-rules" });
            Assert.Equal("delta", result.FormatId);

            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(catalog).ResolveAsync(
                    new FileRouteRequest(contradicting) { ExplicitProductId = "explicit-rules" }));
            // The product declares the extension, so content it does not recognize is its damaged file.
            Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
        }
        finally
        {
            File.Delete(matching);
            File.Delete(contradicting);
        }
    }

    [Fact]
    public void SharedExtension_MustBeOneTheFormatDeclares()
    {
        ProductDefinitionBuilder<ITestSession> builder = ExtProduct.Define<ITestSession>(Manifest("shared"))
            .Formats(
            [
                new FormatDescriptor("rows", FormatUse.Input, ".rows")
                {
                    SharedExtensions = [".xml"],
                },
            ]);
        Complete(builder, "shared");

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            ProductCatalog.Build([new StaticModule(builder.Build())]));

        Assert.Contains("marks an extension shared that it does not declare", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A format may read and write an extension another product owns, as delimited text may be
    /// named <c>data.txt</c>: the unrouted extension claims no generic route and its content is
    /// no evidence against the owner, but the product's explicit selection still reads it.
    /// </summary>
    [Fact]
    public async Task UnroutedExtension_LeavesTheRouteToItsOwnerAndStillReadsExplicitly()
    {
        ProductDefinitionBuilder<ITestSession> rows = ExtProduct.Define<ITestSession>(Manifest("two")).Formats(
        [
            new FormatDescriptor("rows", FormatUse.Input, ".rows", ".txt")
            {
                Ownership = RouteOwnership.Default,
                UnroutedExtensions = [".txt"],
                Recognition = FileFormatRecognition.Match(FileProbePattern.TextContains(","), "delimited text", 70),
            },
        ]);
        Complete(rows, "two");
        ProductCatalog catalog = ProductCatalog.Build(
        [
            Module("one", ".txt", new StaticRecognizer((_, _) => ValueTask.FromResult(
                new FileRecognition { Kind = FileRecognitionKind.Match, FormatId = "one", Confidence = 70 }))),
            new StaticModule(rows.Build()),
        ]);
        string path = CreateFile(".txt", "a,b"u8.ToArray());
        try
        {
            Assert.True(catalog.TryGetDefaultOwner(".txt", out ProductDefinition? owner));
            Assert.Equal("one", owner!.Manifest.Id);
            Assert.Equal("two", Assert.Single(catalog.GetRoutingCapabilities().Routes, static route => route.Extension == ".rows").Product);

            ProductDefinition routed = await new ProductFileRouter(catalog).RouteAsync(path);
            Assert.Equal("one", routed.Manifest.Id);

            FileRouteResult selected = await new ProductFileRouter(catalog).ResolveAsync(
                new FileRouteRequest(path) { ExplicitProductId = "two" });
            Assert.Equal("rows", selected.FormatId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// A product whose two default formats share one signature, plus an input format without
    /// recognition rules that only an explicit selection reaches.
    /// </summary>
    private static ProductDefinition TieProduct()
    {
        FileProbePattern signature = FileProbePattern.AsciiBytesAt(0, "same");
        FileFormatRecognition recognition = FileFormatRecognition.Match(signature, "same signature");
        ProductDefinitionBuilder<ITestSession> builder = ExtProduct.Define<ITestSession>(
                Manifest("ambiguous-format"))
            .Formats(
            [
                new FormatDescriptor("alpha", FormatUse.Input, ".alpha") { Ownership = RouteOwnership.Default, Recognition = recognition },
                new FormatDescriptor("beta", FormatUse.Input, ".beta") { Ownership = RouteOwnership.Default, Recognition = recognition },
                FormatDescriptor.Input("gamma", 2, RouteOwnership.Explicit, ".gamma"),
            ]);
        Complete(builder, "ambiguous-format");
        return builder.Build();
    }

    [Fact]
    public async Task TruncatedZipWithoutDeclaredMarkerRemainsIndeterminateAtForty()
    {
        ProductDefinitionBuilder<ITestSession> builder = ExtProduct.Define<ITestSession>(
                Manifest("package"))
            .Formats(
            [
                new FormatDescriptor("package", FormatUse.Input, ".package")
                {
                    Ownership = RouteOwnership.Default,
                    Recognition = FileFormatRecognition.Match(
                        FileProbePattern.ZipContainsAny("required/marker.xml"),
                        "package marker"),
                },
            ]);
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
            // Routing fails closed; no product recognizes the truncated package, so it is damaged.
            Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
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
        ProductDefinitionBuilder<ITestSession> builder = ExtProduct.Define<ITestSession>(
                Manifest("snapshot"))
            .Formats(
            [
                new FormatDescriptor("sample", FormatUse.Input, ".sample")
                {
                    Ownership = RouteOwnership.Default,
                    Recognition = recognition,
                },
            ]);
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
        ProductDefinitionBuilder<ITestSession> builder = ExtProduct.Define<ITestSession>(
                Manifest("bounded-offset"))
            .Formats(
            [
                new FormatDescriptor("sample", FormatUse.Input, ".sample")
                {
                    Ownership = RouteOwnership.Default,
                    Recognition = FileFormatRecognition.FirstOf(
                        FileFormatRecognition.Match(
                            FileProbePattern.BytesAt(int.MaxValue, 0x43),
                            "far byte")),
                },
            ]);
        Complete(builder, "bounded-offset");
        ProductDefinition definition = builder.Build();
        string path = CreateFile(".sample", [0x43]);
        try
        {
            CliException error = await Assert.ThrowsAsync<CliException>(
                async () => await new ProductFileRouter(
                        ProductCatalog.Build([new StaticModule(definition)]))
                    .RouteAsync(path));
            // The out-of-range offset never matches, so routing fails closed on unrecognized content.
            Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
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
        ProductDefinitionBuilder<ITestSession> builder = ExtProduct.Define<ITestSession>(
            new ProductManifest
            {
                Id = id,
                DisplayName = id,
                Engine = new ProductEngineCapabilities
                {
                    Id = "aspose",
                    Sdk = "Aspose.Test",
                    SdkVersion = "1.0.0",
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
        ProductDefinitionBuilder<ITestSession> builder,
        string id)
    {
        builder
            .Diagnostics([])
            .Json(new ProductJsonDefinition(id, SdkJsonContext.Default))
            .View(new TestProductViewAdapter<ITestSession>())
            .Activator(static _ =>
                throw new InvalidOperationException(
                    "Routing tests must not activate a product session."));
        switch (id)
        {
            case "one":
                builder.WithCommand<ITestSession, TestResultOne>();
                break;
            case "two":
                builder.WithCommand<ITestSession, TestResultTwo>();
                break;
            case "three":
                builder.WithCommand<ITestSession, TestResultThree>();
                break;
            case "four":
                builder.WithCommand<ITestSession, TestResultFour>();
                break;
            case "owner":
                builder.WithCommand<ITestSession, TestResultOwner>();
                break;
            case "detector":
                builder.WithCommand<ITestSession, TestResultDetector>();
                break;
            default:
                builder.WithCommand<ITestSession, TestResultOther>();
                break;
        }
    }

    private static ProductManifest Manifest(string id) => new()
    {
        Id = id,
        DisplayName = id,
        Engine = new ProductEngineCapabilities
        {
            Id = "aspose",
            Sdk = "Aspose.Test",
            SdkVersion = "1.0.0",
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

    private interface ITestSession;

#pragma warning disable APCLI003 // Test results, not product JSON roots.
    private sealed record TestResultOne() : TestResultEnvelope("one");

    private sealed record TestResultTwo() : TestResultEnvelope("two");

    private sealed record TestResultThree() : TestResultEnvelope("three");

    private sealed record TestResultFour() : TestResultEnvelope("four");

    private sealed record TestResultOwner() : TestResultEnvelope("owner");

    private sealed record TestResultDetector() : TestResultEnvelope("detector");

    private sealed record TestResultOther() : TestResultEnvelope("other");
#pragma warning restore APCLI003

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
