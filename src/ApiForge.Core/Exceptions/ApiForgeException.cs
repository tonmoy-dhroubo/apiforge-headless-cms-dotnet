namespace ApiForge.Core;

public sealed class ApiForgeException(string message, int status) : Exception(message)
{
    public int Status { get; } = status;
}

