namespace be_digitalin.Constants;

public static class AppConstants
{
    // Versi kontrak API — SSOT. Naik ke "v2" cukup diubah di sini,
    // Program.cs yang memasang prefix ke seluruh endpoint aplikasi.
    // Webhook & endpoint infrastruktur sengaja tidak versioned.
    public static class Api
    {
        public const string Version = "v1";
        public const string Prefix = $"/api/{Version}";
    }

    // Role
    public static class Roles
    {
        public const string SuperAdmin = "SUPER_ADMIN";
        public const string Admin = "ADMIN";
        public const string Customer = "CUSTOMER";
    }
    
    // reCAPTCHA Actions
    public static class RecaptchaActions
    {
        public const string Login = "login";
        public const string RegisterRequestOtp = "register_request_otp";
        public const string AdminLogin = "admin_login";
    }
    
    public static class SystemSettings
    {
        public const string MidtransExpiry = "MIDRANS_EXPIRY";

        /// <summary>Default durasi expiry charge Midtrans (menit) — dipakai bila
        /// setting belum di-set admin atau nilainya tidak terbaca.</summary>
        public const int MidtransExpiryDefaultMinutes = 30;

        /// <summary>Registry kategori storefront (JSON array [{slug,name}], urutan = urutan tampil).</summary>
        public const string GameCategories = "GAME_CATEGORIES";

        /// <summary>Slug brand pilihan admin untuk section Populer, disimpan sebagai JSON array.</summary>
        public const string PopularGames = "POPULAR_GAMES";

        /// <summary>Keys for Maintenance Mode stored in AppSettings table.</summary>
        public static class MaintenanceKeys
        {
            public const string Enabled          = "MAINTENANCE_MODE";
            public const string Title            = "MAINTENANCE_TITLE";
            public const string Message          = "MAINTENANCE_MESSAGE";
            public const string EstimatedFinish  = "MAINTENANCE_ESTIMATED_FINISH";
            public const string AllowAdmin       = "ALLOW_ADMIN_DURING_MAINTENANCE";
        }
    }
    
    public static class SecurityPrefixes
    {
        public const string Sha1 = "sha1=";
    }
    
    // Status Pesanan di Database
    public static class OrderStatus
    {
        public const string Pending = "PENDING";             // User baru buat order, belum bayar
        public const string Paid = "PAID";                   // Uang SUDAH MASUK ke Midtrans
        public const string Processing = "PROCESSING";       // Sedang diproses oleh Digiflazz (tunggu SN)
        public const string Completed = "COMPLETED";         // Selesai! SN sudah keluar
        public const string NeedsAction = "NEEDS_ACTION";    // BAHAYA: Uang masuk, tapi Digiflazz gagal
        public const string Failed = "FAILED";               // Gagal bayar dari Midtrans (cancel/deny)
        public const string Expired = "EXPIRED";             // Waktu bayar habis
        public const string Refunded = "REFUNDED";           // Refund uang kalau transaksi beneran gk bisa di lakukan

        /// <summary>
        /// Status yang berarti uang SUDAH diterima (pembelian benar-benar terjadi).
        /// SSOT satu-satunya untuk "order sudah dibayar" — dipakai mapping
        /// PaymentStatus invoice publik dan penentuan game "Terakhir Dibeli".
        /// Diketik IReadOnlyList (bukan array/HashSet) supaya Contains di
        /// expression tree EF bind ke Enumerable.Contains → diterjemahkan ke
        /// IN clause (array malah bind MemoryExtensions.Contains/span → crash).
        /// </summary>
        public static readonly IReadOnlyList<string> PaidStatuses =
            new[] { Paid, Processing, Completed, NeedsAction };
    }
    
    // Status Transaksi dari Midtrans
    public static class MidtransStatus
    {
        public const string Settlement = "settlement";
        public const string Capture = "capture";
        public const string Pending = "pending";
        public const string Expire = "expire";
        public const string Cancel = "cancel";
        public const string Deny = "deny";
    }

    // Status Fraud dari Midtrans
    public static class MidtransFraud
    {
        public const string Accept = "accept";
    }

    // Metode pembayaran Midtrans Core API yang didukung checkout DIGITALIN.
    // Whitelist = payment_type yang BENAR-BENAR bisa di-charge via Core API.
    // Midtrans tidak menyediakan kanal DANA/LinkAja/OVO — aplikasi tersebut
    // bayar via QRIS (interoperabel). Baris katalog non-whitelist (lihat seed
    // PaymentMethods) ditolak validasi awal "Metode pembayaran tidak didukung".
    public static class MidtransPaymentMethods
    {
        public const string Qris = "qris";
        public const string GoPay = "gopay";
        public const string ShopeePay = "shopeepay";
        public const string BankTransfer = "bank_transfer";
        public const string Cstore = "cstore";

        /// <summary>Toko convenience store yang sah untuk cstore (verifikasi sandbox).</summary>
        public static readonly IReadOnlySet<string> CstoreStores =
            new HashSet<string>(StringComparer.Ordinal)
                { "indomaret", "alfamart" };

        /// <summary>
        /// Midtrans MENGABAIKAN expiry custom untuk cstore — selalu 24 jam
        /// (verifikasi sandbox). Dipakai OrderExpiryPolicy supaya sweep dan
        /// invoice tidak mematikan order yang kode bayarnya masih hidup.
        /// </summary>
        public const int CstoreExpiryMinutes = 24 * 60;

        /// <summary>
        /// Bank yang sah untuk payment_type bank_transfer. "mandiri" adalah
        /// kasus khusus: provider memetakannya ke echannel (Mandiri Bill
        /// Payment) — bukan bank_transfer.bank, yang diam-diam menghasilkan
        /// VA Permata di sisi Midtrans. Bank di luar daftar ini juga
        /// mengalami Permata-fallback diam-diam (danamon/seabank/saqu
        /// terverifikasi) — hanya tambahkan setelah probe membuktikan VA
        /// bank tersebut asli (bsi terverifikasi).
        /// </summary>
        public static readonly IReadOnlySet<string> VaBanks =
            new HashSet<string>(StringComparer.Ordinal)
                { "bca", "bni", "bri", "cimb", "permata", "mandiri", "bsi" };
    }
    
    // JSON dari Respons Midtrans
    public static class MidtransResponseKeys
    {
        // Core API — envelope transaksi
        public const string StatusCode = "status_code";
        public const string StatusMessage = "status_message";
        public const string TransactionId = "transaction_id";
        public const string OrderId = "order_id";
        public const string PaymentType = "payment_type";
        public const string TransactionStatus = "transaction_status";
        public const string FraudStatus = "fraud_status";
        public const string GrossAmount = "gross_amount";
        public const string SettlementTime = "settlement_time";

        // Core API — instruksi pembayaran
        public const string Actions = "actions";
        public const string ActionName = "name";
        public const string ActionUrl = "url";
        public const string QrString = "qr_string";
        public const string VaNumbers = "va_numbers";
        public const string Bank = "bank";
        public const string VaNumber = "va_number";
        public const string PermataVaNumber = "permata_va_number";
        public const string BillKey = "bill_key";
        public const string BillerCode = "biller_code";

        // Core API — cstore (Indomaret/Alfamart)
        public const string PaymentCode = "payment_code";
        public const string Store = "store";
        public const string Message = "message";

        // Core API — envelope
        public const string MerchantId = "merchant_id";
    }

    // Skema Otentikasi HTTP (Header)
    public static class HttpAuthSchemes
    {
        public const string Basic = "Basic";
        public const string Bearer = "Bearer";
    }

    // waktu
    public static class TimeUnits
    {
        public const string Minute = "minute";
        public const string Hour = "hour";
        public const string Day = "day";
    }
    public static class HttpHeaders
    {
        public const string XHubSignature = "x-hub-signature";
    }
    

    // Status Balasan dari Digiflazz
    public static class DigiflazzStatus
    {
        public const string Sukses = "Sukses";
        public const string Gagal = "Gagal";
        public const string Pending = "Pending";
        
    }

    // Command API Digiflazz
    public static class DigiflazzCommands
    {
        public const string Prepaid = "prepaid";
        public const string Pricelist = "pricelist";

        // Cek saldo: nilai cmd-nya "deposit", tapi suffix sign-nya "depo".
        // Kalau keduanya tertukar, Digiflazz menolak dengan "signature tidak sesuai".
        public const string Deposit = "deposit";
        public const string DepositSign = "depo";
    }

    // JTW JSON dari Respons Digiflazz
    public static class DigiflazzResponseKeys
    {
        public const string Data = "data";
        public const string Category = "category";
        public const string BuyerSkuCode = "buyer_sku_code";
        public const string ProductName = "product_name";
        public const string Price = "price";
        public const string Brand = "brand";

        /// <summary>Instruksi supplier per produk (mis. "Format no tujuan [UID]|[Server]").</summary>
        public const string Desc = "desc";

        /// <summary>Supplier menyalakan produk untuk pembeli. false = tidak boleh dijual.</summary>
        public const string BuyerProductStatus = "buyer_product_status";

        /// <summary>Seller menyalakan produknya. false = tidak boleh dijual.</summary>
        public const string SellerProductStatus = "seller_product_status";

        public const string Sn = "sn";
        public const string Deposit = "deposit";
        public const string Message = "message";
    }
    
    // Kategori Produk
    public static class Categories
    {
        public const string Games = "Games";
        public const string Pulsa = "Pulsa";
    }

    // Kategori Storefront (pengelompokan brand di homepage): slug kebab-case → nama tampilan
    public static class StorefrontCategories
    {
        /// <summary>
        /// Registry bawaan, dipakai selama AppSettings["GAME_CATEGORIES"] belum pernah
        /// disimpan admin. Urutan list = urutan tampil di homepage; admin mengubahnya
        /// lewat PUT /admin/system/game-categories.
        /// </summary>
        public static readonly IReadOnlyList<(string Slug, string Name)> Defaults =
        [
            ("mobile", "Mobile Game"),
            ("pc", "PC Game"),
            ("voucher", "Voucher"),
            ("e-money", "E-Money"),
            ("pulsa-data", "Pulsa & Data"),
        ];

        /// <summary>Nama tampilan bawaan per slug — fallback untuk slug di luar registry.</summary>
        public static readonly IReadOnlyDictionary<string, string> Names =
            Defaults.ToDictionary(
                category => category.Slug,
                category => category.Name,
                StringComparer.OrdinalIgnoreCase);

        /// <summary>Bucket virtual "semua kategori" — tab pertama storefront.</summary>
        public const string AllSlug = "all";
        public const string AllName = "Semua";

        /// <summary>Bucket virtual brand yang belum dikategorikan admin.</summary>
        public const string UncategorizedSlug = "lainnya";
        public const string UncategorizedName = "Lainnya";

        /// <summary>Section virtual kurasi admin (GET /games/brands/popular).</summary>
        public const string PopularSlug = "populer";

        /// <summary>Section virtual "Terakhir Dibeli" (GET /games/brands/recent).</summary>
        public const string RecentSlug = "recently";

        /// <summary>
        /// Slug bucket virtual di atas. Kategori dengan slug ini tidak boleh dibuat
        /// admin — akan bertabrakan dan membuat katalog tampil dobel / brand tanpa
        /// kategori tertimpa / section populer tergantikan kategori manual.
        /// </summary>
        public static readonly IReadOnlySet<string> ReservedSlugs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { AllSlug, UncategorizedSlug, PopularSlug, RecentSlug };
    }

    public static class GameInputFieldTypes
    {
        public const string Text = "text";
        public const string Select = "select";
        public const string Number = "number";
    }

    // Key input field standar untuk template game (dipakai di InputFields & TargetTemplate)
    public static class GameFieldKeys
    {
        public const string UserId = "userId";
        public const string PlayerId = "playerId";
        public const string RoleId = "roleId";
        public const string Username = "username";
        public const string RiotId = "riotId";
        public const string IdPlatform = "idPlatform";
        public const string Server = "server";
        public const string Zone = "zone";
    }
    
    // Nama Folder di Cloudflare R2
    public static class StorageFolders
    {
        public const string GameCovers = "icons-cover";
        public const string ItemIcons = "-icons-item";
        public const string Banners = "banners";
        public const string PaymentIcons = "payment-icons";
    }

    // Batas ukuran file upload (2 MB)
    public static class FileLimits
    {
        public const long MaxImageBytes = 2 * 1024 * 1024; // 2 MB
    }
}
