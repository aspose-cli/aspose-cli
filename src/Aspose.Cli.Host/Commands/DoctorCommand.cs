using System.CommandLine;
using System.Runtime.InteropServices;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Licensing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.Commands;

/// <summary>Environment self-diagnosis without opening a user document.</summary>
internal static class DoctorCommand
{
    public static Command Create(
        CommandExecutor executor,
        ProductCatalog catalog,
        GlobalOptions globals)
    {
        bool licensingApplicable = catalog.Products.Any(
            static product => product.Manifest.Engine.LicenseApplicable);
        var doctor = new Command(
            "doctor", "Diagnose the environment: runtime, resource budgets and output writability.");
        doctor.SetAction(parse => executor.Run(parse, globals, context =>
        {
            IReadOnlyList<ProductLicenseStatus> licenses =
                LicenseManager.Inspect(context).Products;
            var checks = new List<DoctorCheck>
            {
                CliCheck(),
                RuntimeCheck(),
                BudgetCheck(context),
                OutputCheck(context),
            };
            if (licensingApplicable)
            {
                checks.Insert(3, LicenseCheck(licenses));
            }
            checks.AddRange(catalog.Products
                .SelectMany(product =>
                    product.GetDoctorChecks(context.Activate(product))));
            return new DoctorResult
            {
                Ok = checks.TrueForAll(static check => check.Status != DoctorStatuses.Fail),
                Checks = checks,
                Products = licenses
                    .Select(license => new DoctorProductStatus
                    {
                        Product = license.Product,
                        Engine = catalog
                            .GetCapabilities(catalog.Get(license.Product))
                            .Engine?.Id ?? "aspose",
                        LicenseMode = license.Mode,
                    })
                    .ToArray(),
            };
        }));
        return doctor;
    }

    private static DoctorCheck CliCheck() => new()
    {
        Name = "cli",
        Status = DoctorStatuses.Ok,
        Detail = $"Aspose CLI {VersionInfo.CliVersion}",
    };

    private static DoctorCheck RuntimeCheck() => new()
    {
        Name = "runtime",
        Status = DoctorStatuses.Ok,
        Detail = RuntimeInformation.FrameworkDescription,
    };

    private static DoctorCheck BudgetCheck(CommandContext context)
    {
        string? raw = Environment.GetEnvironmentVariable(
            InputSizeGuard.BudgetVariable);
        if (raw is not null
            && (!long.TryParse(raw, out long configured)
                || configured <= 0
                || configured > InputSizeGuard.MaximumBytes))
        {
            return new DoctorCheck
            {
                Name = "resource-budgets",
                Status = DoctorStatuses.Fail,
                Detail =
                    $"{InputSizeGuard.BudgetVariable} must be between 1 and {InputSizeGuard.MaximumBytes}",
                Hint =
                    "Remove the invalid environment override or set it to a byte value within the capabilities maximum.",
            };
        }

        return new DoctorCheck
        {
            Name = "resource-budgets",
            Status = DoctorStatuses.Ok,
            Detail =
                $"input={context.ResourceBudgets.Limit(ResourceBudgetKinds.InputBytes)} bytes; contract v{ResourceBudgetDefaults.ContractVersion}",
        };
    }

    private static DoctorCheck LicenseCheck(
        IReadOnlyList<ProductLicenseStatus> licenses)
    {
        ProductLicenseStatus[] applicable = licenses
            .Where(static license => license.Applicable)
            .ToArray();
        if (applicable.Length == 0)
        {
            return new DoctorCheck
            {
                Name = "license",
                Status = DoctorStatuses.Ok,
                Detail = "licensing is not applicable to compiled products",
            };
        }

        ProductLicenseStatus[] broken = applicable
            .Where(static license => license.Problem is not null)
            .ToArray();
        if (broken.Length > 0)
        {
            return new DoctorCheck
            {
                Name = "license",
                Status = DoctorStatuses.Fail,
                Detail = string.Join(
                    "; ",
                    broken.Select(license =>
                        $"{license.Product}: {license.Problem!}")),
                Hint = broken[0].Hint,
            };
        }

        int licensed = applicable.Count(static license =>
            license.Mode == LicenseModes.Licensed);
        return licensed == applicable.Length
            ? new DoctorCheck
            {
                Name = "license",
                Status = DoctorStatuses.Ok,
                Detail = $"all {licensed} products licensed",
            }
            : new DoctorCheck
            {
                Name = "license",
                Status = DoctorStatuses.Warn,
                Detail = $"{licensed} of {applicable.Length} products licensed",
                Hint = "Produced files carry an Aspose watermark. "
                    + "Run 'aspose-cli license status' for product-specific setup.",
            };
    }

    private static DoctorCheck OutputCheck(CommandContext context)
    {
        string baseDir = context.Paths.BaseDirectory;
        string probe = Path.Combine(baseDir, "." + Path.GetRandomFileName() + ".aspose-doctor");
        try
        {
            if (WorkerOutputSession.IsActive)
            {
                context.ProductActivation.SafeFileWriter.Write(
                    probe,
                    overwrite: false,
                    temporary => File.WriteAllText(temporary, "ok"));
                WorkerOutputSession.MarkDeleted(probe);
            }
            else
            {
                File.WriteAllText(probe, "ok");
            }

            File.Delete(probe);
            return new DoctorCheck { Name = "output", Status = DoctorStatuses.Ok, Detail = $"writable: {baseDir}" };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new DoctorCheck
            {
                Name = "output",
                Status = DoctorStatuses.Fail,
                Detail = $"not writable: {baseDir}",
                Hint = "Choose a writable --workdir, or fix the directory's permissions.",
            };
        }
    }
}
