using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace be_digitalin.Migrations
{
    /// <inheritdoc />
    public partial class AddShopeePayAndEwalletCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: 3,
                column: "SortOrder",
                value: 4);

            migrationBuilder.InsertData(
                table: "PaymentMethods",
                columns: new[] { "Id", "Code", "Description", "DisplayName", "Enabled", "IconUrl", "SortOrder" },
                values: new object[,]
                {
                    { 4, "shopeepay", "Langsung buka aplikasi Shopee", "ShopeePay", true, null, 3 },
                    { 5, "dana", "Langsung buka aplikasi DANA", "DANA", false, null, 5 },
                    { 6, "linkaja", "Langsung buka aplikasi LinkAja", "LinkAja", false, null, 6 },
                    { 7, "ovo", "Langsung buka aplikasi OVO", "OVO", false, null, 7 }
                });

            // Data repair: re-seed bank VA. Migration sebelumnya yang cacat
            // (AddShopeePayAndEwalletCatalog v1, sudah di-rollback) menghapus
            // PaymentMethods Id=3 yang cascade ke seluruh PaymentBanks lewat
            // FK Code — baris bank hilang dari DB dan tidak diketahui diff
            // model (seed bank tidak berubah). Insert eksplisit di sini.
            migrationBuilder.InsertData(
                table: "PaymentBanks",
                columns: new[] { "Id", "Code", "DisplayName", "Enabled", "IconUrl", "PaymentMethodCode", "SortOrder" },
                values: new object[,]
                {
                    { 1, "bca", "BCA", true, null, "bank_transfer", 1 },
                    { 2, "bni", "BNI", true, null, "bank_transfer", 2 },
                    { 3, "bri", "BRI", true, null, "bank_transfer", 3 },
                    { 4, "cimb", "CIMB Niaga", true, null, "bank_transfer", 4 },
                    { 5, "permata", "Permata", true, null, "bank_transfer", 5 },
                    { 6, "mandiri", "Mandiri", false, null, "bank_transfer", 6 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PaymentBanks",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "PaymentBanks",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "PaymentBanks",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "PaymentBanks",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "PaymentBanks",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "PaymentBanks",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.UpdateData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: 3,
                column: "SortOrder",
                value: 3);
        }
    }
}
