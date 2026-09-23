using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class EditCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        Argument<string> file = PdfOptions.File();
        var ops = new Option<string>("--ops") { Required = true, Description = "Ops JSON path, inline JSON, or '-' for stdin." }.WithInput(InputKind.JsonSource);
        var output = new MutationFileOptions();
        var editOptions = new BoundedEditOptions();
        var password = new PasswordOptions("--password", "the PDF");
        var command = new Command("edit", "Apply one validated, atomic PDF operation batch.");
        command.Arguments.Add(file);
        command.Options.Add(ops);
        output.AddTo(command);
        editOptions.AddTo(command);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string source = parse.GetRequiredValue(ops);
            PdfOpsBatch batch = PdfOps.Catalog.Parse<PdfOpsBatch>(
                JsonInputSource.Read(source, context.Paths, context.Inputs, "--ops"),
                ProductJsonContext.Definition);
            batch = NormalizePaths(batch, context);
            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            MutationTarget target = output.Resolve(parse, context.Paths, input, requireBackup: true);
            return context.Port.ApplyOps(input, batch, new PdfEditRequest
            {
                OutputPath = target.OutputPath,
                Overwrite = target.Overwrite,
                BackupPath = target.BackupPath,
                Options = editOptions.Read(parse, batch.IfMatch),
                Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment, stdinAvailable: source != "-"),
                OpSecrets = ResolveSecrets(batch, context.ReadEnvironment),
            });
        }));
        return command;
    }

    private static PdfOpsBatch NormalizePaths(
        PdfOpsBatch batch,
        ProductCommandContext<IPdfEngine> context) =>
        batch with
        {
            Ops = batch.Ops.Select(op => op switch
            {
                InsertPagesFromOp value => value with { Path = context.Paths.ResolveInput(value.Path) },
                AddWatermarkImageOp value => value with { Path = context.Paths.ResolveInput(value.Path) },
                AddStampImageOp value => value with { Path = context.Paths.ResolveInput(value.Path) },
                AddAttachmentOp value => value with { Path = context.Paths.ResolveInput(value.Path) },
                _ => op,
            }).ToArray(),
        };

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
