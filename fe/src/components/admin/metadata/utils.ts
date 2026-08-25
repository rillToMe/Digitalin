export function formatRp(n: number) {
  return "Rp " + n.toLocaleString("id-ID");
}

export function formatNumberInput(value: string) {
  if (!value) return "";

  return Number(value).toLocaleString("id-ID");
}

export type ModalTab = "info" | "products";
