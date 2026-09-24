using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class EditCommand
{
    private static readonly BoundedEditDefinition<PdfOp, PdfOpsBatch> Definition = new()
    {
        Catalog = PdfOps.Catalog,
        Contracts = ProductJsonContext.Definition,
        NormalizePaths = static (op, paths) => op switch
        {
            InsertPagesFromOp value => value with { Path = paths.ResolveInput(value.Path) },
            AddWatermarkImageOp value => value with { Path = paths.ResolveInput(value.Path) },
            AddStampImageOp value => value with { Path = paths.ResolveInput(value.Path) },
            AddAttachmentOp value => value with { Path = paths.ResolveInput(value.Path) },
            _ => op,
        },
    };

    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        Argument<string> file = PdfOptions.File();
        var edit = new BoundedEditCommand<PdfOp, PdfOpsBatch>(Definition);
        var password = new PasswordOptions("--password", "the PDF");
        var fonts = new FontDirectoryOptions();
        var command = new Command("edit", "Apply one validated, atomic PDF operation batch.");
        command.Arguments.Add(file);
        edit.AddTo(command);
        password.AddTo(command);
        fonts.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            BoundedEditInvocation<PdfOpsBatch> invocation = edit.Read(parse, context.Paths, context.Inputs, input);
            using IDisposable fontScope = fonts.Use(parse, context);
            return context.Port.ApplyOps(input, invocation.Batch, new PdfEditRequest
            {
                OutputPath = invocation.Target.OutputPath,
                Overwrite = invocation.Target.Overwrite,
                BackupPath = invocation.Target.BackupPath,
                Options = invocation.Options,
                Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment, stdinAvailable: !invocation.OpsFromStandardInput),
                OpSecrets = ResolveSecrets(invocation.Batch, context.ReadEnvironment),
            });
        }));
        return command;
    }

    private static IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>>? ResolveSecrets(PdfOpsBatch batch, Func<string, string?> readEnvironment)
    {
        var resolved = new Dictionary<int, IReadOnlyDictionary<string, string>>();
        for (int index = 0; index < batch.Ops.Count; index++)
        {
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            switch (batch.Ops[index])
            {
                case InsertPagesFromOp { PasswordEnv: not null } insert:
                    names["password"] = ResolveEnvironment(insert.PasswordEnv, readEnvironment);
                    break;
                case EncryptPdfOp encrypt:
                    names["ownerPassword"] = ResolveEnvironment(encrypt.OwnerPasswordEnv, readEnvironment);
                    if (encrypt.UserPasswordEnv is not null)
                    {
                        names["userPassword"] = ResolveEnvironment(encrypt.UserPasswordEnv, readEnvironment);
                    }

                    break;
            }

            if (names.Count > 0)
            {
                resolved[index] = names;
            }
        }

        return resolved.Count == 0 ? null : resolved;
    }

    private static string ResolveEnvironment(string variable, Func<string, string?> readEnvironment)
    {
        string? value = readEnvironment(variable);
        if (string.IsNullOrEmpty(value))
        {
            throw CliErrors.OptionInvalid(
                "passwordEnv",
                $"environment variable '{variable}' is missing or empty",
                "Set it before running the edit.");
        }

        return value;
    }
}
