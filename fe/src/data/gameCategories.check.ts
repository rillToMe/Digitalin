/**
 * Self-check: aturan paginasi fetch per section HomePage + helper UI kategori.
 * Deret offset/limit harus menutup brandCount tepat sekali — kalau salah,
 * ada game yang tidak pernah tampil atau tombol "lebih banyak" tak pernah habis,
 * dua-duanya gagal sunyi.
 *   npx tsx src/data/gameCategories.check.ts
 */
import assert from "node:assert/strict";
import {
  CATEGORY_ICONS,
  CATEGORY_NAMES,
  INITIAL_FETCH_LIMIT,
  INITIAL_FETCH_LIMIT_UNCATEGORIZED,
  MORE_FETCH_LIMIT,
  POPULAR_CATEGORY_SLUG,
  UNCATEGORIZED_CATEGORY_SLUG,
  categoryIcon,
  categorySlugPreview,
  initialFetchLimit,
  nextBrandOffset,
} from "./gameCategories";

/** Batas atas ?limit di BE (ProductEndpoints.MaxBrandPageSize). */
const SERVER_MAX_LIMIT = 60;

// Batch awal harus lebih kecil dari batch "lebih banyak" — inti optimasinya:
// load pertama ringan, klik berikutnya boleh lebih berat.
assert.ok(INITIAL_FETCH_LIMIT < MORE_FETCH_LIMIT);
assert.ok(INITIAL_FETCH_LIMIT_UNCATEGORIZED < INITIAL_FETCH_LIMIT);

// Limit di atas clamp BE akan dipotong sepi-sepi → section tampak kurang game.
for (const limit of [INITIAL_FETCH_LIMIT, INITIAL_FETCH_LIMIT_UNCATEGORIZED, MORE_FETCH_LIMIT]) {
  assert.ok(limit >= 1 && limit <= SERVER_MAX_LIMIT, `limit ${limit} di luar 1..${SERVER_MAX_LIMIT}`);
}

assert.equal(initialFetchLimit(UNCATEGORIZED_CATEGORY_SLUG), INITIAL_FETCH_LIMIT_UNCATEGORIZED);
assert.equal(initialFetchLimit("mobile"), INITIAL_FETCH_LIMIT);
assert.equal(initialFetchLimit("kategori-karangan-admin"), INITIAL_FETCH_LIMIT);

/**
 * Simulasi useInfiniteQuery: batch pertama initialFetchLimit, sisanya
 * MORE_FETCH_LIMIT, offset dari nextBrandOffset. `available` = isi katalog
 * sebenarnya di server (boleh beda dari brandCount).
 */
function loadAll(slug: string, brandCount: number, available = brandCount) {
  const firstBatch = initialFetchLimit(slug);
  const offsets: number[] = [];
  let loaded = 0;
  let offset: number | undefined = 0;

  while (offset !== undefined) {
    offsets.push(offset);
    const limit = offset === 0 ? firstBatch : MORE_FETCH_LIMIT;
    const batch = Math.max(0, Math.min(limit, available - offset));

    loaded += batch;
    offset = nextBrandOffset(loaded, brandCount, batch);

    assert.ok(offsets.length < 100, "deret offset tidak berhenti");
  }

  return { offsets, loaded };
}

// Katalog "Lainnya" dari screenshot: 129 game, batch awal 6 lalu 24-an.
const uncategorized = loadAll(UNCATEGORIZED_CATEGORY_SLUG, 129);
assert.equal(uncategorized.loaded, 129, "ada game yang tidak pernah terambil");
assert.deepEqual(uncategorized.offsets, [0, 6, 30, 54, 78, 102, 126]);
// Offset harus naik terus: turun/berulang = kartu dobel, melompat = game hilang.
assert.deepEqual([...new Set(uncategorized.offsets)], uncategorized.offsets);

for (const total of [0, 1, 5, 6, 11, 12, 13, 36, 37, 129]) {
  for (const slug of [UNCATEGORIZED_CATEGORY_SLUG, "mobile"]) {
    assert.equal(loadAll(slug, total).loaded, total, `section ${slug} total ${total}`);
  }
}

// Batch pertama sudah menutup section kecil → jangan minta batch kedua.
assert.equal(nextBrandOffset(INITIAL_FETCH_LIMIT, INITIAL_FETCH_LIMIT, INITIAL_FETCH_LIMIT), undefined);

// brandCount lebih besar dari isi katalog (brand dinonaktifkan setelah hitung
// kategori): fetch harus berhenti di batch kosong, bukan memanggil server terus.
const stale = loadAll("mobile", 50, 20);
assert.equal(stale.loaded, 20);
assert.equal(nextBrandOffset(20, 50, 0), undefined);

// Pratinjau slug harus sama dengan ProductService.NormalizeCategory di BE,
// kalau beda admin melihat slug yang bukan slug yang tersimpan.
assert.equal(categorySlugPreview("  Konsol   Game  "), "konsol-game");
assert.equal(categorySlugPreview("E-Money"), "e-money");
assert.equal(categorySlugPreview("   "), "");

// Kategori custom dari admin tidak ada di CATEGORY_ICONS — tab/section-nya
// tetap harus punya ikon, bukan judul melayang.
assert.equal(categoryIcon("mobile"), CATEGORY_ICONS.mobile);
assert.ok(categoryIcon("kategori-karangan-admin"), "kategori custom harus dapat ikon fallback");

// Section "Populer" virtual (dari GET /games/brands/popular) butuh nama & ikon
// sendiri — kalau jatuh ke fallback, judulnya jadi slug mentah "populer".
assert.equal(CATEGORY_NAMES[POPULAR_CATEGORY_SLUG], "Populer");
assert.notEqual(
  categoryIcon(POPULAR_CATEGORY_SLUG),
  categoryIcon("kategori-karangan-admin"),
);

console.log(
  `gameCategories checks passed (batch awal ${INITIAL_FETCH_LIMIT}/${INITIAL_FETCH_LIMIT_UNCATEGORIZED}, lanjutan ${MORE_FETCH_LIMIT})`,
);
