/**
 * Self-check: nilai consent yang tidak dikenal TIDAK boleh dianggap
 * persetujuan — cookie rusak/diedit tangan/versi lama jatuh ke null
 * (banner tampil lagi), dan hanya "accepted" yang membuka gerbang cookie
 * non-esensial.
 *   npx tsx src/utils/cookieConsent.check.ts
 */
import assert from "node:assert/strict";
import { parseConsentValue } from "./cookieConsent";

// Dua nilai sah.
assert.equal(parseConsentValue("accepted"), "accepted");
assert.equal(parseConsentValue("rejected"), "rejected");

// Null/kosong = belum memilih.
assert.equal(parseConsentValue(null), null);
assert.equal(parseConsentValue(undefined), null);
assert.equal(parseConsentValue(""), null);
assert.equal(parseConsentValue("   "), null);

// Tampered/asing = BUKAN persetujuan (fail-closed, banner tampil lagi).
// (Huruf besar/karakter pembungkus TIDAK termasuk tampering — domain nilai
// cuma dua string tetap, jadi dinormalisasi, lihat blok bawah.)
assert.equal(parseConsentValue("yes"), null);
assert.equal(parseConsentValue("accepted; Path=/"), null);
assert.equal(parseConsentValue('accepted<script>'), null);
assert.equal(parseConsentValue("all"), null);

// Nilai kotor tapi intinya sah → dinormalisasi, bukan ditolak.
assert.equal(parseConsentValue("  Accepted  "), "accepted");
assert.equal(parseConsentValue(" rejected\n"), "rejected");
assert.equal(parseConsentValue("ACCEPTED"), "accepted");
assert.equal(parseConsentValue("Rejected"), "rejected");

// Fail-closed berlaku juga untuk gerbang: hanya "accepted" lolos.
// (hasCookieConsent butuh document — di node tanpa DOM harus false, bukan throw.)
import { hasCookieConsent, readCookieConsent } from "./cookieConsent";
assert.equal(readCookieConsent(), null);
assert.equal(hasCookieConsent(), false);

console.log("cookieConsent check OK (fail-closed, dua nilai sah, tamper-safe)");
