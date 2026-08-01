import { Plus, Trash2 } from "lucide-react";
import type {
  GameInputField,
  GameInputFieldType,
  UpdateMetadataPayload,
} from "../../../api/index";

interface DynamicInputFieldsEditorProps {
  form: UpdateMetadataPayload;
  setForm: (form: UpdateMetadataPayload) => void;
}

const createFieldKey = (
  label: string,
  index: number
): string => {
  const normalized = label
    .trim()
    .replace(/[^a-zA-Z0-9\s]/g, "")
    .replace(/\s+(.)/g, (_, character: string) =>
      character.toUpperCase()
    )
    .replace(/\s/g, "");

  if (!normalized) {
    return `field${index + 1}`;
  }

  return (
    normalized.charAt(0).toLowerCase() +
    normalized.slice(1)
  );
};

export function DynamicInputFieldsEditor({
  form,
  setForm,
}: DynamicInputFieldsEditorProps) {
  const updateInputField = (
    index: number,
    updates: Partial<GameInputField>
  ) => {
    setForm({
      ...form,
      inputFields: form.inputFields.map(
        (field, fieldIndex) =>
          fieldIndex === index
            ? { ...field, ...updates }
            : field
      ),
    });
  };

  const addInputField = () => {
    const nextIndex = form.inputFields.length;

    const newField: GameInputField = {
      key: `field${nextIndex + 1}`,
      label: "",
      placeholder: "",
      type: "text",
      required: true,
      order: nextIndex + 1,
      options: [],
    };

    setForm({
      ...form,
      inputFields: [
        ...form.inputFields,
        newField,
      ],
    });
  };

  const removeInputField = (index: number) => {
    const nextFields = form.inputFields
      .filter(
        (_, fieldIndex) => fieldIndex !== index
      )
      .map((field, fieldIndex) => ({
        ...field,
        order: fieldIndex + 1,
      }));

    setForm({
      ...form,
      inputFields: nextFields,
    });
  };

  const updateFieldLabel = (
    index: number,
    label: string
  ) => {
    const field = form.inputFields[index];

    const placeholder =
      field.type === "select"
        ? `Pilih ${label}`
        : `Masukkan ${label}`;

    updateInputField(index, {
      label,
      key: createFieldKey(label, index),
      placeholder,
    });
  };

  const updateFieldType = (
    index: number,
    type: GameInputFieldType
  ) => {
    const field = form.inputFields[index];

    updateInputField(index, {
      type,
      placeholder:
        type === "select"
          ? `Pilih ${field.label}`
          : `Masukkan ${field.label}`,
      options:
        type === "select"
          ? field.options
          : [],
    });
  };

  const addOption = (index: number) => {
    const field = form.inputFields[index];

    updateInputField(index, {
      options: [
        ...field.options,
        "",
      ],
    });
  };

  const updateOption = (
    fieldIndex: number,
    optionIndex: number,
    value: string
  ) => {
    const field = form.inputFields[fieldIndex];

    updateInputField(fieldIndex, {
      options: field.options.map(
        (option, currentOptionIndex) =>
          currentOptionIndex === optionIndex
            ? value
            : option
      ),
    });
  };

  const removeOption = (
    fieldIndex: number,
    optionIndex: number
  ) => {
    const field = form.inputFields[fieldIndex];

    updateInputField(fieldIndex, {
      options: field.options.filter(
        (_, currentOptionIndex) =>
          currentOptionIndex !== optionIndex
      ),
    });
  };

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h4
            style={{
              color: "var(--color-brand-primary)",
              fontSize: "0.9rem",
              fontWeight: 600,
            }}
          >
            Informasi yang Diisi Customer
          </h4>

          <p
            style={{
              color: "var(--color-brand-secondary)",
              fontSize: "0.75rem",
              marginTop: 2,
            }}
          >
            Tambahkan data yang dibutuhkan untuk top up.
          </p>
        </div>

        <button
          type="button"
          onClick={addInputField}
          className="flex items-center gap-1.5 px-3 py-2 rounded-xl"
          style={{
            backgroundColor:
              "var(--color-brand-tertiary)",
            color:
              "var(--color-text-on-primary)",
            fontSize: "0.8rem",
            fontWeight: 600,
          }}
        >
          <Plus size={14} />
          Tambah
        </button>
      </div>

      <div className="space-y-3">
        {form.inputFields.map((field, index) => (
          <div
            key={index}
            className="rounded-xl p-4 space-y-4"
            style={{
              border:
                "1px solid var(--color-border-lighter)",
            }}
          >
            <div className="flex items-center justify-between">
              <span
                style={{
                  color:
                    "var(--color-brand-primary)",
                  fontSize: "0.85rem",
                  fontWeight: 600,
                }}
              >
                Input {index + 1}
              </span>

              <button
                type="button"
                onClick={() =>
                  removeInputField(index)
                }
                aria-label={`Hapus input ${index + 1}`}
                style={{
                  color: "var(--color-danger)",
                }}
              >
                <Trash2 size={16} />
              </button>
            </div>

            <div>
              <label
                style={{
                  color:
                    "var(--color-brand-primary)",
                  fontSize: "0.8rem",
                  fontWeight: 500,
                  display: "block",
                  marginBottom: 6,
                }}
              >
                Nama Input
              </label>

              <input
                value={field.label}
                onChange={(event) =>
                  updateFieldLabel(
                    index,
                    event.target.value
                  )
                }
                placeholder="Contoh: User ID"
                className="w-full px-3 py-2.5 rounded-xl outline-none"
                style={{
                  border:
                    "1px solid var(--color-border-lighter)",
                }}
              />
            </div>

            <div>
              <label
                style={{
                  color:
                    "var(--color-brand-primary)",
                  fontSize: "0.8rem",
                  fontWeight: 500,
                  display: "block",
                  marginBottom: 6,
                }}
              >
                Jenis Input
              </label>

              <select
                value={field.type}
                onChange={(event) =>
                  updateFieldType(
                    index,
                    event.target
                      .value as GameInputFieldType
                  )
                }
                className="w-full px-3 py-2.5 rounded-xl outline-none"
                style={{
                  border:
                    "1px solid var(--color-border-lighter)",
                }}
              >
                <option value="text">
                  Teks bebas
                </option>

                <option value="number">
                  Angka saja
                </option>

                <option value="select">
                  Pilihan / Dropdown
                </option>
              </select>
            </div>

            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={field.required}
                onChange={(event) =>
                  updateInputField(index, {
                    required:
                      event.target.checked,
                  })
                }
              />

              <span
                style={{
                  color:
                    "var(--color-brand-primary)",
                  fontSize: "0.85rem",
                }}
              >
                Wajib diisi customer
              </span>
            </label>

            {field.type === "select" && (
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <label
                    style={{
                      color:
                        "var(--color-brand-primary)",
                      fontSize: "0.8rem",
                      fontWeight: 500,
                    }}
                  >
                    Daftar Pilihan
                  </label>

                  <button
                    type="button"
                    onClick={() => addOption(index)}
                    className="flex items-center gap-1"
                    style={{
                      color:
                        "var(--color-brand-tertiary)",
                      fontSize: "0.8rem",
                      fontWeight: 600,
                    }}
                  >
                    <Plus size={13} />
                    Tambah Pilihan
                  </button>
                </div>

                {field.options.map(
                  (option, optionIndex) => (
                    <div
                      key={optionIndex}
                      className="flex items-center gap-2"
                    >
                      <input
                        value={option}
                        onChange={(event) =>
                          updateOption(
                            index,
                            optionIndex,
                            event.target.value
                          )
                        }
                        placeholder={`Pilihan ${
                          optionIndex + 1
                        }`}
                        className="flex-1 px-3 py-2.5 rounded-xl outline-none"
                        style={{
                          border:
                            "1px solid var(--color-border-lighter)",
                        }}
                      />

                      <button
                        type="button"
                        onClick={() =>
                          removeOption(
                            index,
                            optionIndex
                          )
                        }
                        aria-label={`Hapus pilihan ${
                          optionIndex + 1
                        }`}
                        style={{
                          color:
                            "var(--color-danger)",
                        }}
                      >
                        <Trash2 size={15} />
                      </button>
                    </div>
                  )
                )}

                {field.options.length === 0 && (
                  <p
                    style={{
                      color:
                        "var(--color-brand-secondary)",
                      fontSize: "0.75rem",
                    }}
                  >
                    Belum ada pilihan.
                  </p>
                )}
              </div>
            )}
          </div>
        ))}

        {form.inputFields.length === 0 && (
          <div
            className="rounded-xl p-5 text-center"
            style={{
              border:
                "1px dashed var(--color-border-lighter)",
            }}
          >
            <p
              style={{
                color:
                  "var(--color-brand-secondary)",
                fontSize: "0.85rem",
              }}
            >
              Belum ada input untuk game ini.
            </p>
          </div>
        )}
      </div>
    </div>
  );
}