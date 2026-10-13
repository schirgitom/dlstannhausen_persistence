using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SoWeiT.Optimizer.Persistence.History.Data;
using SoWeiT.Optimizer.Persistence.History.Persistence;

namespace SoWeiT.Optimizer.Tests;

public sealed class OptimizerFieldDataTests
{
    [Theory]
    [InlineData("CustomerMeter", "12345")]
    [InlineData("PvYield", null)]
    [InlineData("StationConsumption", null)]
    [InlineData("DirectLinePower", null)]
    public void FieldDataMessage_DeserializesAllTypes(string type, string? customer)
    {
        var json = JsonSerializer.Serialize(new
        {
            Station = "Station01",
            Variable = "Power",
            Value = 22.4,
            Type = type,
            Timestamp = "2026-10-11T12:00:00+02:00",
            Customer = customer
        });

        var data = JsonSerializer.Deserialize<OptimizerFieldData>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(data);
        Assert.Equal(type, data.Type);
        Assert.Equal(customer, data.Customer);
        Assert.Equal(22.4, data.Value);
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 10, 0, 0, TimeSpan.Zero), data.Timestamp?.ToUniversalTime());
    }

    [Theory]
    [InlineData(null, "Power", 1.0, "PvYield", "2026-10-11T12:00:00Z", null)]
    [InlineData("Station01", null, 1.0, "PvYield", "2026-10-11T12:00:00Z", null)]
    [InlineData("Station01", "Power", null, "PvYield", "2026-10-11T12:00:00Z", null)]
    [InlineData("Station01", "Power", double.NaN, "PvYield", "2026-10-11T12:00:00Z", null)]
    [InlineData("Station01", "Power", 1.0, "Unknown", "2026-10-11T12:00:00Z", null)]
    [InlineData("Station01", "Power", 1.0, "PvYield", null, null)]
    [InlineData("Station01", "Power", 1.0, "CustomerMeter", "2026-10-11T12:00:00Z", null)]
    [InlineData("Station01", "Power", 1.0, "PvYield", "2026-10-11T12:00:00Z", "12345")]
    public void Append_RejectsInvalidMeasurements(
        string? station, string? variable, double? value, string type, string? timestamp, string? customer)
    {
        var store = new EfCoreOptimizerFieldDataStore(new UnusedDbContextFactory());
        var data = new OptimizerFieldData(
            station, variable, value, type,
            timestamp is null ? null : DateTimeOffset.Parse(timestamp), customer);

        Assert.Throws<ArgumentException>(() => store.Append(data));
    }

    [Fact]
    public void FieldDataEntity_HasOptionalCustomerRelationship()
    {
        using var context = new OptimizerHistoryDbContext(
            new DbContextOptionsBuilder<OptimizerHistoryDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);
        var entity = context.Model.FindEntityType(typeof(OptimizerFieldDataEntry));
        Assert.NotNull(entity);
        Assert.Equal("optimizer_field_data", entity.GetTableName());
        var foreignKey = Assert.Single(entity.GetForeignKeys());
        Assert.False(foreignKey.IsRequired);
        Assert.Equal(typeof(CustomerEntry), foreignKey.PrincipalEntityType.ClrType);
    }

    private sealed class UnusedDbContextFactory : IDbContextFactory<OptimizerHistoryDbContext>
    {
        public OptimizerHistoryDbContext CreateDbContext() => throw new InvalidOperationException("Unexpected database access.");
    }
}
