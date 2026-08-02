/**
 * Self-check kontrak `.admin-table` untuk semua halaman yang pakai cellProps.
 * Kegagalannya sunyi: data-label yang salah bikin label kartu kosong di mobile,
 * dan kolom `wide` yang lupa dipasang bikin tabelnya scroll horizontal lagi.
 * Tanpa framework, jalankan langsung:
 *   npx tsx src/components/admin/adminTable.check.ts
 */
import assert from "node:assert/strict";
import { readdirSync, readFileSync } from "node:fs";

const dir = new URL("../../modules/admin/pages/", import.meta.url);
let checked = 0;

for (const file of readdirSync(dir).filter((f) => f.endsWith(".tsx"))) {
  const src = readFileSync(new URL(file, dir), "utf8");
  if (!src.includes("cellProps(")) continue;
  checked++;

  // Hanya blok COLUMNS — `label:` juga dipakai konstanta lain (mis. STATUS_FILTERS).
  const block = src.match(/const COLUMNS: AdminColumn\[\] = \[([\s\S]*?)\n\];/);
  assert.ok(block, `${file}: blok COLUMNS: AdminColumn[] tidak ditemukan`);

  const labels = [...block[1].matchAll(/\{\s*label:\s*"([^"]+)"/g)].map((m) => m[1]);
  const used = [...src.matchAll(/\bcell\("([^"]+)"\)/g)].map((m) => m[1]);

  assert.ok(labels.length > 0, `${file}: COLUMNS kosong`);
  assert.deepEqual(used, labels, `${file}: urutan <td> harus sama dengan urutan header`);
  assert.ok(src.includes('className="admin-table"'), `${file}: <table> harus pakai .admin-table`);
  assert.ok(src.includes('role="rowgroup"'), `${file}: thead/tbody butuh role="rowgroup"`);
  // colSpan empty state ikut COLUMNS.length, bukan angka hardcoded yang basi.
  assert.ok(src.includes("colSpan={COLUMNS.length}"), `${file}: colSpan harus COLUMNS.length`);
}

assert.ok(checked >= 2, `minimal 2 halaman harus ikut dicek, dapat ${checked}`);
console.log(`adminTable checks passed (${checked} halaman)`);
