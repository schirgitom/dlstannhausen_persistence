using Microsoft.EntityFrameworkCore;
using SoWeiT.Optimizer.Persistence.History.Data;

namespace SoWeiT.Optimizer.Persistence.History.Persistence;

public sealed class EfCoreOptimizerFieldDataStore : IOptimizerFieldDataStore
{
    private readonly IDbContextFactory<OptimizerHistoryDbContext> _dbContextFactory;

    public EfCoreOptimizerFieldDataStore(IDbContextFactory<OptimizerHistoryDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public void Append(OptimizerFieldData data)
    {
        if (string.IsNullOrWhiteSpace(data.Station) || string.IsNullOrWhiteSpace(data.Variable))
        {
            throw new ArgumentException("Station and Variable are required.");
        }

        if (data.Value is not { } value || !double.IsFinite(value))
        {
            throw new ArgumentException("Value must be a finite number.");
        }

        if (data.Timestamp is not { } timestamp)
        {
            throw new ArgumentException("Timestamp is required.");
        }

        if (data.Type is not ("CustomerMeter" or "PvYield" or "StationConsumption" or "DirectLinePower"))
        {
            throw new ArgumentException($"Unsupported field data type '{data.Type}'.");
        }

        var customerNumber = data.Customer?.Trim();
        if (data.Type == "CustomerMeter" && string.IsNullOrEmpty(customerNumber))
        {
            throw new ArgumentException("CustomerMeter requires a customer number.");
        }

        if (data.Type != "CustomerMeter" && !string.IsNullOrEmpty(customerNumber))
        {
            throw new ArgumentException("Customer is only valid for CustomerMeter.");
        }

        using var dbContext = _dbContextFactory.CreateDbContext();
        CustomerEntry? customer = null;
        if (data.Type == "CustomerMeter")
        {
            customer = dbContext.Customers.SingleOrDefault(x => x.CustomerNumber == customerNumber)
                ?? throw new InvalidOperationException($"Customer with customer number '{customerNumber}' was not found.");
        }

        dbContext.FieldData.Add(new OptimizerFieldDataEntry
        {
            Timestamp = timestamp.ToUniversalTime(),
            Station = data.Station.Trim(),
            Variable = data.Variable.Trim(),
            Value = value,
            Type = data.Type,
            Customer = customer
        });
        dbContext.SaveChanges();
    }
}
