using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Contracts;
using Aspose.Cli.Sdk.Contracts;


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
        },
    };

    public static SchemaCatalogResult Catalog { get; } = new()
    {
        CliVersion = "development",
        Products =
        [
            new CatalogProduct
            {
                Id = "test",
                Description = "Read and edit test documents.",
                Engine = "Aspose.Test",
                EngineVersion = "1.0.0",
                Commands = [new CatalogCommand { Name = "edit", Description = "Apply a batch of ops." }],
                LoadFormats = ["test"],
                ConvertFormats = [],
                RenderFormats = ["png"],
            },
        ],
        Commands = [new CatalogCommand { Name = "license", Description = "Manage the license." }],
        Ids = ["v3/common/error", "v3/test/edit-command"],
    };

    public static CommandDocument Command { get; } = new()
    {
        Path = "aspose-cli test edit",
        Description = "Apply a batch of ops.",
        Options =
        [
            new CommandOption
            {
                Name = "--ops",
                Aliases = [],
                Type = "string",
                MinimumArity = 1,
                MaximumArity = 1,
                Required = true,
                Recursive = false,
                HasDefault = false,
                AllowedValues = [],
                Secret = false,
                ValueSource = "command-line",
                InputKind = "json-source",
                Description = "The operation document.",
            },
        ],
        Arguments =
        [
            new CommandArgument
            {
                Name = "file",
                Type = "string",
                InputKind = "file",
                ValueSource = "command-line",
                Secret = false,
                MinimumArity = 1,
                MaximumArity = 1,
                Required = true,
                HasDefault = false,
                AllowedValues = [],
            },
        ],
    };

    public static FormatListResult Formats { get; } = new()
    {
        Product = "test",
        Formats = [new FormatInfo { Id = "test", Extensions = [".test"], Aliases = [], Uses = ["input"] }],
    };

    public static DiagnosticListResult Diagnostics { get; } = new()
    {
        Diagnostics =
        [
            new DiagnosticInfo
            {
                Code = "FILE_NOT_FOUND",
                Owner = "common",
                Severity = "error",
                ExitCode = 3,
                Category = "file",
                DetailsSchemaId = "v3/common/not-found-details",
            },
        ],
    };

    public static BudgetListResult Budgets { get; } = new()
    {
        ResourceBudgets =
        [
            ResourceBudget.Domain(
                "input-bytes",
                1L << 30,
                4L << 30,
                "bytes",
                "pre-runtime-and-stream",
                "--max-input-bytes"),
        ],
        Products = [new ProductBudgets { Product = "test", ResourceBudgets = [] }],
    };

    public static VersionResult Version { get; } = new()
    {
        CliVersion = "1.0.0",
        ArtifactVersion = "1.0.0+0123456789abcdef0123456789abcdef01234567",
        SourceRevision = "0123456789abcdef0123456789abcdef01234567",
        BuildDirty = false,
        EnginePins = [new EnginePin { Product = "test", Engine = "Aspose.Test", Version = "1.0.0" }],
    };

    public static LicenseStatusResult LicenseStatus { get; } = new()
    {
        Products = [new ProductLicenseStatus
        {
            Product = "cells", Name = "Cells", Mode = LicenseModes.Evaluation,
        }],
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

    public static IReadOnlyList<CommonSchemaSample> All { get; } =
    [
        new(Error.Schema, Error),
        new(Catalog.Schema, Catalog),
        new(Command.Schema, Command),
        new(Formats.Schema, Formats),
        new(Diagnostics.Schema, Diagnostics),
        new(Budgets.Schema, Budgets),
        new(LicenseStatus.Schema, LicenseStatus),
        new(Version.Schema, Version),
        new(SkillInstall.Schema, SkillInstall),
        new(SkillList.Schema, SkillList),
        new(FontList.Schema, FontList),
    ];
}
