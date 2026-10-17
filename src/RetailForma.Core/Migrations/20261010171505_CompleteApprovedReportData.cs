using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailForma.Core.Migrations
{
    /// <inheritdoc />
    public partial class CompleteApprovedReportData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CategorySnapshot",
                table: "SaleItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVatExemptSnapshot",
                table: "SaleItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LineDiscount",
                table: "SaleItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LineNet",
                table: "SaleItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LineTotal",
                table: "SaleItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LineVat",
                table: "SaleItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxTreatmentSnapshot",
                table: "SaleItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitSnapshot",
                table: "SaleItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedById",
                table: "CashShifts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "CashMovements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                table: "CashMovements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "CashMovements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Destination",
                table: "CashMovements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpenseCategory",
                table: "CashMovements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "CashMovements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DenominationCounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShiftId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FaceValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DenominationCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DenominationCounts_CashShifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "CashShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DenominationCounts_ShiftId_FaceValue",
                table: "DenominationCounts",
                columns: new[] { "ShiftId", "FaceValue" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DenominationCounts");

            migrationBuilder.DropColumn(
                name: "CategorySnapshot",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "IsVatExemptSnapshot",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "LineDiscount",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "LineNet",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "LineTotal",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "LineVat",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "TaxTreatmentSnapshot",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "UnitSnapshot",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ClosedById",
                table: "CashShifts");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "Destination",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "ExpenseCategory",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "CashMovements");
        }
    }
}
