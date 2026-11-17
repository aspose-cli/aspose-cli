using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Operations;


namespace Aspose.Cli.Architecture.Tests.TestSupport;

internal sealed record CommonSchemaSample(string Schema, object Value);

/// <summary>Canonical samples for SDK and Host-owned result families.</summary>
internal static class CommonSchemaSamples
{
    public static LicenseInfo Evaluation { get; } = new() { Mode = LicenseModes.Evaluation };

    public static ErrorEnvelope Error { get; } = new()
    {
        Error = new ErrorPayload
        {
            Code = "ITEM_NOT_FOUND",
            Message = "Item 'missing' was not found.",
            Details = new JsonObject
            {
                ["requested"] = "missing",
                ["available"] = new JsonArray("first", "second"),
            },
            Hint = "Use one of the available item names.",
        },
    };

    public static CapabilitiesResult Capabilities { get; } = new()
    {
        CliVersion = "development",
        SourceRevision = "0123456789abcdef0123456789abcdef01234567",
        BuildDirty = false,
        EnginePins =
        [
            new EnginePinCapabilities
            {
                Product = "test",
                Engine = "test",
                Version = "1.0.0",
            },
        ],
        Diagnostics = [],
        ResourceBudgets =
        [
            ResourceBudgetCapabilities.Domain(
                "input-bytes",
                1L << 30,
                4L << 30,
                "bytes",
                "pre-runtime-and-stream",
                "--max-input-bytes"),
        ],
        Products =
        [
            new ProductCapabilities
            {
                Id = "test",
                Verbs = ["edit"],
                Operations =
                [
                    new ProductOperationDescriptor
                    {
                        Command = "edit",
                        InputSchema = "v2/test/ops",
                        OperationSchema = "aspose-cli schema v2/test/ops --operation <op>",
                        MaximumOperationCount = 16,
                        Ops = ["replace_text"],
                        Fingerprint = new GeneratedOperationSchema(static () => "{}", ["replace_text"]).LazyFingerprint,
                    },
                ],
                Engine = new ProductEngineCapabilities
                {
                    Id = "test",
                    Sdk = "Aspose.Test",
                    SdkVersion = "1.0.0",
                },
            },
        ],
        Schemas = ["v2/test/ops"],
        Routing = new RoutingCapabilities
        {
            TotalProbeMilliseconds = 500,
            TotalProbeBytes = 65_536,
            MaxConcurrency = 4,
            IndeterminatePolicy = "fail-closed-explicit-product-required",
            Routes = [],
        },
        Commands = [],
    };

    public static CapabilitiesSummaryResult CapabilitiesSummary { get; } = new()
    {
        CliVersion = "1.0.0",
        Products =
        [
            new ProductCapabilitiesSummary
            {
                Id = "test",
                Name = "Test",
                Description = "Test document automation.",
                Engine = "test",
                EngineVersion = "1.0.0",
                LoadFormats = ["bin"],
                ConvertFormats = ["bin"],
                RenderFormats = [],
                Commands =
                [
                    new CommandCapabilitiesSummary { Command = "edit", Description = "Apply a batch of ops." },
                    new CommandCapabilitiesSummary { Command = "query range" },
                ],
                Operations =
                [
                    new OperationCapabilitiesSummary { Command = "edit", Ops = ["set_value"] },
                ],
            },
        ],
    };

    public static VersionResult Version { get; } = new()
    {
        CliVersion = "1.0.0",
        ArtifactVersion = "1.0.0+0123456789abcdef0123456789abcdef01234567",
        SourceRevision = "0123456789abcdef0123456789abcdef01234567",
        BuildDirty = false,
        EnginePins = Capabilities.EnginePins,
    };

    public static LicenseStatusResult LicenseStatus { get; } = new()
    {
        Products = [new ProductLicenseStatus
        {
            Product = "cells", Name = "Cells", Mode = LicenseModes.Evaluation,
        }],
    };

    public static DoctorResult Doctor { get; } = new()
    {
        Ok = true,
        Checks =
        [
            new DoctorCheck { Name = "cli", Status = "ok", Detail = "development build" },
            new DoctorCheck { Name = "runtime", Status = "ok", Detail = ".NET 10.0.0" },
            new DoctorCheck
            {
                Name = "license",
                Status = "warn",
                Detail = "evaluation mode",
                Hint = "Produced files carry an Aspose watermark. "
                    + "Set ASPOSE_LICENSE_B64 (or pass --license) to run licensed.",
            },
            new DoctorCheck { Name = "output", Status = "ok", Detail = "writable: /work" },
        ],
        Products = [],
    };

    public static SchemaListResult SchemaList { get; } = new()
    {
        Schemas = ["v2/common/error", "v2/common/schema-list"],
    };

    public static SkillInstallResult SkillInstall { get; } = new()
    {
        Skill = "aspose-cli",
        Target = "/home/user/.agent/skills/aspose-cli",
        FileCount = 8,
    };

    public static SkillListResult SkillList { get; } = new()
    {
        Skills =
        [
            new SkillPackageInfo
            {
                Name = "aspose-cli",
                Description = "Local file automation.",
                Hosts = [],
            },
        ],
    };

    public static FontListResult FontList { get; } = new()
    {
        Products =
        [
            new ProductFonts
            {
                Product = "cells",
                DefaultFont = "Calibri",
                Sources =
                [
                    new FontSource { Kind = "folder", Location = "/usr/share/fonts" },
                    new FontSource { Kind = "memory" },
                ],
            },
        ],
    };

    public static FontCheckResult FontCheck { get; } = new()
    {
        Input = new InputInfo { Path = "D:/data/sample.bin", Format = "bin", SizeBytes = 24576, Fingerprint = new FileFingerprint { Sha256 = new string('0', 64) }, Encrypted = false },
        AllAvailable = false,
        Fonts =
        [
            new FontAvailability { Name = "Calibri", Available = true },
            new FontAvailability { Name = "Custom Sans", Available = false, SubstitutedBy = "Calibri" },
        ],
        License = Evaluation,
    };

    public static IReadOnlyList<CommonSchemaSample> All { get; } =
    [
        new(Error.Schema, Error),
        new(Capabilities.Schema, Capabilities),
        new(CapabilitiesSummary.Schema, CapabilitiesSummary),
        new(LicenseStatus.Schema, LicenseStatus),
        new(Doctor.Schema, Doctor),
        new(Version.Schema, Version),
        new(SchemaList.Schema, SchemaList),
        new(SkillInstall.Schema, SkillInstall),
        new(SkillList.Schema, SkillList),
        new(FontList.Schema, FontList),
        new(FontCheck.Schema, FontCheck),
    ];
}
