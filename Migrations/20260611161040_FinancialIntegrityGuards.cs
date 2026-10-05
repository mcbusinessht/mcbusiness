using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MCBusiness.Migrations
{
    /// <inheritdoc />
    public partial class FinancialIntegrityGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_wallets_userid",
                table: "Wallets");

            migrationBuilder.DropIndex(
                name: "IX_SellerPayments_OrderId",
                table: "SellerPayments");

            migrationBuilder.CreateIndex(
                name: "ux_wallets_user_currency",
                table: "Wallets",
                columns: new[] { "UserId", "CurrencyBase" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_Transaction_ExternalId",
                table: "Transactions",
                column: "TransactionId",
                unique: true,
                filter: "\"TransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_SellerPayment_Order_Company",
                table: "SellerPayments",
                columns: new[] { "OrderId", "CompanyId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "ux_PlatformTransaction_External_Category",
                table: "PlatformTransactions",
                columns: new[] { "ExternalTransactionId", "Category" },
                unique: true,
                filter: "\"ExternalTransactionId\" IS NOT NULL AND \"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_wallets_user_currency",
                table: "Wallets");

            migrationBuilder.DropIndex(
                name: "ux_Transaction_ExternalId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "ux_SellerPayment_Order_Company",
                table: "SellerPayments");

            migrationBuilder.DropIndex(
                name: "ux_PlatformTransaction_External_Category",
                table: "PlatformTransactions");

            migrationBuilder.CreateIndex(
                name: "idx_wallets_userid",
                table: "Wallets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerPayments_OrderId",
                table: "SellerPayments",
                column: "OrderId");
        }
    }
}
