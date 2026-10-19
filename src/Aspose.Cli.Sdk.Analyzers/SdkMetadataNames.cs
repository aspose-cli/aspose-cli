namespace Aspose.Cli.Sdk.Analyzers;

/// <summary>
/// The metadata names of the SDK types the analyzers and generators recognize. They build for
/// netstandard2.0 and cannot reference the SDK, so this table is the one place those names are
/// written: a test resolves every entry in the SDK, and a generator that cannot resolve the
/// types it needs reports <see cref="OperationContractGenerator.MissingSdkType"/> instead of
/// skipping the rules that depend on them.
/// </summary>
/// <remarks>A constant whose field name ends in <c>Namespace</c> names a namespace; every other constant names a type.</remarks>
internal static class SdkMetadataNames
{
    internal const string SdkAssembly = "Aspose.Cli.Sdk";

    internal const string OperationsNamespace = "Aspose.Cli.Sdk.Operations";
    internal const string ContractsNamespace = "Aspose.Cli.Sdk.Contracts";
    internal const string CommandingNamespace = "Aspose.Cli.Sdk.Extensibility.Commanding";

    // Product modules and their definitions.
    internal const string ProductModuleInterface = "Aspose.Cli.Sdk.Extensibility.IProductModule";
    internal const string ProductModuleAttribute = "Aspose.Cli.Sdk.Extensibility.ProductModuleAttribute";
    internal const string ProductDefinitionBuilder = "Aspose.Cli.Sdk.Extensibility.ProductDefinitionBuilder`1";
    internal const string Product = "Aspose.Cli.Sdk.Extensibility.Product";
    internal const string ProductManifest = "Aspose.Cli.Sdk.Extensibility.ProductManifest";
    internal const string ProductMenu = CommandingNamespace + ".ProductMenu";
    internal const string StandardOptions = CommandingNamespace + ".StandardOptions";
    internal const string FormatDescriptor = "Aspose.Cli.Sdk.IO.FormatDescriptor";
    internal const string FormatDescriptorExtensions = "Aspose.Cli.Sdk.IO.FormatDescriptorExtensions";
    internal const string FileFormatRecognition = "Aspose.Cli.Sdk.IO.FileFormatRecognition";
    internal const string FileProbePattern = "Aspose.Cli.Sdk.IO.FileProbePattern";
    internal const string RouteOwnership = "Aspose.Cli.Sdk.IO.RouteOwnership";
    internal const string ResourceBudgetCapabilities = ContractsNamespace + ".ResourceBudgetCapabilities";
    internal const string ErrorCode = "Aspose.Cli.Sdk.Errors.ErrorCode";
    internal const string DiagnosticDescriptor = "Aspose.Cli.Sdk.Diagnostics.DiagnosticDescriptor";
    internal const string ViewPartKinds = "Aspose.Cli.Sdk.Views.ViewPartKinds";
    internal const string ProductView = "Aspose.Cli.Sdk.Views.ProductView";
    internal const string ProductJsonDefinition = "Aspose.Cli.Sdk.Serialization.ProductJsonDefinition";
    internal const string ProductJsonRootAttribute = "Aspose.Cli.Sdk.Serialization.ProductJsonRootAttribute";

    // Operation contracts.
    internal const string BoundedOperation = ContractsNamespace + ".BoundedOperation";
    internal const string OperationVocabularyAttribute = OperationsNamespace + ".OperationVocabularyAttribute";
    internal const string OperationAttribute = OperationsNamespace + ".OperationAttribute";
    internal const string OperationInputAttribute = OperationsNamespace + ".OperationInputAttribute";
    internal const string MistakenForAttribute = OperationsNamespace + ".MistakenForAttribute";
    internal const string EncryptsOutputAttribute = OperationsNamespace + ".EncryptsOutputAttribute";
    internal const string RecordRuleAttribute = OperationsNamespace + ".RecordRuleAttribute";
    internal const string MemberCountAttribute = OperationsNamespace + ".MemberCountAttribute";
    internal const string MinPropertiesAttribute = OperationsNamespace + ".MinPropertiesAttribute";
    internal const string InputPathAttribute = OperationsNamespace + ".InputPathAttribute";
    internal const string SecretEnvAttribute = OperationsNamespace + ".SecretEnvAttribute";
    internal const string ReadBackAttribute = OperationsNamespace + ".ReadBackAttribute";
    internal const string NotReadBackAttribute = OperationsNamespace + ".NotReadBackAttribute";
    internal const string ReadBackByPositionAttribute = OperationsNamespace + ".ReadBackByPositionAttribute";
    internal const string NotStateAttribute = OperationsNamespace + ".NotStateAttribute";

    // Result contracts.
    internal const string ResultEnvelope = ContractsNamespace + ".ResultEnvelope";
    internal const string OutputItem = ContractsNamespace + ".OutputItem";
    internal const string EditVerification = ContractsNamespace + ".EditVerification";
    internal const string WarningCode = ContractsNamespace + ".WarningCode";
    internal const string SchemaIdAttribute = ContractsNamespace + ".SchemaIdAttribute";
    internal const string OpenEnumAttribute = ContractsNamespace + ".OpenEnumAttribute";
    internal const string OneOfByAttribute = ContractsNamespace + ".OneOfByAttribute";

    // Value constraints, shared by operation and result contracts.
    internal const string ValueConstraintAttribute = ContractsNamespace + ".ValueConstraintAttribute";
    internal const string MinItemsAttribute = ContractsNamespace + ".MinItemsAttribute";
    internal const string MaxItemsAttribute = ContractsNamespace + ".MaxItemsAttribute";
    internal const string UniqueItemsAttribute = ContractsNamespace + ".UniqueItemsAttribute";
    internal const string MinimumAttribute = ContractsNamespace + ".MinimumAttribute";
    internal const string MaximumAttribute = ContractsNamespace + ".MaximumAttribute";
    internal const string ExclusiveMinimumAttribute = ContractsNamespace + ".ExclusiveMinimumAttribute";
    internal const string MultipleOfAttribute = ContractsNamespace + ".MultipleOfAttribute";
    internal const string AllowedValuesAttribute = ContractsNamespace + ".AllowedValuesAttribute";
    internal const string JsonScalarAttribute = ContractsNamespace + ".JsonScalarAttribute";
    internal const string MinLengthAttribute = ContractsNamespace + ".MinLengthAttribute";
    internal const string MaxLengthAttribute = ContractsNamespace + ".MaxLengthAttribute";
    internal const string PatternAttribute = ContractsNamespace + ".PatternAttribute";
    internal const string ErrorDetailsAttribute = ContractsNamespace + ".ErrorDetailsAttribute";
    internal const string HexColorAttribute = OperationsNamespace + ".HexColorAttribute";
    internal const string WebLinkAttribute = OperationsNamespace + ".WebLinkAttribute";
    internal const string ValueKindAttribute = OperationsNamespace + ".ValueKindAttribute";

    /// <summary>The types the contract generator resolves as symbols; it describes no contract when one is missing.</summary>
    internal static readonly string[] ContractSymbols =
    [
        ValueConstraintAttribute,
        RecordRuleAttribute,
        MemberCountAttribute,
        MinPropertiesAttribute,
        InputPathAttribute,
        SecretEnvAttribute,
        ReadBackAttribute,
        NotReadBackAttribute,
        ReadBackByPositionAttribute,
        NotStateAttribute,
        OperationInputAttribute,
    ];

    /// <summary>The System.Text.Json types the contract generators recognize.</summary>
    internal static class Json
    {
        internal const string SerializationNamespace = "System.Text.Json.Serialization";

        internal const string JsonElement = "System.Text.Json.JsonElement";
        internal const string JsonObject = "System.Text.Json.Nodes.JsonObject";
        internal const string JsonNode = "System.Text.Json.Nodes.JsonNode";
        internal const string SerializerContext = SerializationNamespace + ".JsonSerializerContext";
        internal const string SerializableAttribute = SerializationNamespace + ".JsonSerializableAttribute";
        internal const string SourceGenerationOptionsAttribute = SerializationNamespace + ".JsonSourceGenerationOptionsAttribute";
        internal const string PropertyNameAttribute = SerializationNamespace + ".JsonPropertyNameAttribute";
        internal const string IgnoreAttribute = SerializationNamespace + ".JsonIgnoreAttribute";
        internal const string ExtensionDataAttribute = SerializationNamespace + ".JsonExtensionDataAttribute";
        internal const string PropertyOrderAttribute = SerializationNamespace + ".JsonPropertyOrderAttribute";
    }
}
