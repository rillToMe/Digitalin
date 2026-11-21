using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace be_digitalin.Migrations
{
    /// <inheritdoc />
    public partial class GameMetadataBrandSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrandSlug",
                table: "GameMetadata",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Backfill dengan aturan yang sama seperti Domain.Slug.From: non
            // alfanumerik jadi hyphen, hyphen di ujung dibuang. Harus jalan
            // sebelum unique index dibuat, kalau tidak semua baris lama masih
            // '' dan indexnya langsung bentrok.
            migrationBuilder.Sql(
                "UPDATE `GameMetadata` " +
                "SET `BrandSlug` = TRIM(BOTH '-' FROM " +
                "LOWER(REGEXP_REPLACE(`Brand`, '[^a-zA-Z0-9]+', '-')))");

            migrationBuilder.CreateIndex(
                name: "IX_GameMetadata_BrandSlug",
                table: "GameMetadata",
                column: "BrandSlug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameMetadata_BrandSlug",
                table: "GameMetadata");

            migrationBuilder.DropColumn(
                name: "BrandSlug",
                table: "GameMetadata");
        }
    }
}
