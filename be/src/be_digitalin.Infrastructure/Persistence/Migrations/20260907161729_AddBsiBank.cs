using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace be_digitalin.Migrations
{
    /// <inheritdoc />
    public partial class AddBsiBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "PaymentBanks",
                columns: new[] { "Id", "Code", "DisplayName", "Enabled", "IconUrl", "PaymentMethodCode", "SortOrder" },
                values: new object[] { 7, "bsi", "BSI", false, null, "bank_transfer", 7 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PaymentBanks",
                keyColumn: "Id",
                keyValue: 7);
        }
    }
}
