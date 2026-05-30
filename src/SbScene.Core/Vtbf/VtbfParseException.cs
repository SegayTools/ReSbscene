namespace SbScene.Core.Vtbf;

public sealed class VtbfParseException : Exception
{
    public VtbfParseException(string message)
        : base(message)
    {
    }

    public VtbfParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
