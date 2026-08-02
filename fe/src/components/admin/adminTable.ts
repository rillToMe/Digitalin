/**
 * Kontrak sel untuk tabel `.admin-table` (aturannya di src/styles/admin.css).
 *
 * Di bawah 768px tabel admin berubah jadi kartu label→nilai, jadi tiap <td>
 * butuh dua hal yang tidak bisa disediakan CSS sendiri:
 *   - data-label : dibaca `content: attr(data-label)` sebagai label kartu.
 *   - role="cell": `display: block` menghapus semantik tabel dari a11y tree,
 *                  jadi role-nya harus dipasang manual supaya screen reader
 *                  tetap membacanya sebagai tabel.
 */
export interface AdminColumn {
  label: string;
  /** Disembunyikan di layar kecil (detailnya tersedia di modal). */
  wide?: boolean;
}

export function cellProps(columns: AdminColumn[]) {
  const wide = new Set(columns.filter((c) => c.wide).map((c) => c.label));

  return (label: string) => ({
    "data-label": label,
    role: "cell" as const,
    className: `px-4 py-3${wide.has(label) ? " admin-col-wide" : ""}`,
  });
}
