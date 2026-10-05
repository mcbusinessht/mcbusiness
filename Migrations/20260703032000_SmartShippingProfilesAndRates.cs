using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MCBusiness.Models;

#nullable disable

namespace MCBusiness.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260703032000_SmartShippingProfilesAndRates")]
    public partial class SmartShippingProfilesAndRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShippingPackageType",
                table: "ProductVariants",
                type: "text",
                nullable: false,
                defaultValue: "Small");

            migrationBuilder.AddColumn<decimal>(
                name: "WeightKg",
                table: "ProductVariants",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresSpecialHandling",
                table: "ProductVariants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShippingQuoteRequired",
                table: "ProductVariants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PackageType",
                table: "ShippingFees",
                type: "text",
                nullable: false,
                defaultValue: "Any");

            migrationBuilder.AddColumn<decimal>(
                name: "BasePrice",
                table: "ShippingFees",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(@"
                UPDATE ""ShippingFees""
                SET ""BasePrice"" = ""Price""
                WHERE ""BasePrice"" = 0;
            ");

            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalItemPrice",
                table: "ShippingFees",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalUnitPrice",
                table: "ShippingFees",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerKg",
                table: "ShippingFees",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinPrice",
                table: "ShippingFees",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxPrice",
                table: "ShippingFees",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QuoteRequired",
                table: "ShippingFees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ShippingFees",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ShippingPackageType", table: "ProductVariants");
            migrationBuilder.DropColumn(name: "WeightKg", table: "ProductVariants");
            migrationBuilder.DropColumn(name: "RequiresSpecialHandling", table: "ProductVariants");
            migrationBuilder.DropColumn(name: "ShippingQuoteRequired", table: "ProductVariants");

            migrationBuilder.DropColumn(name: "PackageType", table: "ShippingFees");
            migrationBuilder.DropColumn(name: "BasePrice", table: "ShippingFees");
            migrationBuilder.DropColumn(name: "AdditionalItemPrice", table: "ShippingFees");
            migrationBuilder.DropColumn(name: "AdditionalUnitPrice", table: "ShippingFees");
            migrationBuilder.DropColumn(name: "PricePerKg", table: "ShippingFees");
            migrationBuilder.DropColumn(name: "MinPrice", table: "ShippingFees");
            migrationBuilder.DropColumn(name: "MaxPrice", table: "ShippingFees");
            migrationBuilder.DropColumn(name: "QuoteRequired", table: "ShippingFees");
            migrationBuilder.DropColumn(name: "IsActive", table: "ShippingFees");
        }
    }
}
