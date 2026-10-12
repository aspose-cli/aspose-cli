using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Operations;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>A vocabulary whose common operations name one it does not declare.</summary>
[OperationVocabulary("misnamed-ops", JsonContext = typeof(TestOpsJsonContext), Common = new[] { "plaec" })]
[JsonConverter(typeof(OperationJsonConverter<MisnamedCommonOp>))]
public abstract partial record MisnamedCommonOp : BoundedOperation;

public sealed class CommonOperationsTests
{
    [Fact]
    public void ACommonOperationTheVocabularyDoesNotDeclare_IsRefusedWhenTheCatalogIsBuilt()
    {
        TypeInitializationException error = Assert.ThrowsAny<TypeInitializationException>(static () => MisnamedCommonOp.Catalog);

        ArgumentException refused = Assert.IsType<ArgumentException>(error.InnerException, exactMatch: false);
        Assert.Contains("'plaec' is not one of its operations", refused.Message, StringComparison.Ordinal);
    }
}
