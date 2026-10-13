namespace SoWeiT.Optimizer.Persistence.History.Data;

public sealed class OptimizerFieldDataEntry
{
    public long Id { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string Station { get; set; } = string.Empty;

    public string Variable { get; set; } = string.Empty;

    public double Value { get; set; }

    public string Type { get; set; } = string.Empty;

    public long? CustomerId { get; set; }

    public CustomerEntry? Customer { get; set; }
}
