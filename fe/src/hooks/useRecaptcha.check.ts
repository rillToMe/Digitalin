/**
 * Self-check: setiap pemanggilan executeRecaptcha harus berada di dalam
 * mutationFn, bukan di handler tombol. Kalau lepas ke handler, `isPending`
 * baru true setelah captcha selesai — tombolnya diam beberapa ratus milidetik
 * dan kliknya terasa tidak masuk. Gagalnya sunyi (tidak ada error, cuma delay).
 *   npx tsx src/hooks/useRecaptcha.check.ts
 */
import assert from "node:assert/strict";
import { readdirSync, readFileSync, statSync } from "node:fs";

const root = new URL("../modules/", import.meta.url);

function tsxFiles(dir: URL): URL[] {
  return readdirSync(dir).flatMap((name) => {
    const entry = new URL(name, dir);

    if (statSync(entry).isDirectory()) return tsxFiles(new URL(`${name}/`, dir));

    return name.endsWith(".tsx") ? [entry] : [];
  });
}

let checked = 0;

for (const file of tsxFiles(root)) {
  const src = readFileSync(file, "utf8");
  const calls = [...src.matchAll(/executeRecaptcha\("[^"]+"\)/g)];
  if (calls.length === 0) continue;

  const name = file.pathname.split("/").pop();
  checked++;

  for (const call of calls) {
    const before = src.slice(0, call.index);
    const insideMutation = before.lastIndexOf("mutationFn:");
    const insideHandler = Math.max(
      before.lastIndexOf("const handle"),
      before.lastIndexOf("const send"),
    );

    assert.ok(
      insideMutation > insideHandler,
      `${name}: ${call[0]} ada di handler tombol, harus di dalam mutationFn`,
    );
  }

  // Pesan error captcha tidak boleh jatuh ke fallback "password salah".
  assert.ok(
    src.includes("instanceof RecaptchaError"),
    `${name}: onError harus membedakan RecaptchaError dari error API`,
  );
}

assert.ok(checked >= 3, `minimal 3 halaman bercaptcha harus dicek, dapat ${checked}`);
console.log(`useRecaptcha checks passed (${checked} halaman)`);
