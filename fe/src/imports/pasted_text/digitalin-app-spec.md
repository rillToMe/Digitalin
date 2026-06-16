# PROJECT
Buatkan aplikasi frontend Top Up Game bernama "Digitalin" menggunakan React + TypeScript + Vite + Tailwind.

---

# TECH STACK

- React
- TypeScript
- Vite
- React Router DOM
- Axios
- TanStack Query (React Query)
- Zustand 
- TailwindCSS 
- Responsive Design (Mobile First)

---

# API CONFIGURATION

Gunakan environment variable:
"Content-Type": "application/json",
.env

VITE_API_BASE_URL=http://localhost:5210

---

# API STRUCTURE

Semua type wajib menggunakan file:

src/api/index.ts

JANGAN membuat ulang type yang sudah ada.

Gunakan type yang tersedia pada file tersebut untuk:
- Auth
- Brand
- Product
- Order
- Banner
- Dashboard
- Metadata
- Pagination

Referensi type berasal dari:
:contentReference[oaicite:0]{index=0}

---

# SERVICE LAYER

Buat service API terpisah:

src/services/api.ts

Contoh:

- authService
- brandService
- orderService
- profileService
- bannerService
- adminService

Axios configuration:

- baseURL = import.meta.env.VITE_API_BASE_URL
- request interceptor untuk token
- response interceptor untuk error handling

---

# STATE MANAGEMENT

Gunakan Zustand untuk:

- Authentication State
- User Session
- Cart / Checkout State
- Selected Product
- Selected Payment Method

Jangan gunakan Context API untuk global state.


# CHECKOUT FLOW

Urutan validasi:

1. User memilih produk
2. User mengisi Player ID
3. User memilih payment method
4. User klik Bayar

Jika ada field kosong:
- tampilkan error toast

Jika request berhasil:
- redirect ke payment page

Gunakan:
CreateOrderPayload
CreateOrderResponse

# ROUTES

Customer:

/
 /game/:slug
 /login
 /register
 /verify-otp
 /profile
 /history

Admin:

/admin/login
/admin/dashboard
/admin/products
/admin/banners
/admin/metadata

# CHECKOUT FLOW

Urutan validasi:

1. User memilih produk
2. User mengisi Player ID
3. User memilih payment method
4. User klik Bayar

Jika ada field kosong:
- button disable dan ada keluar popup yang kosong

Jika request berhasil:
- redirect ke payment page

Gunakan:
CreateOrderPayload
CreateOrderResponse

# ADMIN UI

AdminLayout harus memiliki:

- Sidebar
- Header
- Breadcrumb
- Logout Button

Sidebar Menu:

- Dashboard
- Products
- Banners
- Metadata

# UI COMPONENTS

Gunakan:
- shadcn/ui
- lucide-react

Untuk:
- Dialog
- Dropdown Menu
- Tabs
- Sheet
- Toast
- Form Components

# COLOR SYSTEM

Gunakan CSS Variables.

JANGAN HARDCODE warna di component.

Buat global theme:

:root {
  --color-primary: #0F1419;
  --color-secondary: #4A5568;
  --color-tertiary: #2C5EF5;
  --color-neutral: #F1F3F5;
  --color-surface: #FFFFFF;
  --color-on-primary: #FFFFFF;
}

Semua warna wajib menggunakan variable tersebut.

Contoh:

background-color: var(--color-primary);

DILARANG:

background-color: #0F1419;

gunakan: font-family: "Inter", sans-serif;

---

# DESIGN STYLE

Gunakan desain seperti:

- Tokopedia Digital Product
- UniPin
- Codashop versi modern
- Steam Store minimal

Hindari:
- Neon Gaming Style
- Cyberpunk Style
- RGB Effects
- Over Animation

Fokus:
- Clean
- Fast
- Professional
- Trustworthy

Font:
font-family: "Inter", sans-serif;

---

# ROLE SEPARATION

Pisahkan folder:

src/modules/customer

src/modules/admin

src/layout/AdminLayout.tsx

src/layout/Customerlayout.tsx

Tidak boleh mencampur halaman admin dan user.

---

# USER FEATURES

## Navbar

Navbar harus berisi:

- Logo / Nama Toko: Digitalin
- Search game (optional)
- Avatar User

Jika user belum login:

- Tombol Login
- Tombol Register

Jika user sudah login:

Ketika avatar diklik muncul dropdown:

- Profile
- History Order
- Logout

Dropdown harus memiliki animation.

---

# HOMEPAGE

## Hero Carousel

Buat animated carousel/banner.

Data berasal dari endpoint Banner.

Fitur:

- Auto slide
- Manual navigation
- Smooth animation
- Responsive

---

## Game Categories

Tampilkan kategori:

- Popular
- Mobile Game
- PC Game
- Lainnya

Gunakan tab atau horizontal scroll.

---

## Game Grid

Tampilkan daftar game:

Card harus berisi:

- Logo Game
- Nama Game
- Kategori
- Harga mulai dari

Hover Effect:

- Scale
- Shadow
- Smooth transition

---

# GAME DETAIL PAGE

Ketika game dipilih masuk ke halaman detail.

---

## Informasi Game

Tampilkan:

- Logo Game
- Nama Game
- Banner Game

---

## Player Information Form

Form harus dynamic berdasarkan metadata API.

Gunakan InputType dari API.

Tipe:

### USER_ID_ONLY

Tampilkan:

- Player ID

### USER_ID_AND_ZONE_ID

Tampilkan:

- Player ID
- Zone ID

---

## Dynamic Server Selector

Jika metadata memiliki:

serverList

Maka tampilkan:

Select Server

Contoh:

- Asia
- Europe
- Amerika
- H-K-M

Jika null maka component tidak muncul.

---

## Product Selection

Tampilkan semua nominal diamond.

Gunakan data ProductItem.

Card nominal berisi:

- Nama produk
- Harga
- Selected state

Ketika dipilih:

- Border aktif
- Background aktif
- Check icon

Hanya boleh memilih satu nominal.

---

## Payment Method

Section metode pembayaran.

Payment Method berasal dari API.

Jika endpoint belum tersedia,
buat mock data sementara yang mudah diganti nanti.

Gunakan card selector.

Hanya satu metode pembayaran aktif.

---

## Order Summary

Tampilkan:

- Nama Game
- Player ID
- Server
- Produk
- Harga
- Total

Sticky pada desktop.

---

## Pay Button

Button:

"Bayar Sekarang"

Requirement:

- Hanya bisa diklik 1 kali
- Setelah klik:
  - disabled = true
  - loading state muncul
  - spinner muncul
- Mencegah duplicate order
- Re-enable hanya jika request gagal

Contoh:

disabled={isSubmitting}

---

# HISTORY PAGE

Tampilkan daftar order user.

Gunakan:

PaginatedOrdersResponse

Data:

- Order Number
- Product
- Status
- Date
- Price

Status menggunakan badge warna.

---

# PROFILE PAGE

Gunakan:

ProfileResponse

Tampilkan:

- Full Name
- Email
- Created At

Edit profile:

UpdateProfilePayload

---

# AUTH PAGES

## Login

Gunakan:

LoginPayload

## Register

Gunakan:

RegisterPayload

## OTP Verification

Gunakan:

RequestOtpPayload

---

# ADMIN PANEL

Folder:

src/modules/admin

---

## Admin Login

Gunakan:

AdminLoginPayload

---

## Dashboard

Gunakan:

DashboardStatsResponse

Tampilkan:

- Revenue Today
- Profit Today
- Success Orders
- Pending Orders
- Failed Orders

Tambahkan chart:

ChartResponse

---

## Product Management

Gunakan:

AdminProductResponse
AdminProductsListResponse
CreateProductPayload

Fitur:

- Create
- Update
- Delete
- Search
- Pagination

---

## Banner Management

Gunakan:

BannerItem
CreateBannerResponse

Fitur:

- Upload Banner
- Enable / Disable
- Delete Banner

---

## Metadata Management

Gunakan:

GameMetadataItem
UpdateMetadataPayload

Fitur:

- Ubah Input Type
- Ubah Server List

---

# FOLDER STRUCTURE

src/
├── api/
│   └── index.ts
│
├── services/
│   └── api.ts
│
├── components/
│   ├── common/
│   ├── ui/
│   └── forms/
│
├── layouts/
│
├── modules/
│   ├── customer/
│   │   ├── pages/
│   │   ├── components/
│   │   └── hooks/
│   │
│   └── admin/
│       ├── pages/
│       ├── components/
│       └── hooks/
│
├── routes/
│
├── store/
│
├── styles/
│   ├── variables.css
│   └── globals.css
│
└── App.tsx

---

# UI REQUIREMENTS

- Modern
- Clean
- Responsive Mobile First
- Smooth Animations
- Skeleton Loading
- Empty State
- Error State
- Accessibility Friendly

---

# IMPORTANT RULES

1. Jangan gunakan any.
2. Semua type harus berasal dari src/api/index.ts.
3. Semua warna harus menggunakan CSS Variables.
4. Pisahkan user dan admin.
5. Gunakan reusable components.
6. Gunakan React Query untuk data fetching.
7. Gunakan Axios Service Layer.
8. Tombol bayar wajib anti double click.
9. Semua halaman harus responsive.
10. Gunakan TypeScript strict mode.

# CODE STANDARDS

- TypeScript Strict Mode
- No any
- No eslint disable
- No hardcoded API URL
- No duplicated types
- No duplicated components
- Reusable hooks
- Reusable UI Components
- Feature-based architecture

