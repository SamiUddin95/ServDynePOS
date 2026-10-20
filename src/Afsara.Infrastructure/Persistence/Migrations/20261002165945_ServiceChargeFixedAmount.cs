using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Afsara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ServiceChargeFixedAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServiceChargePercent",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "ServiceChargePercent",
                table: "Orders");

            migrationBuilder.AddColumn<int>(
                name: "ServiceChargeAmount",
                table: "RestaurantSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ServiceChargeAmount",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServiceChargeAmount",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "ServiceChargeAmount",
                table: "Orders");

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceChargePercent",
                table: "RestaurantSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceChargePercent",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
