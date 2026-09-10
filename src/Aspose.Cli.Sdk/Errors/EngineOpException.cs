namespace Aspose.Cli.Sdk.Errors;

/// <summary>SDK-neutral exception raised by an engine while applying one operation.</summary>
public sealed class EngineOpException : Exception
{
    /// <summary>Wraps an SDK failure without exposing an SDK type through a port.</summary>
    public EngineOpException(string message, Exception innerException) : base(message, innerException) { }
}
