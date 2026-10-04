using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyPos.Core.Migrations
{
    /// <inheritdoc />
    public partial class HeldDraftFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerAddress",
                table: "HeldSales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "HeldSales",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DiscountKind",
                table: "HeldSales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrderType",
                table: "HeldSales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "HeldSales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SeniorIdNumber",
                table: "HeldSales",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerAddress",
                table: "HeldSales");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "HeldSales");

            migrationBuilder.DropColumn(
                name: "DiscountKind",
                table: "HeldSales");

            migrationBuilder.DropColumn(
                name: "OrderType",
                table: "HeldSales");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "HeldSales");

            migrationBuilder.DropColumn(
                name: "SeniorIdNumber",
                table: "HeldSales");
        }
    }
}
