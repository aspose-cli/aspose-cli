using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.Output.Rendering;

/// <summary>
/// Human renderers for the product-neutral result families (license, doctor,
/// fonts, skill install, update). A second product reuses these unchanged.
/// </summary>
internal static class CommonRenderers
{
    public static void Render(AppResult app, TableSurface surface)
    {
        surface.Out.WriteLine(app.Running ? "app:     running" : "app:     stopped");
        if (app.Url is { } url)
        {
            surface.Out.WriteLine($"url:     {url}");
        }

        if (app.Pid is { } pid)
        {
            surface.Out.WriteLine($"pid:     {pid}");
        }

        surface.Out.WriteLine($"route:   {app.Route}");
        if (app.File is { } file)
        {
            surface.Out.WriteLine($"file:    {file}");
        }
    }

    public static void Render(LicenseStatusResult status, TableSurface surface)
    {
        surface.Out.WriteLine($"applicable: {TableText.YesNo(status.Applicable)}");
        if (status.Products is { Count: > 0 } products)
        {
            surface.Out.WriteLine();
            var table = new TextTable(
                "product",
                "applicable",
                "mode",
                "source",
                "problem");
            foreach (ProductLicenseStatus product in products)
            {
                table.AddRow(
                    product.Product,
                    TableText.YesNo(product.Applicable),
                    product.Mode,
                    product.Source ?? "-",
                    product.Problem ?? "-");
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
            $"budgets: contract v{capabilities.ResourceBudgetContractVersion}");
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
                table.AddRow($"{product.Id} {command.Command}", command.Description ?? "-");
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

    public static void Render(DoctorResult doctor, TableSurface surface)
    {
        surface.Out.WriteLine(doctor.Ok ? "doctor: ready" : "doctor: not ready");
        surface.Out.WriteLine();

        var table = new TextTable("check", "status", "detail");
        foreach (DoctorCheck check in doctor.Checks)
        {
            table.AddRow(check.Name, check.Status, check.Detail);
        }

        table.WriteTo(surface.Out, surface.Format);

        foreach (DoctorCheck check in doctor.Checks)
        {
            if (check.Hint is { } hint)
            {
                surface.Out.WriteLine($"  {check.Name}: {hint}");
            }
        }

        if (doctor.Products is { Count: > 0 } products)
        {
            surface.Out.WriteLine();
            var productTable = new TextTable("product", "engine", "license");
            foreach (DoctorProductStatus product in products)
            {
                productTable.AddRow(
                    product.Product,
                    product.Engine,
                    product.LicenseMode);
            }

            productTable.WriteTo(surface.Out, surface.Format);
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
        surface.Out.WriteLine($"default font: {fonts.DefaultFont ?? "(engine fallback)"}");
        surface.Out.WriteLine();

        if (fonts.Sources.Count == 0)
        {
            surface.Out.WriteLine("sources: (engine platform defaults)");
            return;
        }

        var table = new TextTable("type", "location");
        foreach (FontSource source in fonts.Sources)
        {
            table.AddRow(source.Type, source.Location ?? "-");
        }

        table.WriteTo(surface.Out, surface.Format);
    }

    public static void Render(FontCheckResult check, TableSurface surface)
    {
        surface.Out.WriteLine(check.AllAvailable
            ? $"{check.Source.Path}: all fonts available"
            : $"{check.Source.Path}: some fonts substituted");
        surface.Out.WriteLine();

        var table = new TextTable("font", "available", "substituted by");
        foreach (FontAvailability font in check.Fonts)
        {
            table.AddRow(font.Name, TableText.YesNo(font.Available), font.SubstitutedBy ?? "-");
        }

        table.WriteTo(surface.Out, surface.Format);
    }

    public static void Render(UpdateResult update, TableSurface surface)
    {
        surface.Out.WriteLine($"status:    {update.Status}");
        surface.Out.WriteLine($"current:   {update.CurrentVersion}");
        WriteIfPresent(surface, "available: ", update.AvailableVersion);
        WriteIfPresent(surface, "source:    ", update.SourceRevision);
        WriteIfPresent(surface, "feed:      ", update.Feed);
        WriteIfPresent(surface, "sha256:    ", update.ArchiveSha256);
        if (update.ProcessId is { } pid)
        {
            surface.Out.WriteLine($"installer: pid {TableText.Int(pid)}");
        }
    }

    private static void WriteIfPresent(TableSurface surface, string label, string? value)
    {
        if (value is not null)
        {
            surface.Out.WriteLine(label + value);
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

    public static void Render(ProductPreviewStartResult preview, TableSurface surface)
    {
        surface.Out.WriteLine($"{(preview.Reused ? "reused" : "started")} {preview.Product} preview {preview.Id}");
        surface.Out.WriteLine($"  url:      {preview.Url}");
        surface.Out.WriteLine($"  file:     {preview.File}");
        surface.Out.WriteLine($"  view:     {preview.View}");
        surface.Out.WriteLine($"  pid:      {TableText.Int(preview.Pid)}");
    }

    public static void Render(ProductPreviewStatusResult preview, TableSurface surface)
    {
        if (preview.Stopped is { } stopped)
        {
            surface.Out.WriteLine(stopped.Count == 0
                ? "no matching preview session was running"
                : $"stopped: {string.Join(", ", stopped)}");
        }

        RenderPreviewSessions(preview.Sessions, surface);
    }

    public static void Render(ReviewResult review, TableSurface surface)
    {
        surface.Out.WriteLine($"review:  {review.OutputDirectory}");
        surface.Out.WriteLine($"product: {review.Product}");
        surface.Out.WriteLine($"view:    {review.View}");
        surface.Out.WriteLine($"source:  {review.SourceFormat}, encrypted: {TableText.YesNo(review.SourceEncrypted)}");
        surface.Out.WriteLine($"index:   {review.Index}");
        surface.Out.WriteLine($"manifest:{review.Manifest}");
        surface.Out.WriteLine(
            $"evidence:{review.Coverage.ReportedItemCount}/{review.Coverage.DiscoveredItemCount} artifacts");
        surface.Out.WriteLine(
            $"visual inspection required: {review.VisualInspectionRequired.ToString().ToLowerInvariant()}");
    }

    private static void RenderPreviewSessions(
        IReadOnlyList<ProductPreviewSessionInfo> sessions,
        TableSurface surface)
    {
        if (sessions.Count == 0)
        {
            surface.Out.WriteLine("no background previews are running");
            return;
        }

        var table = new TextTable("id", "product", "pid", "revision", "view", "file", "url");
        foreach (ProductPreviewSessionInfo session in sessions)
        {
            table.AddRow(
                session.Id,
                session.Product,
                TableText.Int(session.Pid),
                session.Revision?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-",
                session.View,
                session.File,
                session.Url);
        }

        table.WriteTo(surface.Out, surface.Format);
    }
}
