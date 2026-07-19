namespace be_digitalin.Constants;

public static class AppConstants
{
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
    }
    
    // Status Transaksi dari Midtrans
    public static class MidtransStatus
    {
        public const string Settlement = "settlement";
        public const string Capture = "capture";
        public const string Expire = "expire";
        public const string Cancel = "cancel";
        public const string Deny = "deny";
    }

    // Status Fraud dari Midtrans
    public static class MidtransFraud
    {
        public const string Accept = "accept";
    }
    
    //JWT JSON dari Respons Midtrans
    public static class MidtransResponseKeys
    {
        public const string Token = "token";
        public const string RedirectUrl = "redirect_url";
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
        public const string Sn = "sn";
    }
    
    // Kategori Produk
    public static class Categories
    {
        public const string Games = "Games";
        public const string Pulsa = "Pulsa"; 
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
    }

    // Batas ukuran file upload (2 MB)
    public static class FileLimits
    {
        public const long MaxImageBytes = 2 * 1024 * 1024; // 2 MB
    }
}