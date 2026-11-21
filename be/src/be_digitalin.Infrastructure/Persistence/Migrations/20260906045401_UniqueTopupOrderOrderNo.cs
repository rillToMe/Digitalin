using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace be_digitalin.Migrations
{
    /// <inheritdoc />
    public partial class UniqueTopupOrderOrderNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TopupOrders_OrderNo",
                table: "TopupOrders");

            migrationBuilder.CreateIndex(
                name: "IX_TopupOrders_OrderNo",
                table: "TopupOrders",
                column: "OrderNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TopupOrders_OrderNo",
                table: "TopupOrders");

            migrationBuilder.CreateIndex(
                name: "IX_TopupOrders_OrderNo",
                table: "TopupOrders",
                column: "OrderNo");
        }
    }
}
