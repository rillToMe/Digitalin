import { useQuery } from "@tanstack/react-query";
import { Sparkles } from "lucide-react";
import { adminService } from "../../../../services/api";
import type {
  GameTemplate,
  UpdateMetadataPayload,
} from "../../../../api/index";

interface PresetTemplatePickerProps {
  form: UpdateMetadataPayload;
  setForm: (form: UpdateMetadataPayload) => void;
}

export function PresetTemplatePicker({
  form,
  setForm,
}: PresetTemplatePickerProps) {
  const { data: templates = [], isLoading } = useQuery({
    queryKey: ["admin-game-templates"],
    queryFn: adminService.getGameTemplates,
    staleTime: Infinity,
  });

  const applyTemplate = (template: GameTemplate) => {
    // Preset = starting point. Admin can still edit manually afterwards.
    setForm({
      ...form,
      targetTemplate: template.targetTemplate,
      inputFields: template.inputFields.map((field) => ({
        ...field,
        options: [...field.options],
      })),
    });
  };

  const handleSelect = (
    event: React.ChangeEvent<HTMLSelectElement>
  ) => {
    const template = templates.find(
      (item) => item.type === event.target.value
    );
    if (template) applyTemplate(template);
    // Reset select back to placeholder so the same preset can be re-applied.
    event.target.value = "";
  };

  return (
    <div>
      <label
        htmlFor="preset-template"
        style={{
          color: "var(--color-brand-primary)",
          fontSize: "0.85rem",
          fontWeight: 500,
          display: "flex",
          alignItems: "center",
          gap: 6,
          marginBottom: 6,
        }}
      >
        <Sparkles size={14} style={{ color: "var(--color-brand-tertiary)" }} />
        Pilih Preset Template
      </label>

      <select
        id="preset-template"
        defaultValue=""
        onChange={handleSelect}
        disabled={isLoading}
        className="w-full px-3 py-2.5 rounded-xl outline-none"
        style={{
          border: "1px solid var(--color-border-lighter)",
          color: "var(--color-brand-primary)",
        }}
      >
        <option value="" disabled>
          {isLoading ? "Memuat preset..." : "Pilih preset untuk isi otomatis"}
        </option>
        {templates.map((template) => (
          <option key={template.type} value={template.type}>
            {template.displayName}
          </option>
        ))}
      </select>

      <p
        style={{
          color: "var(--color-brand-secondary)",
          fontSize: "0.75rem",
          marginTop: 6,
        }}
      >
        Preset mengisi input & target template secara otomatis. Tetap bisa diedit manual setelahnya.
      </p>
    </div>
  );
}
