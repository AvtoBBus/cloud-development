using CompanyEmployees.Generator.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace CompanyEmployees.Generator.Services;

/// <summary>
/// Сервис для работы с сотрудниками компании <br/>
/// Каждый сгенерированный сотрудник сохраняется в кэш, если ранее он небыл получен <br/>
/// В ином случае значение берется из кэша
/// </summary>
/// <param name="generator">Генератор необходимый для создания сотрудника</param>
/// <param name="cache">Кэш хранящий ранее сгенерированных сотрудников</param>
/// <param name="logger">Логгер</param>
public class CompanyEmployeeService(
    CompanyEmployeeGenerator generator,
    IDistributedCache cache,
    ILogger<CompanyEmployeeService> logger,
    IConfiguration config
)
{
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(config.GetSection("CacheSetting").GetValue("CacheExpirationMinutes", 5));
    private const string CacheKeyPrefix = "company-employee:";

    /// <summary>
    /// Функция для генерации или взятия из кэша сотрудника по id
    /// </summary>
    /// <param name="id">Id сотрудника</param>
    /// <param name="cancellationToken">Токен для отмены операции</param>
    /// <returns></returns>
    public async Task<CompanyEmployeeModel> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{id}";

        logger.LogInformation("Request for company employee application with ID: {Id}", id);

        var cachedData = await cache.GetStringAsync(cacheKey, cancellationToken);

        if (!string.IsNullOrEmpty(cachedData))
        {
            logger.LogInformation("Company employee application {Id} found in cache", id);
            var cachedApplication = JsonSerializer.Deserialize<CompanyEmployeeModel>(cachedData);
            if (cachedApplication != null) return cachedApplication;
        }

        logger.LogInformation("Company employee application {Id} not found in cache, generating new one", id);

        var application = generator.Generate(id);

        var serializedData = JsonSerializer.Serialize(application);
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _cacheExpiration
        };

        await cache.SetStringAsync(cacheKey, serializedData, cacheOptions, cancellationToken);

        logger.LogInformation(
            "Company employee application {Id} saved to cache with TTL {CacheExpiration} minutes",
            id,
            _cacheExpiration.TotalMinutes);

        return application;
    }
}
