namespace CompanyEmployees.Generator.Models;

/// <summary>
/// Модель сотрудника компании
/// </summary>
public class CompanyEmployeeModel
{
    /// <summary>
    /// Идентификатор сотрудника в системе
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ФИО 
    /// <details>
    /// Конкатенация фамилии, имени  и отчества через пробел
    /// </details>
    /// </summary>
    public required string FullName { get; set; }

    /// <summary>
    /// Должность <br/>
    /// Выбирается из справочника профессий (“Developer”, “Manager”, “Analyst” и т.д.) и справочника суффиксов к ним (“Junior”, “Middle”, “Senior” и т.д.) 
    /// </summary>
    public required string Position { get; set; }

    /// <summary>
    /// Отдел
    /// </summary>
    public required string Section { get; set; }

    /// <summary>
    /// Дата приема <br/>
    /// Ограничение: не более 10 лет назад от текущей даты
    /// </summary>
    public required DateOnly AdmissionDate { get; set; }

    /// <summary>
    /// Оклад <br/>
    /// Значение коррелирует с суффиксом должности
    /// </summary>
    public required decimal Salary { get; set; }

    /// <summary>
    /// Электронная почта
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Номер телефона формата +7(***)***-**-**
    /// </summary>
    public required string PhoneNumber { get; set; }

    /// <summary>
    /// Индикатор увольнения
    /// </summary>
    public required bool Dismissal { get; set; } = false;

    /// <summary>
    /// Дата увольнения <br/>
    /// При отсутствии индикатора увольнения дата увольнения не заполняется
    /// </summary>
    public DateOnly? DismissalDate { get; set; }
}
