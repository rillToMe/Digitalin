/**
 * Self-check untuk logika non-trivial di RevenueChart: resolusi token warna
 * dan gradient callback. Sengaja tanpa framework — jalankan langsung:
 *   npx tsx src/components/admin/RevenueChart.check.ts
 * Yang diuji cuma yang bisa rusak diam-diam (token kosong → canvas throw).
 */
import assert from "node:assert/strict";

import { areaFill, token } from "./RevenueChart";

const TOKENS: Record<string, string> = {
  "--color-chart-revenue-from": "rgba(44, 94, 245, 0.2)",
  "--color-chart-revenue-to": "rgba(44, 94, 245, 0)",
};

// Stub minimal: hanya getComputedStyle + document yang dipakai token().
(globalThis as unknown as { document: unknown }).document = { documentElement: {} };
(globalThis as unknown as { getComputedStyle: unknown }).getComputedStyle = () => ({
  getPropertyValue: (name: string) => TOKENS[name] ?? "",
});

assert.equal(token("--color-chart-revenue-from"), "rgba(44, 94, 245, 0.2)");
assert.equal(token("--tidak-ada"), "", "token hilang harus jadi string kosong");

const stops: [number, string][] = [];
const chart = {
  chart: {
    chartArea: { top: 0, bottom: 280 },
    ctx: {
      createLinearGradient: () => ({
        addColorStop: (offset: number, color: string) => {
          assert.ok(color, "addColorStop dengan string kosong akan throw di canvas asli");
          stops.push([offset, color]);
        },
      }),
    },
  },
};

const fill = areaFill("--color-chart-revenue-from", "--color-chart-revenue-to");

// Layout belum siap → warna solid, bukan crash.
assert.equal(
  fill({ chart: { chartArea: undefined, ctx: {} } } as never),
  "rgba(44, 94, 245, 0.2)",
);

fill(chart as never);
assert.deepEqual(stops, [
  [0, "rgba(44, 94, 245, 0.2)"],
  [1, "rgba(44, 94, 245, 0)"],
]);

// Token tidak terdaftar → transparent, dashboard tetap render.
assert.equal(areaFill("--hilang", "--juga-hilang")(chart as never), "transparent");

console.log("RevenueChart checks passed");
