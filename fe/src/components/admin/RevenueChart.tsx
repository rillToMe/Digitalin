import { useEffect, useRef } from "react";
import ApexCharts from "apexcharts/core";
import "apexcharts/line";
import "apexcharts/features/legend";
import "apexcharts/features/toolbar";
import type { ApexOptions } from "apexcharts";

import type { ChartDataPoint } from "../../api/index";
import { useThemeStore } from "../../store/themeStore";

const rupiah = (n: number) => "Rp " + n.toLocaleString("id-ID");

/** Read CSS tokens so chart colors stay aligned with active theme. */
export function token(name: string) {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

export default function RevenueChart({ data }: { data: ChartDataPoint[] }) {
  const chartRef = useRef<HTMLDivElement>(null);
  const theme = useThemeStore((state) => state.theme);

  useEffect(() => {
    const element = chartRef.current;
    if (!element) return;

    const accent = token("--color-brand-tertiary");
    const success = token("--color-success");
    const muted = token("--color-brand-secondary");
    const primary = token("--color-brand-primary");
    const surface = token("--color-surface");
    const border = token("--color-border-lighter");
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    const options: ApexOptions = {
      chart: {
        type: "line",
        height: "100%",
        fontFamily: "inherit",
        background: "transparent",
        foreColor: muted,
        zoom: { enabled: true, type: "x", autoScaleYaxis: true },
        toolbar: {
          show: true,
          autoSelected: "zoom",
          offsetY: -8,
          tools: { download: false, selection: false, zoom: true, zoomin: true, zoomout: true, pan: false, reset: true },
        },
        animations: { enabled: !reducedMotion, speed: 450, easing: "easeinout" },
      },
      series: [
        { name: "Pendapatan", data: data.map((point) => point.pendapatan) },
        { name: "Profit", data: data.map((point) => point.profit) },
      ],
      colors: [accent, success],
      stroke: { curve: "straight", width: 4, colors: [accent, success], lineCap: "butt" },
      markers: { size: 0, hover: { size: 5, sizeOffset: 2 } },
      dataLabels: { enabled: false },
      grid: { borderColor: border, strokeDashArray: 3, padding: { left: 8, right: 12, top: 8, bottom: 0 } },
      xaxis: {
        categories: data.map((point) => point.name),
        axisBorder: { show: false },
        axisTicks: { show: false },
        labels: { style: { colors: muted, fontSize: "12px" }, hideOverlappingLabels: true },
        tooltip: { enabled: false },
      },
      yaxis: {
        min: 0,
        labels: {
          style: { colors: muted, fontSize: "11px" },
          formatter: (value) => `Rp ${(value / 1000).toLocaleString("id-ID")}rb`,
        },
      },
      legend: {
        position: "top",
        horizontalAlign: "center",
        fontSize: "13px",
        labels: { colors: primary },
        markers: { size: 5, shape: "line", strokeWidth: 2 },
        itemMargin: { horizontal: 10 },
      },
      tooltip: {
        theme: surface === "#ffffff" ? "light" : "dark",
        shared: true,
        intersect: false,
        x: { show: true },
        y: { formatter: (value) => rupiah(value) },
        marker: { show: true },
      },
      responsive: [{ breakpoint: 640, options: { chart: { toolbar: { show: false } }, legend: { fontSize: "11px", itemMargin: { horizontal: 5 } }, xaxis: { labels: { style: { fontSize: "10px" } } } } }],
    };

    const chart = new ApexCharts(element, options);
    void chart.render();
    return () => {
      void chart.destroy();
    };
  }, [data, theme]);

  const total = data.reduce((sum, point) => sum + point.pendapatan, 0);

  return (
    <div>
      <div ref={chartRef} style={{ height: 280 }} role="img" aria-label={`Grafik pendapatan dan profit, ${data.length} titik data, total pendapatan ${rupiah(total)}`} />
      <table className="sr-only">
        <caption>Pendapatan dan profit per periode</caption>
        <thead><tr><th scope="col">Periode</th><th scope="col">Pendapatan</th><th scope="col">Profit</th></tr></thead>
        <tbody>{data.map((point) => <tr key={point.name}><th scope="row">{point.name}</th><td>{rupiah(point.pendapatan)}</td><td>{rupiah(point.profit)}</td></tr>)}</tbody>
      </table>
    </div>
  );
}
