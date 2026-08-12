using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Exceptions;

namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public sealed class DigitalOceanSpacesEvidenceStorageProvider : IEvidenceFileStorageProvider
{
    private readonly EvidenceStorageOptions _options;
    private readonly EvidenceStorageOptionsValidator _validator;

    public DigitalOceanSpacesEvidenceStorageProvider(IOptions<EvidenceStorageOptions> options, EvidenceStorageOptionsValidator validator)
    {
        _options = options.Value;
        _validator = validator;
    }

    public async Task<EvidenceFileUploadResult> UploadAsync(EvidenceFileUploadRequest request, CancellationToken cancellationToken)
    {
        EvidenceStorageConfigurationStatus status = _validator.Validate(_options);
        if (!status.Enabled) throw new EvidenceStorageAppException("Evidence storage is disabled.", "evidence_storage_disabled");
        if (!status.Configured) throw new EvidenceStorageAppException("Evidence storage is not configured.", "evidence_storage_not_configured");

        using AmazonS3Client client = CreateClient();
        var put = new PutObjectRequest { BucketName = _options.Bucket, Key = request.ObjectKey, InputStream = request.Content, ContentType = request.ContentType };
        await client.PutObjectAsync(put, cancellationToken);
        return new EvidenceFileUploadResult(status.ProviderName, _options.Bucket, request.ObjectKey);
    }

    public async Task<EvidenceFileDownloadResult> DownloadAsync(string objectKey, string contentType, CancellationToken cancellationToken)
    {
        EvidenceStorageConfigurationStatus status = _validator.Validate(_options);
        if (!status.Enabled) throw new EvidenceStorageAppException("Evidence storage is disabled.", "evidence_storage_disabled");
        if (!status.Configured) throw new EvidenceStorageAppException("Evidence storage is not configured.", "evidence_storage_not_configured");

        try
        {
            using AmazonS3Client client = CreateClient();
            GetObjectResponse response = await client.GetObjectAsync(_options.Bucket, objectKey, cancellationToken);
            var copy = new MemoryStream();
            await response.ResponseStream.CopyToAsync(copy, cancellationToken);
            copy.Position = 0;
            return new EvidenceFileDownloadResult(copy, string.IsNullOrWhiteSpace(response.Headers.ContentType) ? contentType : response.Headers.ContentType, response.ContentLength);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound || string.Equals(exception.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
        {
            throw new EvidenceStorageAppException("Evidence file is not available.", "evidence_file_not_available", StatusCodes.Status404NotFound);
        }
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        EvidenceStorageConfigurationStatus status = _validator.Validate(_options);
        if (!status.Enabled || !status.Configured) return;
        using AmazonS3Client client = CreateClient();
        await client.DeleteObjectAsync(_options.Bucket, objectKey, cancellationToken);
    }

    private AmazonS3Client CreateClient()
    {
        var config = new AmazonS3Config { ServiceURL = _options.ServiceUrl, ForcePathStyle = _options.UsePathStyle, AuthenticationRegion = _options.Region };
        return new AmazonS3Client(new BasicAWSCredentials(_options.AccessKey, _options.SecretKey), config);
    }
}
