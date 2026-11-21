using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace be_digitalin.Migrations
{
    /// <inheritdoc />
    public partial class AddCstorePayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "PaymentMethods",
                columns: new[] { "Id", "Code", "Description", "DisplayName", "Enabled", "IconUrl", "SortOrder" },
                values: new object[] { 8, "cstore", "Bayar di kasir Indomaret / Alfamart", "Toko Retail", true, null, 8 });

            migrationBuilder.InsertData(
                table: "PaymentBanks",
                columns: new[] { "Id", "Code", "DisplayName", "Enabled", "IconUrl", "PaymentMethodCode", "SortOrder" },
                values: new object[,]
                {
                    { 8, "indomaret", "Indomaret", true, null, "cstore", 1 },
                    { 9, "alfamart", "Alfamart", true, null, "cstore", 2 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PaymentBanks",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "PaymentBanks",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: 8);
        }
    }
}
