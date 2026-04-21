using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Aspire.Hosting.Testing;
using CompanyEmployees.Generator.Models;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace CompanyEmployees.Tests;

/// <summary>
/// Интеграционные тесты, проверяющие корректную совместную работу всех сервисов.
/// </summary>
public class IntegrationTests(Fixture fixture) : IClassFixture<Fixture>
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private string _bucketName = "company-employee";

    /// <summary>
    /// Тест для проверки того, что данные генерируются добросовестно
    /// </summary>
    [Fact]
    public async Task GetEmployee_TestValid()
    {
        using var client = fixture.App.CreateHttpClient("companyemployees-apigateway", "http");

        using var response = await client.GetAsync("/employee?id=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var employee = await response.Content.ReadFromJsonAsync<CompanyEmployeeModel>(_jsonOptions);

        Assert.NotNull(employee);
        Assert.Equal(1, employee.Id);
        Assert.False(string.IsNullOrEmpty(employee.FullName));
        Assert.False(string.IsNullOrEmpty(employee.Position));
        Assert.False(string.IsNullOrEmpty(employee.Section));
        Assert.False(string.IsNullOrEmpty(employee.Email));
        Assert.False(string.IsNullOrEmpty(employee.PhoneNumber));
        Assert.True(employee.Salary > 0);

        var isDismissal = employee.Dismissal;

        Assert.True((isDismissal && employee.DismissalDate is not null) || (!isDismissal && employee.DismissalDate is null));
    }

    /// <summary>
    /// Тест для проверки совпадения объектов сгенерированных с одинаковым Id
    /// </summary>
    [Fact]
    public async Task GetEmployee_CheckEmployessWithEqualIDs()
    {
        var randomId = Random.Shared.Next(10, 20);
        using var client = fixture.App.CreateHttpClient("companyemployees-apigateway", "http");

        var employee = await client.GetFromJsonAsync<CompanyEmployeeModel>($"/employee?id={randomId}", _jsonOptions);
        var employeeRepeat = await client.GetFromJsonAsync<CompanyEmployeeModel>($"/employee?id={randomId}", _jsonOptions);

        Assert.NotNull(employee);
        Assert.NotNull(employeeRepeat);

        Assert.Equal(employee.Id, employeeRepeat.Id);
        Assert.Equal(employee.FullName, employeeRepeat.FullName);
        Assert.Equal(employee.Position, employeeRepeat.Position);
        Assert.Equal(employee.Section, employeeRepeat.Section);
        Assert.Equal(employee.Email, employeeRepeat.Email);
        Assert.Equal(employee.PhoneNumber, employeeRepeat.PhoneNumber);
        Assert.Equal(employee.AdmissionDate.ToString(), employeeRepeat.AdmissionDate.ToString());
    }

    /// <summary>
    /// Тест для проверки не совпадения объектов сгенерированных с разными Id
    /// </summary>
    [Fact]
    public async Task GetEmployee_CheckEmployessWithNotEqualIDs()
    {
        using var client = fixture.App.CreateHttpClient("companyemployees-apigateway", "http");

        var employee201 = await client.GetFromJsonAsync<CompanyEmployeeModel>("/employee?id=201", _jsonOptions);
        var employee202 = await client.GetFromJsonAsync<CompanyEmployeeModel>("/employee?id=202", _jsonOptions);

        Assert.NotNull(employee201);
        Assert.NotNull(employee202);
        Assert.Equal(201, employee201.Id);
        Assert.Equal(202, employee202.Id);
        Assert.NotEqual(employee201.FullName, employee202.FullName);
        Assert.NotEqual(employee201.PhoneNumber, employee202.PhoneNumber);
    }

    /// <summary>
    /// Тест для проверки балансировщика
    /// </summary>
    [Fact]
    public async Task GetEmployee_CheckLoadBalancer()
    {
        using var client = fixture.App.CreateHttpClient("companyemployees-apigateway", "http");

        var baseId = Random.Shared.Next(100, 200);
        var tasks = Enumerable.Range(0, 6)
            .Select(i => client.GetAsync($"/employee?id={baseId + i}"));

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }

    /// <summary>
    /// Тест для проверки пайплана:
    /// Gateway → Generator → SQS → FileService → Minio.
    /// </summary>
    [Fact]
    public async Task GetEmployee_CheckPipeline()
    {
        var id = 23;
        var expectedKey = $"employee-{id}.json";

        using var client = fixture.App.CreateHttpClient("companyemployees-apigateway", "http");
        using var response = await client.GetAsync($"/employee?id={id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var objects = await fixture.WaitForS3ObjectAsync(expectedKey);

        Assert.NotEmpty(objects);
    }

    /// <summary>
    /// Тест для проверки данных сохранённых в бакете
    /// </summary>
    [Fact]
    public async Task GetEmployee_CheckBucketData()
    {
        var id = 12;
        var expectedKey = $"employee-{id}.json";

        using var client = fixture.App.CreateHttpClient("companyemployees-apigateway", "http");
        var employee = await client.GetFromJsonAsync<CompanyEmployeeModel>($"/employee?id={id}", _jsonOptions);
        Assert.NotNull(employee);

        var response = await fixture.S3Client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = "company-employee"
        });

        var objects = await fixture.WaitForS3ObjectAsync(expectedKey);
        Assert.NotEmpty(objects);

        var getResponse = await fixture.S3Client.GetObjectAsync(_bucketName, expectedKey);
        using var reader = new StreamReader(getResponse.ResponseStream);
        var json = await reader.ReadToEndAsync();
        var cached = JsonNode.Parse(json)?.AsObject();

        Assert.NotNull(cached);
        Assert.Equal(id, cached["id"]!.GetValue<int>());
        Assert.Equal(employee.FullName, cached["fullName"]!.GetValue<string>());
        Assert.Equal(employee.Email, cached["email"]!.GetValue<string>());
        Assert.Equal(employee.Salary, cached["salary"]!.GetValue<decimal>());
    }

    /// <summary>
    /// Тест для проверки избежания дублирования данных в Minio
    /// </summary>
    [Fact]
    public async Task GetEmployee_CheckNotDuplicate()
    {
        var id = 23;
        var expectedKey = $"employee-{id}.json";

        using var client = fixture.App.CreateHttpClient("companyemployees-apigateway", "http");

        using var firstResponse = await client.GetAsync($"/employee?id={id}");
        firstResponse.EnsureSuccessStatusCode();
        var objectsAfterFirst = await fixture.WaitForS3ObjectAsync(expectedKey);
        Assert.NotEmpty(objectsAfterFirst);

        using var secondResponse = await client.GetAsync($"/employee?id={id}");
        secondResponse.EnsureSuccessStatusCode();

        await Task.Delay(TimeSpan.FromSeconds(5));

        var listResponse = await fixture.S3Client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = _bucketName,
            Prefix = expectedKey
        });

        Assert.Single(listResponse.S3Objects);
    }
}