using be_digitalin.Constants;
using be_digitalin.Domain.Entities;

namespace be_digitalin.Domain.Products;

/// <summary>
/// Aturan domain murni untuk normalisasi dan validasi skema input pelanggan
/// (GameInputField) serta keselarasan placeholder pada TargetTemplate.
/// </summary>
public static class InputSchemaRules
{
    public static List<GameInputField> NormalizeAndValidateInputFields(
        IReadOnlyList<GameInputField>? inputFields)
    {
        if (inputFields == null || inputFields.Count == 0)
        {
            throw new InvalidOperationException(
                "Game harus memiliki minimal satu field input.");
        }

        var normalizedFields = inputFields
            .OrderBy(field => field.Order)
            .Select((field, index) => NormalizeInputField(field, index + 1))
            .ToList();

        var duplicateKey = normalizedFields
            .GroupBy(
                field => field.Key,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateKey != null)
        {
            throw new InvalidOperationException(
                $"Key input '{duplicateKey.Key}' tidak boleh duplikat.");
        }

        return normalizedFields;
    }

    public static GameInputField NormalizeInputField(
        GameInputField field,
        int order)
    {
        var key = NormalizeFieldKey(field.Key);

        var label = field.Label?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new InvalidOperationException(
                $"Label untuk field '{key}' wajib diisi.");
        }

        var type = field.Type?
                       .Trim()
                       .ToLowerInvariant()
                   ?? string.Empty;

        if (type != AppConstants.GameInputFieldTypes.Text &&
            type != AppConstants.GameInputFieldTypes.Number &&
            type != AppConstants.GameInputFieldTypes.Select)
        {
            throw new InvalidOperationException(
                $"Tipe input '{field.Type}' tidak didukung.");
        }

        var options = (field.Options ?? [])
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (type == AppConstants.GameInputFieldTypes.Select &&
            options.Count == 0)
        {
            throw new InvalidOperationException(
                $"Field select '{label}' harus memiliki minimal satu pilihan.");
        }

        if (type != AppConstants.GameInputFieldTypes.Select)
        {
            options = [];
        }

        return new GameInputField
        {
            Key = key,
            Label = label,
            Placeholder = field.Placeholder?.Trim() ?? string.Empty,
            Type = type,
            Required = field.Required,
            Order = order,
            Options = options
        };
    }

    public static string NormalizeFieldKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Key input wajib diisi.");
        }

        var trimmedKey = key.Trim();

        if (!char.IsLetter(trimmedKey[0]))
        {
            throw new InvalidOperationException(
                $"Key '{trimmedKey}' harus diawali huruf.");
        }

        if (!trimmedKey.All(character =>
                char.IsLetterOrDigit(character) ||
                character == '_'))
        {
            throw new InvalidOperationException(
                $"Key '{trimmedKey}' hanya boleh berisi huruf, angka, dan underscore.");
        }

        return char.ToLowerInvariant(trimmedKey[0]) +
               trimmedKey[1..];
    }

    public static string ValidateTargetTemplate(
        string targetTemplate,
        IReadOnlyList<GameInputField> inputFields)
    {
        if (string.IsNullOrWhiteSpace(targetTemplate))
        {
            throw new InvalidOperationException(
                "Target template wajib diisi.");
        }

        var normalizedTemplate = targetTemplate.Trim();

        var templateKeys = ExtractTemplateKeys(normalizedTemplate);

        if (templateKeys.Count == 0)
        {
            throw new InvalidOperationException(
                "Target template harus menggunakan minimal satu input field.");
        }

        var availableKeys = inputFields
            .Select(field => field.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Arah 1: setiap placeholder di template harus ada di schema.
        var unknownKey = templateKeys
            .FirstOrDefault(key => !availableKeys.Contains(key));

        if (unknownKey != null)
        {
            throw new InvalidOperationException(
                $"Placeholder '{unknownKey}' pada target template tidak dikenali.");
        }

        // Arah 2: setiap key di schema wajib direferensikan template — cegah drift
        // yang membuat field (mis. zoneId) diabaikan diam-diam saat render customer_no.
        var missingKey = inputFields
            .Select(field => field.Key)
            .FirstOrDefault(key => !templateKeys.Contains(key));

        if (missingKey != null)
        {
            throw new InvalidOperationException(
                $"Target template belum mereferensikan input key '{missingKey}' yang wajib.");
        }

        return normalizedTemplate;
    }

    public static HashSet<string> ExtractTemplateKeys(string targetTemplate)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var startIndex = -1;

        for (var index = 0; index < targetTemplate.Length; index++)
        {
            if (targetTemplate[index] == '{')
            {
                startIndex = index + 1;
                continue;
            }

            if (targetTemplate[index] != '}' || startIndex < 0)
                continue;

            var keyLength = index - startIndex;
            if (keyLength > 0)
            {
                var key = targetTemplate.Substring(startIndex, keyLength);
                keys.Add(key);
            }

            startIndex = -1;
        }

        return keys;
    }
}
