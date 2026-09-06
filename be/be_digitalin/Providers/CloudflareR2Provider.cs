using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.S3.Model;
using be_digitalin.Application.Ports;
using be_digitalin.Application.Validation;
using be_digitalin.Config;
using Microsoft.Extensions.Options;

namespace be_digitalin.Providers; 

public class CloudflareR2Provider : ICloudflareR2Provider
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _publicUrl;
    private readonly ILogger<CloudflareR2Provider> _logger;

    public CloudflareR2Provider(IOptions<CloudflareR2Settings> settings, ILogger<CloudflareR2Provider> logger)
    {
        var config = settings.Value;
        _logger = logger;

        var s3Config = new AmazonS3Config 
        { 
            ServiceURL = $"https://{config.AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        };
        
        _s3Client = new AmazonS3Client(config.AccessKey, config.SecretKey, s3Config);
        _bucketName = config.BucketName;
        _publicUrl = config.PublicUrl;
    }

    public async Task<string> UploadFileAsync(ImageFileData file, string folder, CancellationToken cancellationToken = default)
    {
        var fileName = $"{folder}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

        using var newStream = new MemoryStream();
        if (file.Content.CanSeek) file.Content.Position = 0;
        await file.Content.CopyToAsync(newStream, cancellationToken);

        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = newStream,
            Key = fileName,
            BucketName = _bucketName,
            ContentType = file.ContentType,
            DisablePayloadSigning = true
        };

        var fileTransferUtility = new TransferUtility(_s3Client);
        await fileTransferUtility.UploadAsync(uploadRequest, cancellationToken);

        return $"{_publicUrl}/{fileName}";
    }

    public async Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(fileUrl)) return false;

            var uri = new Uri(fileUrl);
            var objectKey = uri.AbsolutePath.TrimStart('/');

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);
            _logger.LogInformation("[R2] File {ObjectKey} berhasil dihapus dari cloud.", objectKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[R2] Gagal menghapus file {FileUrl}.", fileUrl);
            return false;
        }
    }
}