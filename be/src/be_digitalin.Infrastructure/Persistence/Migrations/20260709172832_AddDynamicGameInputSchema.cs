using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace be_digitalin.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicGameInputSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Brand",
                table: "GameMetadata",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                    name: "InputSchemaJson",
                    table: "GameMetadata",
                    type: "longtext",
                    nullable: false,
                    defaultValue: "[]")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TargetTemplate",
                table: "GameMetadata",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "{userId}")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_GameMetadata_Brand",
                table: "GameMetadata",
                column: "Brand",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameMetadata_Brand",
                table: "GameMetadata");

            migrationBuilder.DropColumn(
                name: "InputSchemaJson",
                table: "GameMetadata");

            migrationBuilder.DropColumn(
                name: "TargetTemplate",
                table: "GameMetadata");

            migrationBuilder.AlterColumn<string>(
                name: "Brand",
                table: "GameMetadata",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
