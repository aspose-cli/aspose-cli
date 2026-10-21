using System.CommandLine;
using System.Runtime.InteropServices;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Licensing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
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
        var doctor = new Command(
            "doctor", "Diagnose the environment: runtime, native libraries, resource budgets and output writability.");
        doctor.SetAction(parse => executor.Run(parse, globals, context =>
        {
            IReadOnlyList<ProductLicenseStatus> licenses =
                LicenseManager.Inspect(context).Products;
            var checks = new List<DoctorCheck>
            {
                CliCheck(),
                RuntimeCheck(),
                NativeRuntimeCheck(Environment.SystemDirectory),
                BudgetCheck(context),
                LicenseCheck(licenses),
                OutputCheck(context),
            };
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

    /// <summary>The Visual C++ runtime the native engine libraries link against.</summary>
    private static readonly string[] VisualCppRuntime = ["msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll"];

    /// <summary>
    /// Whether <paramref name="systemDirectory"/> holds the Visual C++ runtime. The package does
    /// not carry it, and without it the Slides drawing library cannot load.
    /// </summary>
    internal static DoctorCheck NativeRuntimeCheck(string systemDirectory)
    {
        string[] missing = [.. VisualCppRuntime.Where(name => !File.Exists(Path.Combine(systemDirectory, name)))];
        return missing.Length == 0
            ? new DoctorCheck
            {
                Name = "native-runtime",
                Status = DoctorStatuses.Ok,
                Detail = $"Visual C++ runtime: {string.Join(", ", VisualCppRuntime)}",
            }
            : new DoctorCheck
            {
                Name = "native-runtime",
                Status = DoctorStatuses.Warn,
                Detail = $"Visual C++ runtime missing from {systemDirectory}: {string.Join(", ", missing)}",
                Hint = "Install the Microsoft Visual C++ Redistributable (x64), https://aka.ms/vs/17/release/vc_redist.x64.exe; "
                    + "until then Slides commands that need its native drawing library fail.",
            };
    }

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
                $"input={context.ResourceBudgets.Limit(ResourceBudgetKinds.InputBytes)} bytes",
        };
    }

    private static DoctorCheck LicenseCheck(
        IReadOnlyList<ProductLicenseStatus> licenses)
    {
        ProductLicenseStatus[] broken = licenses
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

        int licensed = licenses.Count(static license =>
            license.Mode == LicenseModes.Licensed);
        if (licenses.All(static license => license.Source == LicenseResolution.RequestedSourceLabel))
        {
            return new DoctorCheck
            {
                Name = "license",
                Status = DoctorStatuses.Warn,
                Detail = $"evaluation mode requested with --license-mode evaluation; no license read for the {licenses.Count} products",
                Hint = "Drop --license-mode evaluation to apply the configured licenses.",
            };
        }
        return licensed == licenses.Count
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
                Detail = $"{licensed} of {licenses.Count} products licensed",
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
            using (var file = new FileStream(probe, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 1, FileOptions.DeleteOnClose))
            {
                file.Write("ok"u8);
                file.Flush(flushToDisk: true);
            }
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
