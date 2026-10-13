namespace SoWeiT.Optimizer.Persistence.History.Persistence;

public sealed record OptimizerFieldData(
    string? Station,
    string? Variable,
    double? Value,
    string? Type,
    DateTimeOffset? Timestamp,
    string? Customer = null);

public interface IOptimizerFieldDataStore
{
    void Append(OptimizerFieldData data);
}
