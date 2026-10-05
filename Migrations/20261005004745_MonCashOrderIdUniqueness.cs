using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MCBusiness.Migrations
{
    /// <inheritdoc />
    public partial class MonCashOrderIdUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_Transaction_OrderId",
                table: "Transactions",
                column: "OrderId",
                unique: true,
                filter: "\"OrderId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_Transaction_OrderId",
                table: "Transactions");
        }
    }
}
