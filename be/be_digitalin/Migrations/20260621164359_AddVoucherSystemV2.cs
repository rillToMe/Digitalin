using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace be_digitalin.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherSystemV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename columns
            migrationBuilder.RenameColumn(
                name: "ValidUntil",
                table: "Vouchers",
                newName: "ExpiredAt");

            migrationBuilder.RenameColumn(
                name: "MinTransactionAmount",
                table: "Vouchers",
                newName: "MinimumTransaction");

            migrationBuilder.RenameColumn(
                name: "MaxDiscountAmount",
                table: "Vouchers",
                newName: "MaximumDiscount");

            // Add new columns
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Vouchers",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Vouchers",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DiscountType",
                table: "Vouchers",
                type: "longtext",
                nullable: false,
                defaultValue: "Percentage")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "Vouchers",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "UsedCount",
                table: "Vouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UsageLimit",
                table: "Vouchers",
                type: "int",
                nullable: false,
                defaultValue: 100);

            // Data migration: copy DiscountPercentage → DiscountValue, Quota → UsageLimit, UsedCount = 0
            migrationBuilder.Sql(
                "UPDATE Vouchers SET DiscountValue = DiscountPercentage, DiscountType = 'Percentage', UsageLimit = Quota, UsedCount = 0, CreatedAt = NOW(), UpdatedAt = NOW()");

            // Drop old columns
            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "Quota",
                table: "Vouchers");

            // Unique index on Code
            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Vouchers",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Code",
                table: "Vouchers",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vouchers_Code",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "UsedCount",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "UsageLimit",
                table: "Vouchers");

            migrationBuilder.RenameColumn(
                name: "ExpiredAt",
                table: "Vouchers",
                newName: "ValidUntil");

            migrationBuilder.RenameColumn(
                name: "MinimumTransaction",
                table: "Vouchers",
                newName: "MinTransactionAmount");

            migrationBuilder.RenameColumn(
                name: "MaximumDiscount",
                table: "Vouchers",
                newName: "MaxDiscountAmount");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Vouchers",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "Vouchers",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Quota",
                table: "Vouchers",
                type: "int",
                nullable: false,
                defaultValue: 100);
        }
    }
}
