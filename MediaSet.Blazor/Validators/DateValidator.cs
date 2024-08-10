using System.ComponentModel.DataAnnotations;

namespace MediaSet.Blazor.Validators;

/// <summary>
/// Attempt to validate whether the given property can be converted to a <see cref="DateTime" />.
/// null and <see cref="string.Empty"/> are considered valid.
/// </summary>
public class DateValidator : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
      try
      {
        if (value is null || string.IsNullOrWhiteSpace(value.ToString()))
        {
          return ValidationResult.Success;
        }
        Convert.ToDateTime(value);
        return ValidationResult.Success;
      }
      catch
      {
        return new ValidationResult($"{value} is not a valid date", [validationContext.MemberName ?? string.Empty]);
      }
    }
}