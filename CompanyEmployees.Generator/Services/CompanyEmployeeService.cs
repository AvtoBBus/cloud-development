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
    ILogger<CompanyEmployeeService> logger
)
{
    private readonly CompanyEmployeeGenerator _generator = generator;
    private readonly IDistributedCache _cache = cache;
    private readonly ILogger<CompanyEmployeeService> _logger = logger;

    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
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

        _logger.LogInformation("Request for credit application with ID: {Id}", id);

        var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);

        if (!string.IsNullOrEmpty(cachedData))
        {
            _logger.LogInformation("Credit application {Id} found in cache", id);
            var cachedApplication = JsonSerializer.Deserialize<CompanyEmployeeModel>(cachedData);
            return cachedApplication!;
        }

        _logger.LogInformation("Credit application {Id} not found in cache, generating new one", id);

        var application = _generator.Generate(id);

        var serializedData = JsonSerializer.Serialize(application);
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _cacheExpiration
        };

        await _cache.SetStringAsync(cacheKey, serializedData, cacheOptions, cancellationToken);

        _logger.LogInformation(
            "Credit application {Id} saved to cache with TTL {CacheExpiration} minutes",
            id,
            _cacheExpiration.TotalMinutes);

        return application;
    }
}
