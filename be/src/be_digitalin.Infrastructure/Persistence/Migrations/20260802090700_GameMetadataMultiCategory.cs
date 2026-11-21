using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace be_digitalin.Migrations
{
    /// <inheritdoc />
    public partial class GameMetadataMultiCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Satu game boleh punya beberapa kategori: kolomnya jadi CSV slug.
            // Rename (bukan drop + add) supaya kategori yang sudah diisi admin
            // tetap ada — nilai lama "mobile" valid sebagai CSV satu elemen.
            migrationBuilder.RenameColumn(
                name: "Category",
                table: "GameMetadata",
                newName: "Categories");

            migrationBuilder.AlterColumn<string>(
                name: "Categories",
                table: "GameMetadata",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Balik ke satu kategori: CSV lebih dari 50 char akan terpotong,
            // dan game multi-kategori kehilangan kategori keduanya.
            migrationBuilder.AlterColumn<string>(
                name: "Categories",
                table: "GameMetadata",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.RenameColumn(
                name: "Categories",
                table: "GameMetadata",
                newName: "Category");
        }
    }
}
