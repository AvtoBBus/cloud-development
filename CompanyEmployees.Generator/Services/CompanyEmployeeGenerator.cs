using Bogus;
using Bogus.DataSets;
using CompanyEmployees.Generator.Models;

namespace CompanyEmployees.Generator.Services;

/// <summary>
/// Генератор сотрудника по идентификатору
/// </summary>
/// <param name="logger">Логгер</param>
public class CompanyEmployeeGenerator(ILogger<CompanyEmployeeGenerator> logger)
{
    /// <summary>
    /// Справочник профессий сотрудников
    /// </summary>
    private static readonly string[] _positionProfessions =
    [
        "Developer",
        "Manager",
        "Analyst",
        "DevOps Engineer",
        "QA Engineer",
        "System Architect"
    ];

    /// <summary>
    /// Справочник суффиксов для профессий
    /// </summary>
    private enum PositionSuffix
    {
        Junior = 0,
        Middle = 1,
        Senior = 2,
        TeamLead = 3
    };

    /// <summary>
    /// Словарь множителей на основе суффиксов профессий
    /// </summary>
    private static readonly Dictionary<PositionSuffix, decimal> _positionSuffixSalaryMultipliers = new()
    {
        [PositionSuffix.Junior] = 1m,
        [PositionSuffix.Middle] = 1.5m,
        [PositionSuffix.Senior] = 2.1m,
        [PositionSuffix.TeamLead] = 2.6m
    };

    private readonly ILogger<CompanyEmployeeGenerator> _logger = logger;

    /// <summary>
    /// Функция для генерации сотрудника на основе параметра id
    /// </summary>
    /// <param name="id">Идентификатор сотрудника в системе</param>
    /// <returns>Сгенерированные данные о сотруднике</returns>
    public CompanyEmployeeModel Generate(int id)
    {
        _logger.LogInformation("Start generating company employee application with ID: {Id}", id);

        var faker = new Faker<CompanyEmployeeModel>("ru")
            .UseSeed(id)
            .RuleFor(x => x.Id, _ => id)
            .RuleFor(x => x.FullName, f => GenerateEmployeeFullName(f))
            .RuleFor(x => x.Position, f => GenerateEmployeePosition(f))
            .RuleFor(x => x.Section, f => f.Commerce.Department())
            .RuleFor(x => x.AdmissionDate, f => f.Date.PastDateOnly(10))
            .RuleFor(x => x.Salary, (f, x) => GenerateEmployeeSalary(f, x))
            .RuleFor(x => x.Email, f => f.Internet.Email())
            .RuleFor(x => x.PhoneNumber, f => f.Phone.PhoneNumber("+7(###)###-##-##"))
            .RuleFor(x => x.Dismissal, f => f.Random.Bool())
            .RuleFor(x => x.DismissalDate, (f, x) => GenerateEmployeeDismissalDate(f, x));

        _logger.LogInformation("Finally generate employee with ID: {Id}", id);

        return faker.Generate();
    }

    private static string GenerateEmployeeFullName(Faker faker)
    {
        var gender = faker.PickRandom(Enum.GetValues(typeof(Name.Gender)).Cast<Name.Gender>().ToArray());

        var firstName = faker.Name.FirstName(gender);
        var lastName = faker.Name.LastName(gender);
        var patronymic = faker.Name.FirstName(gender) + (gender == Name.Gender.Male ? "еевич" : "еевна");

        return string.Join(' ', firstName, lastName, patronymic);
    }

    private static string GenerateEmployeePosition(Faker faker)
    {
        return string.Join(' ', faker.PickRandom(_positionProfessions), faker.PickRandom<PositionSuffix>());
    }

    private static decimal GenerateEmployeeSalary(Faker faker, CompanyEmployeeModel x)
    {
        var baseSalary = faker.Random.Decimal(100, 200);
        var finalSalary = baseSalary;

        foreach (var kvp in _positionSuffixSalaryMultipliers)
        {
            if (x.Position.Contains(kvp.Key.ToString()))
            {
                finalSalary = baseSalary * kvp.Value;
                break;
            }
        }

        return Math.Round(baseSalary, 2);
    }

    private static DateOnly? GenerateEmployeeDismissalDate(Faker faker, CompanyEmployeeModel x)
    {

        if (x.Dismissal) return null;

        return faker.Date.BetweenDateOnly(x.AdmissionDate, DateOnly.FromDateTime(DateTime.UtcNow));
    }
}
