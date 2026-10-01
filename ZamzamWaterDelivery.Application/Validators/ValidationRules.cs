using System.ComponentModel.DataAnnotations;

namespace ZamzamWaterDelivery.Application.Validators;

public static class ValidationPatterns
{
    /// <summary>Optional leading +, then 7-15 digits; spaces and dashes are allowed as separators.</summary>
    public const string Phone = @"^\+?[0-9][0-9\s\-]{6,18}$";
    public const string PhoneMessage = "Enter a valid phone number (e.g. +252 61 5123456).";

    /// <summary>At least 8 chars with upper, lower, digit and symbol - mirrors the Identity password policy.</summary>
    public const string Password = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{8,100}$";
    public const string PasswordMessage =
        "Password must be at least 8 characters and include upper and lower case letters, a number and a symbol.";
}

/// <summary>Validates that a date is today or later.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotInPastAttribute : ValidationAttribute
{
    public NotInPastAttribute() : base("{0} cannot be in the past.") { }

    public override bool IsValid(object? value) =>
        value is not DateTime date || date.Date >= DateTime.Today;
}

/// <summary>Validates that an enum value is one of the defined members.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class DefinedEnumAttribute : ValidationAttribute
{
    public DefinedEnumAttribute() : base("{0} has an invalid value.") { }

    public override bool IsValid(object? value) =>
        value is null || (value.GetType().IsEnum && Enum.IsDefined(value.GetType(), value));
}
