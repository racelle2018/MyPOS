using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyPos.Core.Migrations
{
    /// <inheritdoc />
    public partial class SeniorPwdDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiscountKind",
                table: "Sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SeniorIdNumber",
                table: "Sales",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountKind",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "SeniorIdNumber",
                table: "Sales");
        }
    }
}
