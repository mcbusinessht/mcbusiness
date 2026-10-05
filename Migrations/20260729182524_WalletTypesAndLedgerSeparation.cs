using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MCBusiness.Migrations
{
    /// <inheritdoc />
    public partial class WalletTypesAndLedgerSeparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_wallets_user_currency",
                table: "Wallets");

            migrationBuilder.AddColumn<string>(
                name: "WalletType",
                table: "WalletTransactions",
                type: "text",
                nullable: false,
                defaultValue: "Customer");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Wallets",
                type: "text",
                nullable: false,
                defaultValue: "Customer");

            migrationBuilder.AddColumn<int>(
                name: "WalletId",
                table: "Transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WalletType",
                table: "Transactions",
                type: "text",
                nullable: false,
                defaultValue: "Customer");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "ShippingFees",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<int>(
                name: "WalletId",
                table: "BankTransferPayments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WalletType",
                table: "BankTransferPayments",
                type: "text",
                nullable: false,
                defaultValue: "Customer");

            migrationBuilder.AddColumn<int>(
                name: "WalletId",
                table: "BalanceReservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WalletType",
                table: "BalanceReservations",
                type: "text",
                nullable: false,
                defaultValue: "Customer");

            migrationBuilder.CreateIndex(
                name: "idx_Wallet_Type",
                table: "Wallets",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "ux_wallets_user_currency_type",
                table: "Wallets",
                columns: new[] { "UserId", "CurrencyBase", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_WalletTransaction_WalletType",
                table: "WalletTransactions",
                column: "WalletType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_Wallet_Type",
                table: "Wallets");

            migrationBuilder.DropIndex(
                name: "ux_wallets_user_currency_type",
                table: "Wallets");

            migrationBuilder.DropIndex(
                name: "idx_WalletTransaction_WalletType",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "WalletType",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "WalletType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "BankTransferPayments");

            migrationBuilder.DropColumn(
                name: "WalletType",
                table: "BankTransferPayments");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "BalanceReservations");

            migrationBuilder.DropColumn(
                name: "WalletType",
                table: "BalanceReservations");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "ShippingFees",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.CreateIndex(
                name: "ux_wallets_user_currency",
                table: "Wallets",
                columns: new[] { "UserId", "CurrencyBase" },
                unique: true);
        }
    }
}
