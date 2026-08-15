namespace CS2Migrate.Core;

public sealed class MigrationException : Exception
{
    public MigrationException(string message) : base(message)
    {
    }

    public MigrationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
