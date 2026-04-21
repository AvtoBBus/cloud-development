using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Xunit;

namespace CompanyEmployees.Tests;

/// <summary>
/// Фикстура, поднимающая AppHost для интеграционных тестов.
/// </summary>
public class Fixture : IAsyncLifetime
{
    public DistributedApplication App { get; private set; } = null!;
    public AmazonS3Client S3Client { get; private set; } = null!;
    
    public string sqsUrl = "";

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CompanyEmployees_AppHost>();

        appHost.Services.ConfigureHttpClientDefaults(http =>
            http.AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(3);
                options.Retry.MaxRetryAttempts = 10;
                options.Retry.Delay = TimeSpan.FromSeconds(3);
            }));

        App = await appHost.BuildAsync();
        await App.StartAsync();

        await Task.WhenAll(
            App.ResourceNotifications.WaitForResourceAsync("minio"),
            App.ResourceNotifications.WaitForResourceAsync("elasticmq"),
            App.ResourceNotifications.WaitForResourceAsync("companyemployees-apigateway"),
            App.ResourceNotifications.WaitForResourceAsync("company-employee-fileservice")
        ).WaitAsync(TimeSpan.FromMinutes(5));

        await Task.Delay(TimeSpan.FromSeconds(5));

        using var minioClient = App.CreateHttpClient("minio", "http");
        var minioUrl = minioClient.BaseAddress!.ToString().TrimEnd('/');


        var sqsHttpClient = App.CreateHttpClient("elasticmq", "http");
        sqsUrl = sqsHttpClient.BaseAddress?.ToString().TrimEnd('/');


        S3Client = new AmazonS3Client(
            new BasicAWSCredentials("minioadmin", "minioadmin"),
            new AmazonS3Config
            {
                ServiceURL = minioUrl,
                ForcePathStyle = true,
                UseHttp = true,
                AuthenticationRegion = "us-east-1"
            });

        var doesExist = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(S3Client, "company-employee");

        if (!doesExist)
        {
            await S3Client.PutBucketAsync(new PutBucketRequest
            {
                BucketName = "company-employee"
            });
        }

    }

    public async Task<List<S3Object>> WaitForS3ObjectAsync(string key, int maxAttempts = 15)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            
            try
            {
                var doesExist = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(S3Client, "company-employee");
                
                var response = await S3Client.ListObjectsV2Async(new ListObjectsV2Request
                    {
                        BucketName = "company-employee",
                        Prefix = key,
                    }
                );

                if (response.S3Objects is not null && response.S3Objects.Count > 0)
                    return response.S3Objects;
            }
            catch (AmazonS3Exception ex) when (ex.Message.Contains("NoSuchBucket"))
            {
                Console.WriteLine($"Bucket not ready yet, attempt {i + 1}/{maxAttempts}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing objects: {ex.Message}");
            }
        }

        return [];
    }

    public async Task DisposeAsync()
    {
        S3Client?.Dispose();

        try
        {
            await App.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException) { }
        catch (OperationCanceledException) { }
    }
}