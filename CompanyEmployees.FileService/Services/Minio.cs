using Amazon.S3;
using Amazon.S3.Model;
using CompanyEmployees.FileService.Configuration;
using Microsoft.Extensions.Options;

namespace CompanyEmployees.FileService.Services;

public class MinioInitializer(IAmazonS3 s3Client, ILogger<MinioInitializer> logger, IOptions<MinioConfiguration> configuration) : BackgroundService
{

    private MinioConfiguration _configuration = configuration.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = _configuration.BucketName }, stoppingToken);
                logger.LogInformation("Minio bucket '{BucketName}' ready", _configuration.BucketName);
                return;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                logger.LogInformation("Minio bucket '{BucketName}' already exists", _configuration.BucketName);
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Failed to initialize Minio bucket, retrying in 3 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}