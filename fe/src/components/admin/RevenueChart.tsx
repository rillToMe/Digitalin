import { useEffect, useRef } from "react";
import {
  CategoryScale,
  Chart,
  Filler,
  Legend,
  LinearScale,
  LineController,
  LineElement,
  PointElement,
  Tooltip,
  type ChartOptions,
  type ScriptableContext,
} from "chart.js";

import type { ChartDataPoint } from "../../api/index";

/* Import per-komponen, bukan "chart.js/auto": auto mendaftarkan SEMUA
   controller (bar, pie, radar, scatter, doughnut...) sehingga tree-shaking
   mati dan bundle-nya ikut membawa chart yang tidak dipakai. Daftar di bawah
   adalah yang minimum untuk area chart: line + fill + 2 skala + tooltip. */
Chart.register(
  LineController,
  LineElement,
  PointElement,
  LinearScale,
  CategoryScale,
  Filler,
  Tooltip,
  Legend,
);

const rupiah = (n: number) => "Rp " + n.toLocaleString("id-ID");

/** Canvas tidak mengerti var(--x), jadi token harus dibaca jadi nilai dulu.
    Komponen tetap tidak tahu warnanya — hanya nama token-nya.
    Diekspor untuk RevenueChart.check.ts. */
export function token(name: string) {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

/** Gradient canvas butuh dua warna konkret, dan CSS gradient tidak bisa
    dipakai di <canvas>, jadi tiap stop punya token sendiri di globals.css. */
export function areaFill(fromToken: string, toToken: string) {
  const from = token(fromToken);
  const to = token(toToken);

  return (ctx: ScriptableContext<"line">) => {
    const { chartArea, ctx: canvas } = ctx.chart;
    // Token hilang/typo → getPropertyValue "" → addColorStop("") throw dan
    // seluruh dashboard blank. Area fill boleh hilang, chart-nya jangan.
    if (!from || !to) return "transparent";
    // Pass pertama dijalankan sebelum layout selesai — chartArea belum ada.
    if (!chartArea) return from;

    const gradient = canvas.createLinearGradient(0, chartArea.top, 0, chartArea.bottom);
    gradient.addColorStop(0, from);
    gradient.addColorStop(1, to);
    return gradient;
  };
}

export default function RevenueChart({ data }: { data: ChartDataPoint[] }) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const accent = token("--color-brand-tertiary");
    const success = token("--color-success");
    const muted = token("--color-brand-secondary");

    const options: ChartOptions<"line"> = {
      responsive: true,
      maintainAspectRatio: false,
      animation: window.matchMedia("(prefers-reduced-motion: reduce)").matches
        ? false
        : { duration: 400 },
      interaction: { mode: "index", intersect: false },
      scales: {
        x: {
          grid: { display: false },
          border: { display: false },
          ticks: { color: muted, font: { size: 12 } },
        },
        y: {
          grid: { color: token("--color-border-lighter") },
          border: { display: false },
          ticks: {
            color: muted,
            font: { size: 11 },
            callback: (value) => "Rp " + (Number(value) / 1000).toLocaleString("id-ID") + "rb",
          },
        },
      },
      plugins: {
        legend: {
          labels: {
            color: token("--color-brand-primary"),
            usePointStyle: true,
            pointStyle: "line",
            boxWidth: 24,
            font: { size: 13 },
          },
        },
        tooltip: {
          backgroundColor: token("--color-surface"),
          borderColor: token("--color-border-default"),
          borderWidth: 1,
          titleColor: token("--color-brand-primary"),
          bodyColor: token("--color-brand-primary"),
          cornerRadius: 10,
          padding: 10,
          bodyFont: { size: 13 },
          callbacks: {
            label: (item) => ` ${item.dataset.label}: ${rupiah(item.parsed.y)}`,
          },
        },
      },
    };

    const chart = new Chart(canvas, {
      type: "line",
      data: {
        labels: data.map((d) => d.name),
        datasets: [
          {
            label: "Pendapatan",
            data: data.map((d) => d.pendapatan),
            borderColor: accent,
            backgroundColor: areaFill("--color-chart-revenue-from", "--color-chart-revenue-to"),
            borderWidth: 2,
            tension: 0.35,
            fill: true,
            pointRadius: 0,
            pointHoverRadius: 4,
            pointHoverBackgroundColor: accent,
          },
          {
            label: "Profit",
            data: data.map((d) => d.profit),
            borderColor: success,
            backgroundColor: areaFill("--color-chart-profit-from", "--color-chart-profit-to"),
            borderWidth: 2,
            tension: 0.35,
            fill: true,
            pointRadius: 0,
            pointHoverRadius: 4,
            pointHoverBackgroundColor: success,
          },
        ],
      },
      options,
    });

    return () => chart.destroy();
  }, [data]);

  /* Canvas tidak terbaca screen reader, jadi angkanya diringkas di aria-label
     dan detail per titik tetap tersedia sebagai tabel di bawah (sr-only). */
  const total = data.reduce((sum, d) => sum + d.pendapatan, 0);

  return (
    <div style={{ height: 280, position: "relative" }}>
      <canvas
        ref={canvasRef}
        role="img"
        aria-label={`Grafik pendapatan dan profit, ${data.length} titik data, total pendapatan ${rupiah(total)}`}
      />
      <table className="sr-only">
        <caption>Pendapatan dan profit per periode</caption>
        <thead>
          <tr>
            <th scope="col">Periode</th>
            <th scope="col">Pendapatan</th>
            <th scope="col">Profit</th>
          </tr>
        </thead>
        <tbody>
          {data.map((d) => (
            <tr key={d.name}>
              <th scope="row">{d.name}</th>
              <td>{rupiah(d.pendapatan)}</td>
              <td>{rupiah(d.profit)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
