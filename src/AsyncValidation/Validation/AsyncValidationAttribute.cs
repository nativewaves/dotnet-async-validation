﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;

#nullable disable
namespace Microsoft.AspNetCore.Mvc.AsyncValidation.Validation;

public abstract class AsyncValidationAttribute : ValidationAttribute
{
    protected AsyncValidationAttribute() { }
    protected AsyncValidationAttribute(Func<string> errorMessageAccessor) : base(errorMessageAccessor) { }
    protected AsyncValidationAttribute(string errorMessage) : base(errorMessage) { }

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        throw new InvalidOperationException("Async validation called synchronously.");
    }

    protected abstract Task<ValidationResult> IsValidAsync(object value, ValidationContext validationContext, CancellationToken ct = default);

    /// <summary>
    /// Tests whether the given <paramref name="value"/> is valid with respect to the current
    /// validation attribute without throwing a <see cref="ValidationException"/>
    /// </summary>
    /// <remarks>
    /// If this method returns <see cref="ValidationResult.Success"/>, then validation was successful, otherwise
    /// an instance of <see cref="ValidationResult"/> will be returned with a guaranteed non-null
    /// <see cref="ValidationResult.ErrorMessage"/>.
    /// </remarks>
    /// <param name="value">The value to validate</param>
    /// <param name="validationContext">A <see cref="ValidationContext"/> instance that provides
    /// context about the validation operation, such as the object and member being validated.</param>
    /// <param name="ct"></param>
    /// <returns>
    /// When validation is valid, <see cref="ValidationResult.Success"/>.
    /// <para>
    /// When validation is invalid, an instance of <see cref="ValidationResult"/>.
    /// </para>
    /// </returns>
    /// <exception cref="InvalidOperationException"> is thrown if the current attribute is malformed.</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="validationContext"/> is null.</exception>
    /// <exception cref="InvalidOperationException"> is thrown when <see cref="IsValid(object, ValidationContext)" />
    /// is called.
    /// </exception>
    public async Task<ValidationResult> GetValidationResultAsync(object value, ValidationContext validationContext, CancellationToken ct = default)
    {
        if (validationContext == null)
        {
            throw new ArgumentNullException("validationContext");
        }

        ValidationResult result = await IsValidAsync(value, validationContext, ct);

        // If validation fails, we want to ensure we have a ValidationResult that guarantees it has an ErrorMessage
        if (result != null)
        {
            var hasErrorMessage = result != null && !string.IsNullOrEmpty(result.ErrorMessage);
            if (!hasErrorMessage)
            {
                var errorMessage = FormatErrorMessage(validationContext.DisplayName);
                result = new ValidationResult(errorMessage, result?.MemberNames);
            }
        }

        return result;
    }

    /// <summary>
    /// Validates the specified <paramref name="value"/> and throws <see cref="ValidationException"/> if it is not.
    /// </summary>
    /// <remarks>This method invokes the <see cref="IsValidAsync(object, ValidationContext, CancellationToken)"/> method 
    /// to determine whether or not the <paramref name="value"/> is acceptable given the <paramref name="validationContext"/>.
    /// If that method doesn't return <see cref="ValidationResult.Success"/>, this base method will throw
    /// a <see cref="ValidationException"/> containing the <see cref="ValidationResult"/> describing the problem.
    /// </remarks>
    /// <param name="value">The value to validate</param>
    /// <param name="validationContext">Additional context that may be used for validation.  It cannot be null.</param>
    /// <param name="ct"></param>
    /// <exception cref="ValidationException"> is thrown if <see cref="IsValidAsync(object, ValidationContext, CancellationToken)"/> 
    /// doesn't return <see cref="ValidationResult.Success"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException"> is thrown if the current attribute is malformed.</exception>
    /// <exception cref="NotImplementedException"> is thrown when <see cref="IsValidAsync(object, ValidationContext, CancellationToken)" />
    /// has not been implemented by a derived class.
    /// </exception>
    public async Task ValidateAsync(object value, ValidationContext validationContext, CancellationToken ct = default)
    {
        if (validationContext == null)
        {
            throw new ArgumentNullException("validationContext");
        }

        ValidationResult result = await GetValidationResultAsync(value, validationContext, ct: ct);

        if (result != null)
        {
            // Convenience -- if implementation did not fill in an error message,
            throw new ValidationException(result, this, value);
        }
    }
}

#nullable restore
