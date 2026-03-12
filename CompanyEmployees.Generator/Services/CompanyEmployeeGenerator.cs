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

    private readonly Faker<CompanyEmployeeModel> _faker = new Faker<CompanyEmployeeModel>("ru")
            .RuleFor(x => x.FullName, GenerateEmployeeFullName)
            .RuleFor(x => x.Position, f => $"{f.PickRandom(_positionProfessions)} {f.PickRandom<PositionSuffix>()}")
            .RuleFor(x => x.Section, f => f.Commerce.Department())
            .RuleFor(x => x.AdmissionDate, f => f.Date.PastDateOnly(10))
            .RuleFor(x => x.Salary, GenerateEmployeeSalary)
            .RuleFor(x => x.Email, f => f.Internet.Email())
            .RuleFor(x => x.PhoneNumber, f => f.Phone.PhoneNumber("+7(###)###-##-##"))
            .RuleFor(x => x.Dismissal, f => f.Random.Bool())
            .RuleFor(x => x.DismissalDate, (f, employeeObject) =>
                        employeeObject.Dismissal
                        ? f.Date.BetweenDateOnly(employeeObject.AdmissionDate, DateOnly.FromDateTime(DateTime.UtcNow))
                        : null
            );

    /// <summary>
    /// Минимальное значение оклада
    /// </summary>
    private const decimal SalaryMin = 100;

    /// <summary>
    /// Максимальное значение оклада
    /// </summary>
    private const decimal SalaryMax = 200;

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

    /// <summary>
    /// Функция для генерации сотрудника на основе параметра id
    /// </summary>
    /// <param name="id">Идентификатор сотрудника в системе</param>
    /// <returns>Сгенерированные данные о сотруднике</returns>
    public CompanyEmployeeModel Generate(int id)
    {
        logger.LogInformation("Start generating company employee application with ID: {Id}", id);
        return _faker.UseSeed(id).RuleFor(x => x.Id, _ => id).Generate();
    }

    private static string GenerateEmployeeFullName(Faker faker)
    {
        var gender = faker.Person.Gender;
        var firstName = faker.Name.FirstName(gender);
        var lastName = faker.Name.LastName(gender);
        var patronymic = faker.Name.FirstName(Name.Gender.Male) + (gender == Name.Gender.Male ? "еевич" : "еевна");

        return string.Join(' ', firstName, lastName, patronymic);
    }

    private static decimal GenerateEmployeeSalary(Faker faker, CompanyEmployeeModel employeeObject)
    {
        var baseSalary = faker.Random.Decimal(SalaryMin, SalaryMax);
        var salaryMap = _positionSuffixSalaryMultipliers
            .FirstOrDefault(pair => employeeObject.Position.Contains(pair.Key.ToString()), new KeyValuePair<PositionSuffix, decimal>(PositionSuffix.Junior, 1m));

        return Math.Round(baseSalary * salaryMap.Value, 2);
    }
}
