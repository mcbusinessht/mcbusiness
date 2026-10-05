using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MCBusiness.Models;

#nullable disable

namespace MCBusiness.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260701120000_DropWalletNonNegativeConstraint")]
    public partial class DropWalletNonNegativeConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Wallets"
                DROP CONSTRAINT IF EXISTS "CK_Wallet_Balance_NonNegative";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Wallets"
                ADD CONSTRAINT "CK_Wallet_Balance_NonNegative"
                CHECK ("Balance" >= 0) NOT VALID;
                """);
        }
    }
}
