using System.Security.Cryptography;
using be_digitalin.Data;
using be_digitalin.Models;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services;

public class OtpService : IOtpService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    private readonly DigitalinDbContext _db;

    public OtpService(DigitalinDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<string> GenerateAndSaveOtpAsync(int customerId, string purpose, TimeSpan? ttl = null)
    {
        // Generate a cryptographically-secure 6-digit OTP (100000–999999)
        int otpNumber = RandomNumberGenerator.GetInt32(100_000, 1_000_000);
        string otpCode = otpNumber.ToString("D6");

        var effectiveTtl = ttl ?? DefaultTtl;

        // Upsert: remove any existing OTP for the same customer+purpose before inserting a new one.
        // This guarantees only one active OTP per (customerId, purpose) at any time.
        var existing = await _db.CustomerOtps
            .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.Purpose == purpose);

        if (existing != null)
        {
            _db.CustomerOtps.Remove(existing);
        }

        _db.CustomerOtps.Add(new CustomerOtp
        {
            CustomerId = customerId,
            Purpose    = purpose,
            OtpCode    = otpCode,
            ExpiresAt  = DateTime.UtcNow.Add(effectiveTtl),
            CreatedAt  = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return otpCode;
    }

    /// <inheritdoc />
    public async Task<bool> ValidateAndConsumeOtpAsync(int customerId, string purpose, string otpCode)
    {
        var record = await _db.CustomerOtps
            .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.Purpose == purpose);

        if (record == null) return false;

        // Always remove the record after a validation attempt to prevent brute-force reuse.
        // (even on failure, to avoid leaking validity through timing)
        bool isMatch   = record.OtpCode == otpCode;
        bool isExpired = record.ExpiresAt < DateTime.UtcNow;

        _db.CustomerOtps.Remove(record);
        await _db.SaveChangesAsync();

        return isMatch && !isExpired;
    }
}
