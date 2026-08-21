using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace WarehouseApi.Dtos.Auth;

/// <summary>
/// Validated automatically by ASP.NET Core's built-in Minimal API validation
/// (see builder.Services.AddValidation() in Program.cs) — no manual filter needed.
/// Complex, cross-field rules (password strength, password confirmation) live in
/// Validate(), which the runtime calls because this type implements IValidatableObject.
/// </summary>
public record RegisterRequest(
    [property: Required, EmailAddress, MaxLength(256)]
    string Email,

    [property: Required]
    [property: MinLength(12, ErrorMessage = "Password must be at least 12 characters long.")]
    string Password,

    [property: Required]
    string ConfirmPassword) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Regex.IsMatch(Password, "[A-Z]"))
        {
            yield return new ValidationResult("Password must contain an uppercase letter.", [nameof(Password)]);
        }

        if (!Regex.IsMatch(Password, "[a-z]"))
        {
            yield return new ValidationResult("Password must contain a lowercase letter.", [nameof(Password)]);
        }

        if (!Regex.IsMatch(Password, "[0-9]"))
        {
            yield return new ValidationResult("Password must contain a digit.", [nameof(Password)]);
        }

        if (!Regex.IsMatch(Password, @"[^a-zA-Z0-9]"))
        {
            yield return new ValidationResult("Password must contain a special character.", [nameof(Password)]);
        }

        if (Password != ConfirmPassword)
        {
            yield return new ValidationResult("Passwords do not match.", [nameof(ConfirmPassword)]);
        }
    }
}
