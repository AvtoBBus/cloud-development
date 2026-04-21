using Amazon.S3;
using Amazon.S3.Model;

namespace CompanyEmployees.FileService.Services;

public class MinioInitializer(IAmazonS3 s3Client, ILogger<MinioInitializer> logger, IConfiguration configuration) : BackgroundService
{

    private readonly string _bucketName = configuration["MinIO:BucketName"] ?? "company-employee";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = _bucketName }, stoppingToken);
                logger.LogInformation("Minio bucket '{BucketName}' ready", _bucketName);
                return;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                logger.LogInformation("Minio bucket '{BucketName}' already exists", _bucketName);
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