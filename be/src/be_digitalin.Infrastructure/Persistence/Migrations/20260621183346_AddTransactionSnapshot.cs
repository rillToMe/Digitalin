using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace be_digitalin.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrandName",
                table: "TopupOrders",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BuyerSkuCode",
                table: "TopupOrders",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CategoryName",
                table: "TopupOrders",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "CostPrice",
                table: "TopupOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "TopupOrders",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPrice",
                table: "TopupOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "TopupOrders",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitAmount",
                table: "TopupOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPaid",
                table: "TopupOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Backfill existing orders with snapshot data from GameProduct
            migrationBuilder.Sql(@"
                UPDATE `TopupOrders` o
                INNER JOIN `GameProducts` p ON o.`GameProductId` = p.`Id`
                SET o.`ProductName` = p.`Name`,
                    o.`BuyerSkuCode` = p.`BuyerSkuCode`,
                    o.`BrandName` = p.`Brand`,
                    o.`CategoryName` = p.`Category`,
                    o.`ImageUrl` = p.`ImageUrl`,
                    o.`OriginalPrice` = p.`Price`,
                    o.`TotalPaid` = p.`Price` - o.`DiscountAmount`,
                    o.`CostPrice` = p.`Price` - COALESCE(p.`CustomMargin`, 0),
                    o.`ProfitAmount` = COALESCE(p.`CustomMargin`, 0)
                WHERE o.`ProductName` = '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrandName",
                table: "TopupOrders");

            migrationBuilder.DropColumn(
                name: "BuyerSkuCode",
                table: "TopupOrders");

            migrationBuilder.DropColumn(
                name: "CategoryName",
                table: "TopupOrders");

            migrationBuilder.DropColumn(
                name: "CostPrice",
                table: "TopupOrders");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "TopupOrders");

            migrationBuilder.DropColumn(
                name: "OriginalPrice",
                table: "TopupOrders");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "TopupOrders");

            migrationBuilder.DropColumn(
                name: "ProfitAmount",
                table: "TopupOrders");

            migrationBuilder.DropColumn(
                name: "TotalPaid",
                table: "TopupOrders");
        }
    }
}
