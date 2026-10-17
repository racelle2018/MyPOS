using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailForma.Core.Migrations
{
    /// <inheritdoc />
    public partial class DrawerAssignmentAndSaleShiftLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CashShiftId",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidCashShiftId",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedCashierId",
                table: "CashShifts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DrawerMode",
                table: "CashShifts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashShiftId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "VoidCashShiftId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "AssignedCashierId",
                table: "CashShifts");

            migrationBuilder.DropColumn(
                name: "DrawerMode",
                table: "CashShifts");
        }
    }
}
