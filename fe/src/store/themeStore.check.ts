/**
 * Self-check: tema gelap harus menimpa SEMUA token warna tema terang.
 * Satu token yang lupa ditimpa gagal sunyi — misal --color-surface tetap
 * putih di atas halaman gelap, atau teks #0f1419 di atas kartu #171c23 yang
 * praktis tidak terbaca. Ini tidak ketahuan sampai ada yang membuka halaman
 * itu di tema gelap, jadi dicek di sini.
 *   npx tsx src/store/themeStore.check.ts
 */
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { DEFAULT_THEME, resolveTheme } from "./themeStore";

// Storage bisa berisi apa saja (versi lama, diedit tangan, JSON rusak).
assert.equal(resolveTheme("light"), "light");
assert.equal(resolveTheme("dark"), "dark");
assert.equal(resolveTheme(undefined), DEFAULT_THEME);
assert.equal(resolveTheme(null), DEFAULT_THEME);
assert.equal(resolveTheme("DARK"), DEFAULT_THEME);
assert.equal(resolveTheme({ theme: "dark" }), DEFAULT_THEME);

const css = readFileSync(new URL("../styles/globals.css", import.meta.url), "utf8");

/** Isi satu blok deklarasi CSS berdasarkan selektornya. */
function block(selector: string): string {
  const start = css.indexOf(`${selector} {`);
  assert.notEqual(start, -1, `blok ${selector} tidak ada di globals.css`);
  const end = css.indexOf("\n}", start);
  assert.notEqual(end, -1, `blok ${selector} tidak ditutup`);
  return css.slice(start, end);
}

function tokens(selector: string): string[] {
  return [...block(selector).matchAll(/--color-[\w-]+(?=\s*:)/g)].map((m) => m[0]);
}

/**
 * Token yang memang sama di kedua tema: warna yang selalu duduk di atas
 * permukaan gelap (tombol aksen kobalt, footer) atau di atas gambar (kontrol
 * banner) — latarnya bukan permukaan aplikasi, jadi tidak ikut berubah.
 */
const SHARED_TOKENS = new Set([
  "--color-on-primary",
  "--color-media-control",
  "--color-on-media",
  "--color-on-media-muted",
  "--color-footer-heading",
  "--color-footer-text",
  "--color-footer-link",
  "--color-footer-link-hover",
  "--color-footer-border",
  "--color-footer-focus-ring",
]);

const light = tokens(":root");
const dark = new Set(tokens("html.dark"));

assert.ok(light.length > 20, "token tema terang tidak terbaca");

for (const token of light) {
  if (SHARED_TOKENS.has(token)) continue;
  assert.ok(dark.has(token), `${token} tidak ditimpa di html.dark`);
}

// Kebalikannya juga: token gelap yang tidak punya pasangan terang berarti
// salah tulis — tidak akan pernah dipakai komponen mana pun.
for (const token of dark) {
  assert.ok(light.includes(token), `${token} ada di html.dark tapi tidak di :root`);
}

// --color-skeleton di tema terang menunjuk ke --color-neutral; di gelap neutral
// adalah latar halaman, jadi nilainya wajib berdiri sendiri.
assert.ok(
  !/--color-skeleton:\s*var\(--color-neutral\)/.test(block("html.dark")),
  "skeleton gelap tidak boleh sama dengan latar halaman",
);

// Footer meminjam --color-brand-primary di tema terang. Di gelap token itu
// sudah jadi warna teks terang, jadi footer harus punya nilainya sendiri.
assert.ok(
  !/--color-footer-bg:\s*var\(--color-brand-primary\)/.test(block("html.dark")),
  "footer gelap masih meminjam --color-brand-primary (akan jadi latar putih)",
);

// Tanpa color-scheme, scrollbar dan kontrol native tetap versi terang.
assert.match(block("html.dark"), /color-scheme:\s*dark/);
assert.match(block(":root"), /color-scheme:\s*light/);

console.log(
  `themeStore checks passed (default ${DEFAULT_THEME}, ${light.length} token terang / ${dark.size} timpaan gelap)`,
);
