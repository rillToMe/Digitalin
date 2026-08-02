/**
 * Self-check loader Snap: script-nya dibagi satu unduhan untuk semua pemanggil,
 * dan setelah gagal memo-nya direset supaya percobaan berikutnya tidak
 * menggantung pada <script> mati.
 *   npx tsx src/services/midtransSnap.check.ts
 */
import assert from "node:assert/strict";

interface FakeScript {
  id: string;
  src: string;
  dataset: Record<string, string>;
  async: boolean;
  onload: (() => void) | null;
  onerror: (() => void) | null;
  remove: () => void;
}

const created: FakeScript[] = [];
let paidToken: string | null = null;

// DOM tiruan seminimal mungkin — cukup untuk mengamati <script> yang dibuat.
const g = globalThis as unknown as {
  window: { snap?: { pay: (token: string, options: unknown) => void } };
  document: {
    createElement: (tag: string) => FakeScript;
    head: { appendChild: (el: FakeScript) => void };
  };
};

g.window = {};
g.document = {
  createElement: () => {
    const el: FakeScript = {
      id: "",
      src: "",
      dataset: {},
      async: false,
      onload: null,
      onerror: null,
      remove: () => {},
    };

    created.push(el);

    return el;
  },
  head: { appendChild: () => {} },
};

const { paySnap, preloadSnap, SnapError } = await import("./midtransSnap");

preloadSnap();
preloadSnap();
assert.equal(created.length, 1, "snap.js harus diunduh sekali meski preload dipanggil berulang");
assert.ok(created[0].src.endsWith("/snap/snap.js"), "URL snap.js salah");
assert.ok(created[0].dataset.clientKey.length > 0, "data-client-key wajib terisi");

// paySnap ikut menunggu unduhan yang sedang jalan, tidak memulai unduhan kedua.
const failed = paySnap("TOKEN-GAGAL", {}).catch((err: unknown) => err);
assert.equal(created.length, 1, "paySnap harus memakai hasil pramuat yang sedang berjalan");

created[0].onerror?.();
assert.ok(await failed instanceof SnapError, "kegagalan unduh harus jadi SnapError");

// Memo direset: percobaan setelah gagal membuat <script> baru, bukan menempel
// ke elemen mati yang tidak akan pernah memicu load/error lagi.
preloadSnap();
assert.equal(created.length, 2, "setelah gagal, percobaan berikutnya harus mengunduh ulang");

created[1].onload?.();
g.window.snap = { pay: (token) => { paidToken = token; } };
await paySnap("TOKEN-OK", {});
assert.equal(paidToken, "TOKEN-OK", "token harus diteruskan ke window.snap.pay");
assert.equal(created.length, 2, "script yang sudah siap tidak boleh diunduh lagi");

console.log(`midtransSnap checks passed (${created.length} script dibuat)`);
