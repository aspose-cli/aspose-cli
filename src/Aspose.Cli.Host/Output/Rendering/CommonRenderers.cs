using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.Output.Rendering;

/// <summary>
/// Human renderers for the product-neutral result families (license, fonts, skill
/// install). A second product reuses these unchanged.
/// </summary>
internal static class CommonRenderers
{
    public static void Render(LicenseStatusResult status, TableSurface surface)
    {
        if (status.Products is { Count: > 0 } products)
        {
            var table = new TextTable(
                "product",
                "mode",
                "source",
                "problem");
            foreach (ProductLicenseStatus product in products)
            {
                table.AddRow(
                    product.Product,
                    product.Mode,
                    product.Source,
                    product.Problem);
            }

            table.WriteTo(surface.Out, surface.Format);
            foreach (ProductLicenseStatus product in products)
            {
                if (product.Hint is { } hint)
                {
                    surface.Out.WriteLine($"  {product.Product}: {hint}");
                }
            }
        }
    }

    public static void Render(CapabilitiesResult capabilities, TableSurface surface)
    {
        surface.Out.WriteLine($"aspose-cli {capabilities.CliVersion}");
        surface.Out.WriteLine($"source:  {capabilities.SourceRevision}");
        surface.Out.WriteLine($"dirty:   {TableText.YesNo(capabilities.BuildDirty)}");
        surface.Out.WriteLine(
            $"default: {capabilities.Routing.DefaultProduct ?? "(explicit product required)"}");
        surface.Out.WriteLine(
            $"routing: content probe, {capabilities.Routing.TotalProbeBytes} bytes, "
            + $"{capabilities.Routing.TotalProbeMilliseconds} ms total, "
            + $"{capabilities.Routing.IndeterminatePolicy}");
        ResourceBudgetCapabilities input = capabilities.ResourceBudgets.Single(
            static budget => budget.Kind == "input-bytes");
        surface.Out.WriteLine(
            $"input:   {input.Default} bytes default, {input.Maximum} hard maximum "
            + $"({input.Option})");
        surface.Out.WriteLine(
            $"diagnostics: {capabilities.Diagnostics.Count} "
            + $"({capabilities.Diagnostics.Count(static item => item.Severity == "error")} errors, "
            + $"{capabilities.Diagnostics.Count(static item => item.Severity == "warning")} warnings)");
        foreach (ProductCapabilities product in capabilities.Products)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine(product.Id);
            surface.Out.WriteLine($"  verbs:    {string.Join(", ", product.Verbs)}");
            surface.Out.WriteLine($"  convert:  {string.Join(", ", product.ConvertFormats)}");
            surface.Out.WriteLine($"  render:   {string.Join(", ", product.RenderFormats)}");
            if (product.Operations.Count > 0)
            {
                surface.Out.WriteLine(
                    $"  ops:      {string.Join(", ", product.Operations.SelectMany(static operation => operation.Ops))}");
            }
            surface.Out.WriteLine(
                $"  budgets:  {string.Join(", ", product.ResourceBudgets.Select(static budget => budget.Kind))}");
        }
    }

    public static void Render(CapabilitiesSummaryResult summary, TableSurface surface)
    {
        surface.Out.WriteLine($"aspose-cli {summary.CliVersion}");
        foreach (ProductCapabilitiesSummary product in summary.Products)
        {
            surface.Out.WriteLine();
            string engine = product.Engine is null
                ? string.Empty
                : $" ({product.Engine} {product.EngineVersion})";
            surface.Out.WriteLine($"{product.Id}: {product.Name}{engine}");
            surface.Out.WriteLine($"  load:     {string.Join(", ", product.LoadFormats)}");
            surface.Out.WriteLine($"  convert:  {string.Join(", ", product.ConvertFormats)}");
            surface.Out.WriteLine($"  render:   {string.Join(", ", product.RenderFormats)}");
            foreach (OperationCapabilitiesSummary operation in product.Operations)
            {
                surface.Out.WriteLine(
                    $"  {operation.Command} ops: {string.Join(", ", operation.Ops)}");
            }

            surface.Out.WriteLine();
            var table = new TextTable("command", "description");
            foreach (CommandCapabilitiesSummary command in product.Commands)
            {
                table.AddRow($"{product.Id} {command.Command}", command.Description);
            }

            table.WriteTo(surface.Out, surface.Format);
        }
    }

    public static void Render(VersionResult version, TableSurface surface)
    {
        surface.Out.WriteLine($"aspose-cli {version.CliVersion}");
        if (version.ArtifactVersion is { } artifact)
        {
            surface.Out.WriteLine($"artifact: {artifact}");
        }

        surface.Out.WriteLine($"source:   {version.SourceRevision}");
        surface.Out.WriteLine($"dirty:    {TableText.YesNo(version.BuildDirty)}");
        if (version.EnginePins.Count > 0)
        {
            surface.Out.WriteLine();
            var table = new TextTable("product", "engine", "version");
            foreach (EnginePinCapabilities pin in version.EnginePins)
            {
                table.AddRow(pin.Product, pin.Engine, pin.Version);
            }

            table.WriteTo(surface.Out, surface.Format);
        }
    }

    public static void Render(SchemaListResult result, TableSurface surface)
    {
        surface.Out.WriteLine("schemas:");
        var table = new TextTable("id");
        foreach (string schema in result.Schemas)
        {
            table.AddRow(schema);
        }

        table.WriteTo(surface.Out, surface.Format);
    }

    public static void Render(FontListResult fonts, TableSurface surface)
    {
        var table = new TextTable("product", "default font", "kind", "location");
        foreach (ProductFonts product in fonts.Products)
        {
            string? name = product.Product;
            string fallback = product.DefaultFont ?? "(engine fallback)";
            if (product.Sources.Count == 0)
            {
                table.AddRow(name, fallback, "(platform defaults)", null);
            }

            foreach (FontSource source in product.Sources)
            {
                table.AddRow(name, fallback, source.Kind, source.Location);
            }
        }

        table.WriteTo(surface.Out, surface.Format);
    }

    public static void Render(DryRunResult dryRun, TableSurface surface)
    {
        surface.Out.WriteLine($"dry run: checked {string.Join(", ", dryRun.Checked)}; nothing was written");
        foreach (string path in dryRun.Paths ?? [])
        {
            surface.Out.WriteLine(dryRun.Format is { } format ? $"path: {path} ({format})" : $"path: {path}");
        }
    }

    public static void Render(SkillInstallResult skill, TableSurface surface) =>
        surface.Out.WriteLine($"installed {skill.Skill} to {skill.Target} ({skill.FileCount} files)");

    public static void Render(SkillListResult result, TableSurface surface)
    {
        var table = new TextTable("skill", "hosts", "description");
        foreach (SkillPackageInfo skill in result.Skills)
        {
            table.AddRow(skill.Name, string.Join(", ", skill.Hosts), skill.Description);
        }

        table.WriteTo(surface.Out, surface.Format);
    }
}
