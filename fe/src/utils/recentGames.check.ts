/**
 * Self-check: cookie "Terakhir Dibeli" harus aman terhadap isi rusak /
 * diedit tangan / kepanjangan, dan tidak pernah menyimpan apa pun selain
 * slug katalog publik.
 *   npx tsx src/utils/recentGames.check.ts
 */
import assert from "node:assert/strict";
import {
  RECENT_GAMES_MAX,
  parseRecentSlugs,
  serializeRecentSlugs,
  upsertRecentSlug,
} from "./recentGames";

// Cookie kosong / hilang → tidak ada section "Terakhir Dibeli".
assert.deepEqual(parseRecentSlugs(null), []);
assert.deepEqual(parseRecentSlugs(undefined), []);
assert.deepEqual(parseRecentSlugs(""), []);

// Isi normal — urutan cookie (terbaru dulu) dipertahankan.
assert.deepEqual(
  parseRecentSlugs("free-fire,mobile-legends,genshin-impact"),
  ["free-fire", "mobile-legends", "genshin-impact"],
);

// Tampered: entri sampah dibuang, entri kotor dinormalisasi, duplikat hilang.
assert.deepEqual(
  parseRecentSlugs("MOBILE_LEGENDS,***, mobile-legends ,,sql-injection';--"),
  ["mobile-legends"],
);
assert.deepEqual(
  parseRecentSlugs("a,b,a,B"),
  ["a", "b"],
);

// Oversized: dibatasi RECENT_GAMES_MAX.
const oversized = Array.from({ length: RECENT_GAMES_MAX + 10 }, (_, i) => `game-${i}`);
assert.equal(parseRecentSlugs(oversized.join(",")).length, RECENT_GAMES_MAX);
assert.deepEqual(
  parseRecentSlugs(oversized.join(",")),
  oversized.slice(0, RECENT_GAMES_MAX),
);

// Upsert: terbaru pindah ke depan, duplikat tidak dobel, tetap bounded.
assert.deepEqual(
  upsertRecentSlug(["a", "b", "c"], "b"),
  ["b", "a", "c"],
);
const full = Array.from({ length: RECENT_GAMES_MAX }, (_, i) => `game-${i}`);
const afterUpsert = upsertRecentSlug(full, "game-new");
assert.deepEqual(afterUpsert, ["game-new", ...full.slice(0, RECENT_GAMES_MAX - 1)]);
// slug lama paling belakang terdorong keluar (bounded).
assert.equal(upsertRecentSlug(full, "game-0").length, RECENT_GAMES_MAX);
// slug tidak layak → daftar tidak berubah.
assert.deepEqual(upsertRecentSlug(["a"], "DROP TABLE users;"), ["a"]);

// Round-trip serialize → parse identik.
assert.deepEqual(
  parseRecentSlugs(serializeRecentSlugs(["x", "y", "z"])),
  ["x", "y", "z"],
);

// Nilai cookie hanya slug — tidak pernah memuat data transaksi.
const serialized = serializeRecentSlugs(["x", "y"]);
assert.ok(!/INV-|player|token|@|\d{6,}/.test(serialized), `cookie menyimpan data sensitif: ${serialized}`);

console.log(`recentGames check OK (${RECENT_GAMES_MAX} max, tamper-safe, slug-only)`);
