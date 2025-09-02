namespace GenericRepo.Dapper;

public sealed class DapperOptions
{
    public int CommandTimeoutSeconds { get; init; } = 30; // default
}