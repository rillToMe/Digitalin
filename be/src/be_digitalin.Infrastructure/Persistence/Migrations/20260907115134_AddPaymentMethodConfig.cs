using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace be_digitalin.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMethodConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IconUrl = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                    table.UniqueConstraint("AK_PaymentMethods_Code", x => x.Code);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PaymentBanks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IconUrl = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    PaymentMethodCode = table.Column<string>(type: "varchar(50)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentBanks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentBanks_PaymentMethods_PaymentMethodCode",
                        column: x => x.PaymentMethodCode,
                        principalTable: "PaymentMethods",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "PaymentMethods",
                columns: new[] { "Id", "Code", "Description", "DisplayName", "Enabled", "IconUrl", "SortOrder" },
                values: new object[,]
                {
                    { 1, "qris", "Scan dari semua e-wallet & m-banking", "QRIS", true, null, 1 },
                    { 2, "gopay", "Langsung buka aplikasi Gojek", "GoPay", true, null, 2 },
                    { 3, "bank_transfer", "Virtual Account otomatis", "Transfer Bank", true, null, 3 }
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBanks_Code",
                table: "PaymentBanks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBanks_PaymentMethodCode_Code",
                table: "PaymentBanks",
                columns: new[] { "PaymentMethodCode", "Code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentBanks");

            migrationBuilder.DropTable(
                name: "PaymentMethods");
        }
    }
}
