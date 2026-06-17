using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.S3.Model;
using be_digitalin.Config;
using Microsoft.Extensions.Options;

namespace be_digitalin.Providers; 

public class CloudflareR2Provider 
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _publicUrl;

    public CloudflareR2Provider(IOptions<CloudflareR2Settings> settings)
    {
        var config = settings.Value;

        var s3Config = new AmazonS3Config 
        { 
            ServiceURL = $"https://{config.AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        };
        
        _s3Client = new AmazonS3Client(config.AccessKey, config.SecretKey, s3Config);
        _bucketName = config.BucketName;
        _publicUrl = config.PublicUrl;
    }

    public async Task<string> UploadFileAsync(IFormFile file, string folder)
    {
        var fileName = $"{folder}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        
        using var newStream = new MemoryStream();
        await file.CopyToAsync(newStream);

        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = newStream,
            Key = fileName,
            BucketName = _bucketName,
            ContentType = file.ContentType,
            DisablePayloadSigning = true
        };
        
        var fileTransferUtility = new TransferUtility(_s3Client);
        await fileTransferUtility.UploadAsync(uploadRequest);

        return $"{_publicUrl}/{fileName}";
    }
    
    public async Task<bool> DeleteFileAsync(string fileUrl)
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

            await _s3Client.DeleteObjectAsync(deleteRequest);
            Console.WriteLine($"[R2] File {objectKey} berhasil dihapus dari cloud.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[R2 Error] Gagal menghapus file: {ex.Message}");
            return false;
        }
    }
}