using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace be_digitalin.Migrations
{
    /// <inheritdoc />
    public partial class AddFaqCms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Faqs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Question = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Answer = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Featured = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faqs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Faqs",
                columns: new[] { "Id", "Answer", "Category", "Featured", "Question" },
                values: new object[,]
                {
                    { 1, "Pilih game yang ingin di-top up, masukkan Player ID, pilih nominal, lalu lakukan pembayaran. Proses otomatis setelah pembayaran berhasil.", "Top Up", true, "Bagaimana cara melakukan top up game?" },
                    { 2, "Proses top up umumnya instan (1-5 menit) setelah pembayaran terkonfirmasi. Beberapa produk mungkin membutuhkan waktu hingga 15 menit.", "Top Up", true, "Berapa lama proses top up?" },
                    { 3, "Tidak wajib. Anda bisa melakukan top up sebagai guest. Namun, dengan akun Anda bisa melihat riwayat transaksi.", "Top Up", false, "Apakah perlu membuat akun untuk top up?" },
                    { 4, "Jika sudah dibayar, top up tidak dapat dibatalkan. Pastikan Player ID benar sebelum melakukan pembayaran.", "Top Up", true, "Player ID salah, bisa dibatalkan?" },
                    { 5, "Kami mendukung berbagai metode pembayaran melalui Midtrans: GoPay, OVO, ShopeePay, transfer bank, kartu kredit, dan convenience store.", "Pembayaran", true, "Metode pembayaran apa saja yang didukung?" },
                    { 6, "Ya. Seluruh transaksi diproses melalui Midtrans yang sudah terenkripsi dan tersertifikasi PCI DSS.", "Pembayaran", false, "Apakah pembayaran aman?" },
                    { 7, "Jika pembayaran gagal, Anda bisa mencoba kembali. Tidak ada biaya yang dikenakan untuk transaksi yang gagal.", "Pembayaran", false, "Bagaimana jika pembayaran gagal?" },
                    { 8, "Klik tombol 'Daftar' di navbar, masukkan email dan password, lalu verifikasi dengan kode OTP yang dikirim ke email Anda.", "Akun", false, "Bagaimana cara mendaftar?" },
                    { 9, "Ya. Kami mendukung login menggunakan akun Google untuk kemudahan akses.", "Akun", false, "Apakah bisa login dengan Google?" },
                    { 10, "Masuk ke halaman Profile, lalu pilih opsi ubah password. Masukkan password baru dan simpan.", "Akun", false, "Bagaimana cara mengubah password?" },
                    { 11, "Masuk ke halaman Riwayat Order (jika memiliki akun) untuk melihat status seluruh transaksi Anda.", "Transaksi", true, "Bagaimana cara melihat riwayat transaksi?" },
                    { 12, "Tunggu 5-15 menit. Jika masih belum masuk, hubungi kami melalui halaman Kontak dengan menyertakan nomor invoice.", "Transaksi", true, "Diamond belum masuk setelah pembayaran berhasil?" },
                    { 13, "Status PENDING berarti pembayaran belum terkonfirmasi. Selesaikan pembayaran Anda agar pesanan diproses.", "Transaksi", false, "Apa arti status PENDING pada pesanan?" },
                    { 14, "Masukkan kode voucher pada kolom yang tersedia saat melakukan checkout. Diskon akan otomatis diterapkan.", "Voucher", false, "Bagaimana cara menggunakan kode voucher?" },
                    { 15, "Pastikan kode voucher masih aktif, belum kedaluwarsa, dan memenuhi minimum transaksi. Jika masih bermasalah, hubungi support.", "Voucher", false, "Voucher tidak bisa digunakan?" },
                    { 16, "Ya. Digitalin menggunakan enkripsi SSL, pembayaran diproses via Midtrans (PCI DSS certified), dan data Anda dilindungi.", "Lainnya", false, "Apakah Digitalin aman?" },
                    { 17, "Refund hanya dapat dilakukan jika pesanan gagal diproses oleh sistem. Hubungi tim support kami untuk pengajuan refund.", "Lainnya", false, "Apakah bisa refund?" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Faqs");
        }
    }
}
