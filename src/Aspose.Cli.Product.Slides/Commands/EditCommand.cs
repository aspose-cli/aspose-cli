using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class EditCommand
{
    private static readonly BoundedEditDefinition<SlidesOp, SlidesOpsBatch> Definition = new()
    {
        Catalog = SlidesOps.Catalog,
        Contracts = ProductJsonContext.Definition,
        NormalizePaths = static (op, paths) => op switch
        {
            SetBackgroundOp { ImagePath: not null } value =>
                value with { ImagePath = paths.ResolveInput(value.ImagePath) },
            AppendPresentationOp value =>
                value with { Path = paths.ResolveInput(value.Path) },
            SlidesInsertImageOp value =>
                value with { Path = paths.ResolveInput(value.Path) },
            _ => op,
        },
    };

    public static Command Create(IProductCommandHost<IPresentationEngine> host)
    {
        Argument<string> file = SlidesOptions.File();
        var edit = new BoundedEditCommand<SlidesOp, SlidesOpsBatch>(Definition);
        var password = new PasswordOptions("--password", "the presentation");
        var encrypt = new PasswordOptions("--encrypt", "the output presentation", allowStdin: false);
        var command = new Command("edit", "Apply one validated, atomic presentation operation batch.");
        command.Arguments.Add(file);
        edit.AddTo(command);
        password.AddTo(command);
        encrypt.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            BoundedEditInvocation<SlidesOpsBatch> invocation = edit.Read(parse, context.Paths, context.Inputs, input);
            return context.Port.ApplyOps(input, invocation.Batch, new PresentationEditRequest
            {
                OutputPath = invocation.Target.OutputPath,
                Overwrite = invocation.Target.Overwrite,
                BackupPath = invocation.Target.BackupPath,
                Options = invocation.Options,
                Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment, stdinAvailable: !invocation.OpsFromStandardInput),
                EncryptPassword = encrypt.Resolve(parse, context.Inputs, context.ReadEnvironment),
            });
        }));
        return command;
    }
}
