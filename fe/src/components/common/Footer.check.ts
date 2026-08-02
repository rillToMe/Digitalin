/**
 * Self-check: setiap tujuan link footer harus punya <Route path> di App.tsx.
 * Link footer jarang diklik saat ngoding, jadi yang mati bisa bertahan lama
 * tanpa ada yang sadar — dan route "*" menelannya jadi redirect sunyi ke "/".
 *   npx tsx src/components/common/Footer.check.ts
 */
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const footer = readFileSync(new URL("./Footer.tsx", import.meta.url), "utf8");
const app = readFileSync(new URL("../../App.tsx", import.meta.url), "utf8");

const routes = new Set(
  [...app.matchAll(/path="([^"]+)"/g)].map((m) => m[1]),
);

const targets = [...footer.matchAll(/to: "([^"]+)"/g)].map((m) => m[1]);

assert.ok(targets.length > 0, "NAV_GROUPS kosong — regex-nya kena atau link-nya hilang");

for (const target of targets) {
  // "/#game-list" adalah anchor ke section di halaman "/", bukan route.
  const path = target.startsWith("/#") ? "/" : target;

  assert.ok(routes.has(path), `Footer: ${target} tidak ada di <Route path> App.tsx`);
}

// Tahun copyright harus dihitung, bukan literal yang basi tiap 1 Januari.
assert.ok(
  footer.includes("new Date().getFullYear()"),
  "Footer: tahun copyright harus dihitung runtime",
);

console.log(`Footer checks passed (${targets.length} link)`);
