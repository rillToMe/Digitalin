using be_digitalin.Data;
using be_digitalin.DTOs;
using be_digitalin.Models;
using be_digitalin.Constants;
using be_digitalin.Providers;
using be_digitalin.Services.GameTemplates;
using be_digitalin.Infrastructure.Serialization;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services;

public class ProductService : IProductService
{
    private readonly DigitalinDbContext _db;
    private readonly CloudflareR2Provider _r2Provider;
    private readonly DigiflazzProvider _digiflazzProvider;

    public ProductService(
        DigitalinDbContext db, 
        CloudflareR2Provider r2Provider, 
        DigiflazzProvider digiflazzProvider)
    {
        _db = db;
        _r2Provider = r2Provider;
        _digiflazzProvider = digiflazzProvider;
    }
    
    public async Task<IReadOnlyList<GameMetadataResponse>>
        GetGameMetadataAsync()
    {
        var metadata = await _db.GameMetadata
            .AsNoTracking()
            .OrderBy(m => m.Brand)
            .ToListAsync();

        return metadata
            .Select(MapMetadataResponse)
            .ToList();
    }

    public async Task<GameMetadataResponse?>
        GetGameMetadataByIdAsync(int id)
    {
        var metadata = await _db.GameMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        return metadata == null
            ? null
            : MapMetadataResponse(metadata);
    }

    public async Task<GameMetadataResponse?>
        UpdateGameMetadataAsync(
            int id,
            UpdateGameMetadataRequest request)
    {
        var metadata = await _db.GameMetadata
            .FirstOrDefaultAsync(m => m.Id == id);

        if (metadata == null)
            return null;

        var normalizedFields = NormalizeAndValidateInputFields(
            request.InputFields);

        var normalizedTargetTemplate = ValidateTargetTemplate(
            request.TargetTemplate,
            normalizedFields);

        metadata.InputSchemaJson = GameInputSchemaSerializer.Serialize(normalizedFields);
        metadata.TargetTemplate = normalizedTargetTemplate;

        await _db.SaveChangesAsync();

        return MapMetadataResponse(metadata);
    }
    
    // logic sync
    public async Task<(int Added, int Updated, int Deactivated)> SyncCatalogFromDigiflazzAsync()
    {
        var dataArray = await _digiflazzProvider.GetPriceListAsync();

        int added = 0;
        int updated = 0;
        int deactivated = 0;

        var activeDigiflazzSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── Load katalog sekali ke memori (hindari N+1 di dalam loop) ──
        // Brand di-lookup case-insensitive agar sepadan dengan kolasi default MySQL.
        var existingBrands = await _db.GameMetadata
            .Select(m => m.Brand)
            .ToListAsync();
        var knownBrands = new HashSet<string>(existingBrands, StringComparer.OrdinalIgnoreCase);

        // SKU unik → lookup per item cukup dari dictionary (bukan query).
        var products = await _db.GameProducts.ToListAsync();
        var productsBySku = products
            .Where(p => !string.IsNullOrEmpty(p.BuyerSkuCode))
            .GroupBy(p => p.BuyerSkuCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in dataArray)
        {
            var category = item
                .GetProperty(AppConstants.DigiflazzResponseKeys.Category)
                .GetString();

            if (category != AppConstants.Categories.Games)
                continue;

            var sku = item
                .GetProperty(AppConstants.DigiflazzResponseKeys.BuyerSkuCode)
                .GetString() ?? string.Empty;

            var name = item
                .GetProperty(AppConstants.DigiflazzResponseKeys.ProductName)
                .GetString() ?? string.Empty;

            var modalPrice = item
                .GetProperty(AppConstants.DigiflazzResponseKeys.Price)
                .GetDecimal();

            var brand = item
                .GetProperty(AppConstants.DigiflazzResponseKeys.Brand)
                .GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(sku))
                continue;

            activeDigiflazzSkus.Add(sku);

            if (!knownBrands.Contains(brand))
            {
                _db.GameMetadata.Add(new GameMetadata
                {
                    Brand = brand,
                    InputSchemaJson = GameInputSchemaSerializer.Serialize(
                        GameTemplateFactory.CreateDefaultInputFields()),
                    TargetTemplate = GameTemplateFactory.DefaultTargetTemplate
                });

                // Tandai agar SKU lain dengan brand sama tidak menambah metadata duplikat.
                knownBrands.Add(brand);
            }

            if (productsBySku.TryGetValue(sku, out var existingProduct))
            {
                var margin = existingProduct.CustomMargin ?? 0m;
                var sellingPrice = modalPrice + margin;

                var hasChanges =
                    existingProduct.Price != sellingPrice ||
                    existingProduct.Name != name ||
                    existingProduct.Brand != brand ||
                    existingProduct.Category != category ||
                    !existingProduct.IsActive;

                if (hasChanges)
                {
                    existingProduct.Price = sellingPrice;
                    existingProduct.Name = name;
                    existingProduct.Brand = brand;
                    existingProduct.Category = category;
                    existingProduct.GameCode = brand;
                    existingProduct.IsActive = true;

                    updated++;
                }
            }
            else
            {
                var newProduct = new GameProduct
                {
                    BuyerSkuCode = sku,
                    Name = name,
                    Price = modalPrice,
                    Brand = brand,
                    Category = category,
                    GameCode = brand,
                    IsActive = true
                };

                _db.GameProducts.Add(newProduct);

                // Tandai agar SKU duplikat dalam price-list tidak menambah produk ganda.
                productsBySku[sku] = newProduct;

                added++;
            }
        }

        if (activeDigiflazzSkus.Count == 0)
        {
            throw new InvalidOperationException(
                "Sync dibatalkan: Digiflazz tidak mengembalikan produk kategori Games. " +
                "Produk lama tidak dinonaktifkan untuk mencegah kehilangan katalog."
            );
        }

        var productsToDeactivate = await _db.GameProducts
            .Where(p =>
                p.Category == AppConstants.Categories.Games &&
                p.IsActive &&
                !string.IsNullOrEmpty(p.BuyerSkuCode) &&
                !activeDigiflazzSkus.Contains(p.BuyerSkuCode))
            .ToListAsync();

        foreach (var product in productsToDeactivate)
        {
            product.IsActive = false;
            deactivated++;
        }

        await _db.SaveChangesAsync();

        return (added, updated, deactivated);
    }

    public async Task<IEnumerable<ProductResponse>> GetAllProductsAsync()
    {
        return await _db.GameProducts
            .Select(p => new ProductResponse(p.Id, p.Name, p.Price, p.GameCode))
            .ToListAsync();
    }
    
    // HOMEPAGE: Menampilkan Brand beserta Instruksi Form UI-nya
    public async Task<IEnumerable<CategoryInfo>> GetUniqueGameBrandsAsync()
    {
        var games = await _db.GameMetadata
            .AsNoTracking()
            .Where(metadata =>
                _db.GameProducts.Any(product =>
                    product.Brand == metadata.Brand &&
                    product.Category == AppConstants.Categories.Games &&
                    product.IsActive))
            .OrderBy(metadata => metadata.Brand)
            .Select(metadata => new CategoryInfo(
                metadata.Brand,
                metadata.Brand.ToLower().Replace(" ", "-"),
                metadata.ImageUrl,
                AppConstants.Categories.Games
            ))
            .ToListAsync();

        return games;
    }
    
    public async Task<GameDetailResponse?> GetGameDetailAsync(string brandSlug)
    {
        var metadata = await _db.GameMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.Brand.ToLower().Replace(" ", "-") == brandSlug.ToLower());

        if (metadata == null)
            return null;

        var products = await _db.GameProducts
            .AsNoTracking()
            .Where(p =>
                p.Brand == metadata.Brand &&
                p.Category == AppConstants.Categories.Games &&
                p.IsActive)
            .OrderBy(p => p.Price)
            .Select(p => new GameProductItemResponse(
                p.Id,
                p.Name,
                p.Price,
                p.GameCode,
                p.Brand,
                p.Category,
                p.ImageUrl,
                p.CustomMargin
            ))
            .ToListAsync();

        return new GameDetailResponse(
            metadata.Brand,
            metadata.Brand.ToLower().Replace(" ", "-"),
            metadata.ImageUrl,
            GameInputSchemaSerializer.Deserialize(metadata.InputSchemaJson),
            products
        );
    }
    
    
    public async Task<IEnumerable<GameProduct>> GetProductsByBrandAsync(string brand)
    {
        var dbBrandName = brand.Replace("-", " ").ToLower();
        return await _db.GameProducts
            .Where(p => p.Brand.ToLower() == dbBrandName)
            .OrderBy(p => p.Price) 
            .ToListAsync();
    }
    
    public async Task<PagedResult<GameProduct>> GetPagedProductsAsync(
        string? search,
        string? brand,
        int page,
        int limit)
    {
        var query = _db.GameProducts
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(brand))
        {
            var normalizedBrand = brand.Trim();

            query = query.Where(product =>
                product.Brand == normalizedBrand);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();

            query = query.Where(product =>
                product.Name.Contains(normalizedSearch) ||
                product.Brand.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(product => product.Price)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(
            totalCount / (double)limit);

        return new PagedResult<GameProduct>(
            items,
            totalCount,
            totalPages,
            page);
    }

    public async Task<GameProduct> CreateProductAsync(GameProduct product)
    {
        _db.GameProducts.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<string?> UpdateProductIconAsync(int productId, IFormFile file)
    {
        var product = await _db.GameProducts.FindAsync(productId);
        if (product == null) return null;

        if (!string.IsNullOrEmpty(product.ImageUrl))
        {
            await _r2Provider.DeleteFileAsync(product.ImageUrl);
        }

        var newImageUrl = await _r2Provider.UploadFileAsync(file, "icon-item");

        product.ImageUrl = newImageUrl;
        await _db.SaveChangesAsync();

        return newImageUrl;
    }

    public async Task<int> CleanDummyProductsAsync()
    {
        var dummyProducts = await _db.GameProducts
            .Where(p => string.IsNullOrEmpty(p.BuyerSkuCode))
            .ToListAsync();

        if (dummyProducts.Count == 0)
            return 0;

        int count = dummyProducts.Count;
        _db.GameProducts.RemoveRange(dummyProducts);
        await _db.SaveChangesAsync();

        return count;
    }

    public async Task<bool> UpdateProductMarginAsync(int productId, decimal? margin)
    {
        var product = await _db.GameProducts.FindAsync(productId);
        if (product == null) return false;

        decimal oldMargin = product.CustomMargin ?? 0m;
        decimal modalPrice = product.Price - oldMargin;

        decimal newMargin = margin ?? 0m;

        product.Price = modalPrice + newMargin;
        product.CustomMargin = margin;
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<int> GetMidtransExpiryAsync()
    {
        var setting = await _db.AppSettings
            .FirstOrDefaultAsync(s => s.Key == AppConstants.SystemSettings.MidtransExpiry);

        return setting != null ? int.Parse(setting.Value) : 5; // default 5 menit
    }

    public async Task SetMidtransExpiryAsync(int expiryMinutes)
    {
        var setting = await _db.AppSettings
            .FirstOrDefaultAsync(s => s.Key == AppConstants.SystemSettings.MidtransExpiry);

        if (setting == null)
        {
            _db.AppSettings.Add(new AppSetting
            {
                Key = AppConstants.SystemSettings.MidtransExpiry,
                Value = expiryMinutes.ToString()
            });
        }
        else
        {
            setting.Value = expiryMinutes.ToString();
        }

        await _db.SaveChangesAsync();
    }

    public async Task<string?> UpdateGameCoverAsync(int metadataId, IFormFile file)
    {
        var meta = await _db.GameMetadata.FindAsync(metadataId);
        if (meta == null) return null;

        // hapus gambar lama
        if (!string.IsNullOrEmpty(meta.ImageUrl))
        {
            await _r2Provider.DeleteFileAsync(meta.ImageUrl);
        }

        var imageUrl = await _r2Provider.UploadFileAsync(file, "icon-cover");

        meta.ImageUrl = imageUrl;
        await _db.SaveChangesAsync();

        return imageUrl;
    }

    private static GameMetadataResponse MapMetadataResponse(
        GameMetadata metadata)
    {
        return new GameMetadataResponse(
            metadata.Id,
            metadata.Brand,
            metadata.ImageUrl,
            GameInputSchemaSerializer.Deserialize(metadata.InputSchemaJson),
            metadata.TargetTemplate
        );
    }

    private static List<GameInputField> NormalizeAndValidateInputFields(
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
    
    private static GameInputField NormalizeInputField(
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
    
    private static string NormalizeFieldKey(string key)
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
    
    private static string ValidateTargetTemplate(
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

        var unknownKey = templateKeys
            .FirstOrDefault(key => !availableKeys.Contains(key));

        if (unknownKey != null)
        {
            throw new InvalidOperationException(
                $"Target template menggunakan key '{unknownKey}' yang tidak tersedia.");
        }

        return normalizedTemplate;
    }
    
    private static HashSet<string> ExtractTemplateKeys(
        string targetTemplate)
    {
        var keys = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

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
                var key = targetTemplate.Substring(
                    startIndex,
                    keyLength);

                keys.Add(key);
            }

            startIndex = -1;
        }

        return keys;
    }
    
}