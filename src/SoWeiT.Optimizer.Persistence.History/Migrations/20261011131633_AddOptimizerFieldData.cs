using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoWeiT.Optimizer.Persistence.History.Migrations
{
    /// <inheritdoc />
    public partial class AddOptimizerFieldData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "optimizer_field_data",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Station = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Variable = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_optimizer_field_data", x => x.Id);
                    table.ForeignKey(
                        name: "FK_optimizer_field_data_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_optimizer_field_data_CustomerId_Timestamp",
                table: "optimizer_field_data",
                columns: new[] { "CustomerId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_optimizer_field_data_Station_Variable_Timestamp",
                table: "optimizer_field_data",
                columns: new[] { "Station", "Variable", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_optimizer_field_data_Timestamp",
                table: "optimizer_field_data",
                column: "Timestamp");

            migrationBuilder.Sql(
                """
                SELECT create_hypertable(
                    'optimizer_field_data',
                    by_range('Id', 100000),
                    if_not_exists => TRUE
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "optimizer_field_data");
        }
    }
}
