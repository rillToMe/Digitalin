import { useEffect, useState } from "react";
import { Gamepad2, Loader2, Plus, Save, Search, Trash2, X } from "lucide-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { AdminLayout } from "../../../layouts/AdminLayout";
import { adminService } from "../../../services/api";
import type { BrandItem, GameMetadataItem } from "../../../api/index";

const MAX_POPULAR_GAMES = 12;

export default function PopularPage() {
  const queryClient = useQueryClient();
  const [modalOpen, setModalOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [draft, setDraft] = useState<string[]>([]);

  const { data, isLoading } = useQuery({
    queryKey: ["admin-popular-games"],
    queryFn: adminService.getPopularGames,
  });

  const selected = data?.selected ?? [];
  const options = data?.options ?? [];

  const saveMutation = useMutation({
    mutationFn: adminService.updatePopularGames,
    onSuccess: (result) => {
      queryClient.setQueryData(["admin-popular-games"], { options, selected: result.selected });
      queryClient.invalidateQueries({ queryKey: ["popularBrands"] });
      toast.success(result.message);
      setModalOpen(false);
    },
    onError: (err: { response?: { data?: { message?: string } } }) =>
      toast.error(err.response?.data?.message ?? "Gagal menyimpan game populer"),
  });

  const openModal = () => {
    setDraft(selected.map((game) => game.slug));
    setSearch("");
    setModalOpen(true);
  };

  useEffect(() => {
    if (!modalOpen) return;
    const close = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !saveMutation.isPending) setModalOpen(false);
    };
    window.addEventListener("keydown", close);
    return () => window.removeEventListener("keydown", close);
  }, [modalOpen, saveMutation.isPending]);

  const remove = (slug: string) => {
    const next = selected.filter((game) => game.slug !== slug).map((game) => game.slug);
    saveMutation.mutate(next);
  };

  const toggle = (game: GameMetadataItem) => {
    const slug = game.brandSlug;
    setDraft((current) => {
      if (current.includes(slug)) return current.filter((item) => item !== slug);
      if (current.length >= MAX_POPULAR_GAMES) {
        toast.error(`Maksimal ${MAX_POPULAR_GAMES} game populer.`);
        return current;
      }
      return [...current, slug];
    });
  };

  const filtered = options.filter((game) =>
    game.brand.toLowerCase().includes(search.trim().toLowerCase()),
  );

  return (
    <AdminLayout title="Game Populer" breadcrumb={["Admin", "Game Populer"]}>
      <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-start">
        <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem", maxWidth: "62ch" }}>
          Pilih maksimal {MAX_POPULAR_GAMES} game. Urutan daftar ini menjadi urutan game pada
          section Populer paling atas di homepage.
        </p>
        <button
          type="button"
          onClick={openModal}
          disabled={isLoading}
          className="flex items-center gap-2 rounded-lg px-4 py-2.5 sm:ml-auto"
          style={{ backgroundColor: "var(--color-brand-tertiary)", color: "var(--color-on-primary)", fontWeight: 600, whiteSpace: "nowrap" }}
        >
          <Plus size={17} aria-hidden="true" /> Kelola Game
        </button>
      </div>

      <div className="overflow-hidden rounded-lg" style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}>
        <div className="overflow-x-auto">
          <table className="admin-table">
            <thead>
              <tr style={{ backgroundColor: "var(--color-neutral)", borderBottom: "1px solid var(--color-border-light)" }}>
                {(["Urutan", "Cover", "Nama Game", "Aksi"] as const).map((label) => (
                  <th key={label} className="px-5 py-4 text-left" style={{ color: "var(--color-brand-secondary)", fontSize: "0.75rem", textTransform: "uppercase" }}>{label}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {isLoading && Array.from({ length: 5 }).map((_, index) => (
                <tr key={index}><td colSpan={4} className="px-5 py-5"><div className="h-9 animate-pulse rounded" style={{ backgroundColor: "var(--color-skeleton)" }} /></td></tr>
              ))}
              {!isLoading && selected.map((game, index) => (
                <PopularRow key={game.slug} game={game} index={index} deleting={saveMutation.isPending} onDelete={() => remove(game.slug)} />
              ))}
              {!isLoading && selected.length === 0 && (
                <tr><td colSpan={4} className="px-5 py-14 text-center" style={{ color: "var(--color-brand-secondary)" }}>Belum ada game populer.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: "var(--color-overlay)" }} onClick={() => !saveMutation.isPending && setModalOpen(false)}>
          <div role="dialog" aria-modal="true" aria-labelledby="popular-modal-title" className="flex w-full max-w-2xl flex-col rounded-lg p-5" style={{ backgroundColor: "var(--color-surface)", maxHeight: "88vh" }} onClick={(event) => event.stopPropagation()}>
            <div className="mb-4 flex items-center justify-between gap-4">
              <div>
                <h2 id="popular-modal-title" style={{ color: "var(--color-brand-primary)", fontWeight: 700 }}>Kelola Game Populer</h2>
                <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.8rem" }}>{draft.length}/{MAX_POPULAR_GAMES} game dipilih</p>
              </div>
              <button type="button" aria-label="Tutup" onClick={() => setModalOpen(false)}><X size={20} /></button>
            </div>
            <label className="mb-4 flex items-center gap-2 rounded-lg px-3 py-2.5" style={{ border: "1px solid var(--color-border-input)" }}>
              <Search size={16} style={{ color: "var(--color-brand-secondary)" }} />
              <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Cari nama game" className="min-w-0 flex-1 bg-transparent outline-none" />
            </label>
            <div className="grid flex-1 grid-cols-1 gap-2 overflow-y-auto sm:grid-cols-2">
              {filtered.length === 0 ? (
                <p className="col-span-full py-10 text-center" style={{ color: "var(--color-brand-secondary)" }}>
                  Tidak ada game yang cocok.
                </p>
              ) : filtered.map((game) => {
                const slug = game.brandSlug;
                const checked = draft.includes(slug);
                return (
                  <label key={game.id} className="flex cursor-pointer items-center gap-3 rounded-lg p-3" style={{ border: `1px solid ${checked ? "var(--color-brand-tertiary)" : "var(--color-border-light)"}`, backgroundColor: checked ? "var(--color-hover-surface)" : "transparent" }}>
                    <input type="checkbox" checked={checked} onChange={() => toggle(game)} />
                    <GameImage name={game.brand} imageUrl={game.imageUrl} />
                    <span className="min-w-0 truncate" style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.86rem" }}>{game.brand}</span>
                  </label>
                );
              })}
            </div>
            <button type="button" onClick={() => saveMutation.mutate(draft)} disabled={saveMutation.isPending} className="mt-5 flex items-center justify-center gap-2 rounded-lg py-3" style={{ backgroundColor: "var(--color-brand-tertiary)", color: "var(--color-on-primary)", fontWeight: 600 }}>
              {saveMutation.isPending ? <Loader2 className="animate-spin" size={16} /> : <Save size={16} />}
              {saveMutation.isPending ? "Menyimpan..." : "Simpan Daftar"}
            </button>
          </div>
        </div>
      )}
    </AdminLayout>
  );
}

function PopularRow({ game, index, deleting, onDelete }: { game: BrandItem; index: number; deleting: boolean; onDelete: () => void }) {
  return (
    <tr style={{ borderBottom: "1px solid var(--color-border-lighter)" }}>
      <td className="px-5 py-4" style={{ color: "var(--color-brand-secondary)" }}>{index + 1}</td>
      <td className="px-5 py-3"><GameImage name={game.brand} imageUrl={game.imageUrl} /></td>
      <td className="px-5 py-4" style={{ color: "var(--color-brand-primary)", fontWeight: 600 }}>{game.brand}</td>
      <td className="px-5 py-4">
        <button type="button" onClick={onDelete} disabled={deleting} aria-label={`Hapus ${game.brand}`} title="Hapus dari Populer" className="flex h-9 w-9 items-center justify-center rounded-md" style={{ color: "var(--color-danger)", backgroundColor: "color-mix(in srgb, var(--color-danger) 10%, transparent)" }}>
          {deleting ? <Loader2 className="animate-spin" size={15} /> : <Trash2 size={15} />}
        </button>
      </td>
    </tr>
  );
}

function GameImage({ name, imageUrl }: { name: string; imageUrl: string | null }) {
  return imageUrl ? (
    <img src={imageUrl} alt="" className="h-12 w-16 rounded object-cover" />
  ) : (
    <span className="flex h-12 w-16 items-center justify-center rounded" style={{ backgroundColor: "var(--color-neutral)", color: "var(--color-brand-secondary)" }}><Gamepad2 size={20} aria-label={name} /></span>
  );
}
