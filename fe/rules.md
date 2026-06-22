# Digitalin Frontend Development Rules

Dokumen ini berisi aturan yang WAJIB dipatuhi sebelum melakukan implementasi apa pun pada project Digitalin.

Aturan ini berlaku untuk seluruh fitur, baik Customer maupun Admin.

Pelanggaran terhadap aturan ini dianggap sebagai implementasi yang tidak sesuai dengan standar project.

---

# 1. WAJIB MEMBACA API_CONTRACT.md

Sebelum menulis kode apa pun yang berhubungan dengan API:

WAJIB membaca:

API_CONTRACT.md

API Contract adalah **Single Source of Truth**.

Jangan pernah:

* mengira struktur request
* mengira response
* mengira endpoint
* mengira business rules

Apabila endpoint belum ada di API Contract:

STOP IMPLEMENTASI.

Jangan membuat endpoint sendiri.

Laporkan bahwa API Contract belum mendukung fitur tersebut.

---

# 2. JANGAN MEMBUAT ENDPOINT SENDIRI

Jika endpoint tidak ditemukan pada API_CONTRACT.md:

❌ Jangan membuat endpoint.

❌ Jangan membuat URL sendiri.

❌ Jangan membuat request dummy.

❌ Jangan membuat mock API.

Tunggu API Contract diperbarui.

---

# 3. SELURUH TYPE BERASAL DARI src/api/index.ts

Jangan membuat interface lokal apabila type sudah tersedia.

WAJIB menggunakan:

src/api/index.ts

Apabila API Contract berubah:

Perbarui src/api/index.ts terlebih dahulu.

Baru implementasikan fitur.

---

# 4. GUNAKAN SERVICE LAYER

Semua komunikasi API harus melalui:

src/services/api.ts

atau service khusus yang mengikuti arsitektur project.

Jangan:

* fetch()
* axios.create()
* axios langsung di component

Component tidak boleh mengetahui detail implementasi API.

---

# 5. DILARANG MENGGUNAKAN any

Project menggunakan TypeScript Strict.

Jangan menggunakan:

any

Gunakan type yang benar.

---

# 6. DILARANG HARDCODE WARNA

Semua warna harus berasal dari:

src/styles/globals.css

Gunakan CSS Variables.

Contoh:

var(--color-primary)

Jangan menggunakan:

* #FFFFFF
* #000000
* #ef4444
* rgba(...)
* rgb(...)
* hsl(...)

atau warna hardcoded lainnya.

---

# 7. COMPONENT TIDAK BOLEH MENGETAHUI WARNA

Component hanya bertanggung jawab terhadap:

* Layout
* Structure
* Behavior

Design System menentukan:

* Color
* Border
* Shadow
* Background
* Overlay
* Hover
* Skeleton
* Status

---

# 8. IKUTI ARSITEKTUR PROJECT

Sebelum membuat file baru:

Pelajari struktur project terlebih dahulu.

Ikuti pola yang sudah digunakan.

Jangan membuat struktur baru apabila sudah ada pola yang konsisten.

---

# 9. GUNAKAN REUSABLE COMPONENT

Jika project sudah memiliki:

* Button
* Table
* Modal
* Badge
* Pagination
* Dialog
* Search
* Form
* Input

Gunakan kembali.

Jangan membuat komponen yang sama dengan nama berbeda.

---

# 10. PISAHKAN TANGGUNG JAWAB

Jangan membuat file besar yang mencampurkan:

* UI
* API
* Business Logic
* State
* Utility

Pisahkan menjadi:

* pages
* components
* hooks
* services
* utils

sesuai arsitektur project.

---

# 11. JANGAN MERUSAK FITUR YANG SUDAH ADA

Sebelum mengubah implementasi:

Pastikan perubahan tidak menyebabkan regression.

Periksa seluruh flow yang berkaitan.

---

# 12. MOBILE FIRST

Seluruh fitur harus bekerja pada:

* Desktop
* Tablet
* Mobile

Jangan hanya menguji Desktop.

---

# 13. RESPONSIVE

Periksa:

* overflow
* spacing
* typography
* touch target
* scrolling

---

# 14. ACCESSIBILITY

Perhatikan:

* keyboard navigation
* focus state
* aria-label
* semantic HTML

---

# 15. REACT QUERY

Gunakan TanStack Query untuk seluruh data fetching.

Jangan mengelola asynchronous request secara manual apabila sudah ada pola React Query.

---

# 16. JANGAN DUPLIKASI LOGIC

Apabila terdapat logic yang dapat digunakan kembali:

Pindahkan ke:

* hooks
* utils
* reusable component

---

# 17. CLEAN CODE

Kode harus:

* mudah dibaca
* mudah dipelihara
* mudah dikembangkan

Hindari nested code yang berlebihan.

Gunakan penamaan yang konsisten.

---

# 18. FUTURE READY

Saat membuat fitur,

pertimbangkan kemungkinan pengembangan di masa depan.

Jangan membuat implementasi yang menyulitkan scaling.

---

# 19. OUTPUT SETELAH IMPLEMENTASI

Setelah setiap task selesai,

WAJIB memberikan laporan:

* File yang ditambahkan.
* File yang diubah.
* Alasan perubahan.
* Dampak terhadap fitur lain.
* Potensi breaking changes (jika ada).

---

# 20. PRINSIP UTAMA

Sebelum mulai coding, selalu lakukan urutan berikut:

1. Baca RULES.txt
2. Baca API_CONTRACT.md
3. Analisis struktur project
4. Cari reusable component yang sudah ada
5. Implementasikan sesuai arsitektur
6. Hindari duplikasi
7. Pastikan responsive
8. Pastikan tidak ada hardcoded color
9. Pastikan seluruh type berasal dari src/api/index.ts
10. Berikan laporan implementasi setelah selesai.
