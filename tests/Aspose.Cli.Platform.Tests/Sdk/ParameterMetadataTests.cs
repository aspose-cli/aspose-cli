using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class ParameterMetadataTests
{
    [Fact]
    public void StringSymbols_RequireAnExplicitDeclaration()
    {
        var command = new Command("sample");
        command.Arguments.Add(new Argument<string>("file"));
        Assert.Contains("file", Assert.Throws<InvalidOperationException>(command.ValidateParameters).Message);
        var option = new Option<string[]>("--names");
        Assert.Throws<InvalidOperationException>(() => option.GetParameterMetadata());
        Assert.Throws<InvalidOperationException>(() => new Argument<FileInfo>("typed-file").GetParameterMetadata());
    }

    [Fact]
    public void CommandWithSubcommands_TakesNoArgument()
    {
        var command = new Command("viewer");
        command.Arguments.Add(new Argument<string?>("file").WithInput(InputKind.File));
        command.Subcommands.Add(new Command("status"));

        Assert.Contains("viewer", Assert.Throws<InvalidOperationException>(command.ValidateParameters).Message);
    }

    [Fact]
    public void Scalars_DefaultToOrdinaryValues()
    {
        new RootCommand().ValidateParameters();
        Assert.Equal(new ParameterMetadata(InputKind.None),
            new Option<int>("--count").GetParameterMetadata());
        Assert.Equal(new ParameterMetadata(InputKind.None),
            new Option<bool>("--overwrite").GetParameterMetadata());
    }

    [Fact]
    public void RoleAndSource_AreImmutableAndCannotBeDeclaredTwice()
    {
        var input = new Argument<string>("anything").WithInput(InputKind.File);
        Assert.Equal(InputKind.File, input.GetParameterMetadata().InputKind);
        Assert.Throws<InvalidOperationException>(() => input.WithInput(InputKind.None));
        Assert.Throws<InvalidOperationException>(() => input.WithInput(InputKind.File));
        Assert.Equal(InputKind.File, input.GetParameterMetadata().InputKind);
    }

    [Fact]
    public void InvalidTypeAndSourceCombinations_AreRejectedAtDeclaration()
    {
        Assert.Throws<InvalidOperationException>(() => new Option<bool>("--flag").WithInput(InputKind.File));
        Assert.Throws<InvalidOperationException>(() => new Option<string[]>("--ops").WithInput(InputKind.JsonSource));
        Assert.Throws<InvalidOperationException>(() => new Option<string>("--file").WithInput(
            InputKind.File, ParameterValueSource.EnvironmentVariableName));
        Assert.Throws<InvalidOperationException>(() => new Option<int>("--count").WithInput(
            InputKind.None, ParameterValueSource.EnvironmentVariableName));
        Assert.Throws<InvalidOperationException>(() => new Argument<string>("stdin").WithInput(
            InputKind.None, ParameterValueSource.StandardInput));
        Assert.Throws<ArgumentException>(() => new Command("sample").WithInput(InputKind.None));
    }

    [Fact]
    public void SharedPasswordFactory_DeclaresEverySecretSourceOnce()
    {
        var command = new Command("sample");
        new PasswordOptions("--credential", "test resource").AddTo(command);
        command.ValidateParameters();
        Assert.All(command.Options, option => Assert.True(option.GetParameterMetadata().Secret));
        Assert.Equal(ParameterValueSource.EnvironmentVariableName,
            command.Options.Single(option => option.Name == "--credential-env").GetParameterMetadata().ValueSource);
        Assert.Equal(ParameterValueSource.StandardInput,
            command.Options.Single(option => option.Name == "--credential-stdin").GetParameterMetadata().ValueSource);
    }

    [Fact]
    public void Validation_ReachesNestedCommandsAndStringArrays()
    {
        var root = new RootCommand();
        var product = new Command("product");
        var nested = new Command("query");
        nested.Options.Add(new Option<string[]>("--names").WithInput(InputKind.None));
        nested.Arguments.Add(new Argument<string[]>("files").WithInput(InputKind.File));
        product.Subcommands.Add(nested);
        root.Subcommands.Add(product);
        root.ValidateParameters();
        nested.Options.Add(new Option<string>("--forgotten"));
        Assert.Throws<InvalidOperationException>(root.ValidateParameters);
    }
}
