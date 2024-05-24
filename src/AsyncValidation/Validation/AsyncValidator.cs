﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Mvc.AsyncValidation.Validation;
using System.Globalization;

namespace System.ComponentModel.DataAnnotations;

/// <summary>
/// Helper class to validate objects, properties and other values using their associated <see cref="ValidationAttribute"/>
/// custom attributes.
/// </summary>
public static class AsyncValidator
{
    private static ValidationAttributeStore _store = ValidationAttributeStore.Instance;

    /// <summary>
    /// Tests whether the given property value is valid.
    /// </summary>
    /// <remarks>
    /// This method will test each <see cref="ValidationAttribute"/> associated with the property
    /// identified by <paramref name="validationContext"/>.  If <paramref name="validationResults"/> is non-null,
    /// this method will add a <see cref="ValidationResult"/> to it for each validation failure.
    /// <para>
    /// If there is a <see cref="RequiredAttribute"/> found on the property, it will be evaluated before all other
    /// validation attributes.  If the required validator fails then validation will abort, adding that single
    /// failure into the <paramref name="validationResults"/> when applicable, returning a value of <c>false</c>.
    /// </para>
    /// <para>
    /// If <paramref name="validationResults"/> is null and there isn't a <see cref="RequiredAttribute"/> failure,
    /// then all validators will be evaluated.
    /// </para>
    /// </remarks>
    /// <param name="value">The value to test.</param>
    /// <param name="validationContext">Describes the property member to validate and provides services and context for the validators.</param>
    /// <param name="validationResults">Optional collection to receive <see cref="ValidationResult"/>s for the failures.</param>
    /// <param name="ct"></param>
    /// <returns><c>true</c> if the value is valid, <c>false</c> if any validation errors are encountered.</returns>
    /// <exception cref="ArgumentException">
    /// When the <see cref="ValidationContext.MemberName"/> of <paramref name="validationContext"/> is not a valid property.
    /// </exception>
    public static async Task<bool> TryValidatePropertyAsync(
        this object value,
        ValidationContext validationContext,
        ICollection<ValidationResult> validationResults,
        CancellationToken ct = default
        )
    {
        // Throw if value cannot be assigned to this property.  That is not a validation exception.
        Type propertyType = _store.GetPropertyType(validationContext);
        string propertyName = validationContext.MemberName;
        EnsureValidPropertyType(propertyName, propertyType, value);

        bool result = true;
        bool breakOnFirstError = (validationResults == null);

        IEnumerable<ValidationAttribute> attributes = _store.GetPropertyValidationAttributes(validationContext);

        foreach (ValidationError err in await GetValidationErrorsAsync(value, validationContext, attributes, breakOnFirstError, ct))
        {
            result = false;

            if (validationResults != null)
            {
                validationResults.Add(err.ValidationResult);
            }
        }

        return result;
    }

    /// <summary>
    /// Tests whether the given object instance is valid.
    /// </summary>
    /// <remarks>
    /// This method evaluates all <see cref="ValidationAttribute"/>s attached to the object instance's type.  It also
    /// checks to ensure all properties marked with <see cref="RequiredAttribute"/> are set.  It does not validate the
    /// property values of the object.
    /// <para>
    /// If <paramref name="validationResults"/> is null, then execution will abort upon the first validation
    /// failure.  If <paramref name="validationResults"/> is non-null, then all validation attributes will be
    /// evaluated.
    /// </para>
    /// </remarks>
    /// <param name="instance">The object instance to test.  It cannot be <c>null</c>.</param>
    /// <param name="validationContext">Describes the object to validate and provides services and context for the validators.</param>
    /// <param name="validationResults">Optional collection to receive <see cref="ValidationResult"/>s for the failures.</param>
    /// <param name="ct"></param>
    /// <returns><c>true</c> if the object is valid, <c>false</c> if any validation errors are encountered.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="instance"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="instance"/> doesn't match the
    /// <see cref="ValidationContext.ObjectInstance"/>on <paramref name="validationContext"/>.</exception>
    public static Task<bool> TryValidateObject(object instance, ValidationContext validationContext, ICollection<ValidationResult> validationResults, CancellationToken ct = default)
        => TryValidateObjectAsync(instance, validationContext, validationResults, false /*validateAllProperties*/, ct);

    /// <summary>
    /// Tests whether the given object instance is valid.
    /// </summary>
    /// <remarks>
    /// This method evaluates all <see cref="ValidationAttribute"/>s attached to the object instance's type.  It also
    /// checks to ensure all properties marked with <see cref="RequiredAttribute"/> are set.  If <paramref name="validateAllProperties"/>
    /// is <c>true</c>, this method will also evaluate the <see cref="ValidationAttribute"/>s for all the immediate properties
    /// of this object.  This process is not recursive.
    /// <para>
    /// If <paramref name="validationResults"/> is null, then execution will abort upon the first validation
    /// failure.  If <paramref name="validationResults"/> is non-null, then all validation attributes will be
    /// evaluated.
    /// </para>
    /// <para>
    /// For any given property, if it has a <see cref="RequiredAttribute"/> that fails validation, no other validators
    /// will be evaluated for that property.
    /// </para>
    /// </remarks>
    /// <param name="instance">The object instance to test.  It cannot be null.</param>
    /// <param name="validationContext">Describes the object to validate and provides services and context for the validators.</param>
    /// <param name="validationResults">Optional collection to receive <see cref="ValidationResult"/>s for the failures.</param>
    /// <param name="validateAllProperties">If <c>true</c>, also evaluates all properties of the object (this process is not
    /// recursive over properties of the properties).</param>
    /// <param name="ct"></param>
    /// <returns><c>true</c> if the object is valid, <c>false</c> if any validation errors are encountered.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="instance"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="instance"/> doesn't match the
    /// <see cref="ValidationContext.ObjectInstance"/>on <paramref name="validationContext"/>.</exception>
    public static async Task<bool> TryValidateObjectAsync(object instance, ValidationContext validationContext, ICollection<ValidationResult> validationResults, bool validateAllProperties, CancellationToken ct = default)
    {
        if (instance == null)
        {
            throw new ArgumentNullException("instance");
        }

        if (validationContext != null && instance != validationContext.ObjectInstance)
        {
            throw new ArgumentException("InstanceMustMatchValidationContextInstance", "instance");
        }

        bool result = true;
        bool breakOnFirstError = (validationResults == null);

        foreach (ValidationError err in await GetObjectValidationErrorsAsync(instance, validationContext, validateAllProperties, breakOnFirstError, ct))
        {
            result = false;

            if (validationResults != null)
            {
                validationResults.Add(err.ValidationResult);
            }
        }

        return result;
    }

    /// <summary>
    /// Tests whether the given value is valid against a specified list of <see cref="ValidationAttribute"/>s.
    /// </summary>
    /// <remarks>
    /// This method will test each <see cref="ValidationAttribute"/>s specified .  If
    /// <paramref name="validationResults"/> is non-null, this method will add a <see cref="ValidationResult"/>
    /// to it for each validation failure.
    /// <para>
    /// If there is a <see cref="RequiredAttribute"/> within the <paramref name="validationAttributes"/>, it will
    /// be evaluated before all other validation attributes.  If the required validator fails then validation will
    /// abort, adding that single failure into the <paramref name="validationResults"/> when applicable, returning a
    /// value of <c>false</c>.
    /// </para>
    /// <para>
    /// If <paramref name="validationResults"/> is null and there isn't a <see cref="RequiredAttribute"/> failure,
    /// then all validators will be evaluated.
    /// </para>
    /// </remarks>
    /// <param name="value">The value to test.  It cannot be null.</param>
    /// <param name="validationContext">Describes the object being validated and provides services and context for the validators.</param>
    /// <param name="validationResults">Optional collection to receive <see cref="ValidationResult"/>s for the failures.</param>
    /// <param name="validationAttributes">The list of <see cref="ValidationAttribute"/>s to validate this <paramref name="value"/> against.</param>
    /// <param name="ct"></param>
    /// <returns><c>true</c> if the object is valid, <c>false</c> if any validation errors are encountered.</returns>
    public static async Task<bool> TryValidateValueAsync(object value, ValidationContext validationContext, ICollection<ValidationResult> validationResults, IEnumerable<ValidationAttribute> validationAttributes, CancellationToken ct = default)
    {
        bool result = true;
        bool breakOnFirstError = validationResults == null;

        foreach (ValidationError err in await GetValidationErrorsAsync(value, validationContext, validationAttributes, breakOnFirstError, ct))
        {
            result = false;

            if (validationResults != null)
            {
                validationResults.Add(err.ValidationResult);
            }
        }

        return result;
    }

    /// <summary>
    /// Throws a <see cref="ValidationException"/> if the given property <paramref name="value"/> is not valid.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <param name="validationContext">Describes the object being validated and provides services and context for the validators.  It cannot be <c>null</c>.</param>
    /// <param name="ct"></param>
    /// <exception cref="ArgumentNullException">When <paramref name="validationContext"/> is null.</exception>
    /// <exception cref="ValidationException">When <paramref name="value"/> is invalid for this property.</exception>
    public static async Task ValidatePropertyAsync(object value, ValidationContext validationContext, CancellationToken ct = default)
    {
        // Throw if value cannot be assigned to this property.  That is not a validation exception.
        Type propertyType = _store.GetPropertyType(validationContext);
        EnsureValidPropertyType(validationContext.MemberName, propertyType, value);

        IEnumerable<ValidationAttribute> attributes = _store.GetPropertyValidationAttributes(validationContext);

        ValidationError err = (await GetValidationErrorsAsync(value, validationContext, attributes, false, ct)).FirstOrDefault();
        if (err != null)
        {
            err.ThrowValidationException();
        }
    }

    /// <summary>
    /// Throws a <see cref="ValidationException"/> if the given <paramref name="instance"/> is not valid.
    /// </summary>
    /// <remarks>
    /// This method evaluates all <see cref="ValidationAttribute"/>s attached to the object's type.
    /// </remarks>
    /// <param name="instance">The object instance to test.  It cannot be null.</param>
    /// <param name="validationContext">Describes the object being validated and provides services and context for the validators.  It cannot be <c>null</c>.</param>
    /// <param name="ct"></param>
    /// <exception cref="ArgumentNullException">When <paramref name="instance"/> is null.</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="validationContext"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="instance"/> doesn't match the
    /// <see cref="ValidationContext.ObjectInstance"/> on <paramref name="validationContext"/>.</exception>
    /// <exception cref="ValidationException">When <paramref name="instance"/> is found to be invalid.</exception>
    public static Task ValidateObjectAsync(object instance, ValidationContext validationContext, CancellationToken ct = default)
        => ValidateObjectAsync(instance, validationContext, false /*validateAllProperties*/, ct);

    /// <summary>
    /// Throws a <see cref="ValidationException"/> if the given object instance is not valid.
    /// </summary>
    /// <remarks>
    /// This method evaluates all <see cref="ValidationAttribute"/>s attached to the object's type.
    /// If <paramref name="validateAllProperties"/> is <c>true</c> it also validates all the object's properties.
    /// </remarks>
    /// <param name="instance">The object instance to test.  It cannot be null.</param>
    /// <param name="validationContext">Describes the object being validated and provides services and context for the validators.  It cannot be <c>null</c>.</param>
    /// <param name="validateAllProperties">If <c>true</c>, also validates all the <paramref name="instance"/>'s properties.</param>
    /// <param name="ct"></param>
    /// <exception cref="ArgumentNullException">When <paramref name="instance"/> is null.</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="validationContext"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="instance"/> doesn't match the
    /// <see cref="ValidationContext.ObjectInstance"/> on <paramref name="validationContext"/>.</exception>
    /// <exception cref="ValidationException">When <paramref name="instance"/> is found to be invalid.</exception>
    public static async Task ValidateObjectAsync(object instance, ValidationContext validationContext, bool validateAllProperties, CancellationToken ct = default)
    {
        if (instance == null)
        {
            throw new ArgumentNullException("instance");
        }
        if (validationContext == null)
        {
            throw new ArgumentNullException("validationContext");
        }
        if (instance != validationContext.ObjectInstance)
        {
            throw new ArgumentException("Validator_InstanceMustMatchValidationContextInstance", "instance");
        }

        ValidationError err = (await GetObjectValidationErrorsAsync(instance, validationContext, validateAllProperties, false, ct)).FirstOrDefault();
        if (err != null)
        {
            err.ThrowValidationException();
        }
    }

    /// <summary>
    /// Throw a <see cref="ValidationException"/> if the given value is not valid for the <see cref="ValidationAttribute"/>s.
    /// </summary>
    /// <remarks>
    /// This method evaluates the <see cref="ValidationAttribute"/>s supplied until a validation error occurs,
    /// at which time a <see cref="ValidationException"/> is thrown.
    /// <para>
    /// A <see cref="RequiredAttribute"/> within the <paramref name="validationAttributes"/> will always be evaluated first.
    /// </para>
    /// </remarks>
    /// <param name="value">The value to test.  It cannot be null.</param>
    /// <param name="validationContext">Describes the object being tested.</param>
    /// <param name="validationAttributes">The list of <see cref="ValidationAttribute"/>s to validate against this instance.</param>
    /// <param name="ct"></param>
    /// <exception cref="ArgumentNullException">When <paramref name="validationContext"/> is null.</exception>
    /// <exception cref="ValidationException">When <paramref name="value"/> is found to be invalid.</exception>
    public static async Task ValidateValueAsync(object value, ValidationContext validationContext, IEnumerable<ValidationAttribute> validationAttributes, CancellationToken ct = default)
    {
        if (validationContext == null)
        {
            throw new ArgumentNullException("validationContext");
        }

        ValidationError err = (await GetValidationErrorsAsync(value, validationContext, validationAttributes, false, ct)).FirstOrDefault();
        if (err != null)
        {
            err.ThrowValidationException();
        }
    }

    /// <summary>
    /// Creates a new <see cref="ValidationContext"/> to use to validate the type or a member of
    /// the given object instance.
    /// </summary>
    /// <param name="instance">The object instance to use for the context.</param>
    /// <param name="validationContext">An parent validation context that supplies an <see cref="IServiceProvider"/>
    /// and <see cref="ValidationContext.Items"/>.</param>
    /// <returns>A new <see cref="ValidationContext"/> for the <paramref name="instance"/> provided.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="validationContext"/> is null.</exception>
    internal static ValidationContext CreateValidationContext(object instance, ValidationContext validationContext)
    {
        if (validationContext == null)
        {
            throw new ArgumentNullException("validationContext");
        }

        // Create a new context using the existing ValidationContext that acts as an IServiceProvider and contains our existing items.
        ValidationContext context = new ValidationContext(instance, validationContext, validationContext.Items);
        return context;
    }

    /// <summary>
    /// Determine whether the given value can legally be assigned into the specified type.
    /// </summary>
    /// <param name="destinationType">The destination <see cref="Type"/> for the value.</param>
    /// <param name="value">The value to test to see if it can be assigned as the Type indicated by <paramref name="destinationType"/>.</param>
    /// <returns><c>true</c> if the assignment is legal.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="destinationType"/> is null.</exception>
    private static bool CanBeAssigned(Type destinationType, object value)
    {
        if (destinationType == null)
        {
            throw new ArgumentNullException("destinationType");
        }

        if (value == null)
        {
            // Null can be assigned only to reference types or Nullable or Nullable<>
            return !destinationType.IsValueType ||
                    (destinationType.IsGenericType && destinationType.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        // Not null -- be sure it can be cast to the right type
        return destinationType.IsAssignableFrom(value.GetType());
    }

    /// <summary>
    /// Determines whether the given value can legally be assigned to the given property.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="propertyType">The type of the property.</param>
    /// <param name="value">The value.  Null is permitted only if the property will accept it.</param>
    /// <exception cref="ArgumentException"> is thrown if <paramref name="value"/> is the wrong type for this property.</exception>
    private static void EnsureValidPropertyType(string propertyName, Type propertyType, object value)
    {
        if (!CanBeAssigned(propertyType, value))
        {
            throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, "Validator_Property_Value_Wrong_Type", propertyName, propertyType), "value");
        }
    }

    /// <summary>
    /// Internal iterator to enumerate all validation errors for the given object instance.
    /// </summary>
    /// <param name="instance">Object instance to test.</param>
    /// <param name="validationContext">Describes the object type.</param>
    /// <param name="validateAllProperties">if <c>true</c> also validates all properties.</param>
    /// <param name="breakOnFirstError">Whether to break on the first error or validate everything.</param>
    /// <param name="ct"></param>
    /// <returns>A collection of validation errors that result from validating the <paramref name="instance"/> with
    /// the given <paramref name="validationContext"/>.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="instance"/> is null.</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="validationContext"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="instance"/> doesn't match the
    /// <see cref="ValidationContext.ObjectInstance"/> on <paramref name="validationContext"/>.</exception>
    private static async Task<IEnumerable<ValidationError>> GetObjectValidationErrorsAsync(object instance, ValidationContext validationContext, bool validateAllProperties, bool breakOnFirstError, CancellationToken ct)
    {
        if (instance == null)
        {
            throw new ArgumentNullException("instance");
        }

        if (validationContext == null)
        {
            throw new ArgumentNullException("validationContext");
        }

        // Step 1: Validate the object properties' validation attributes
        List<ValidationError> errors = new List<ValidationError>();
        errors.AddRange(await GetObjectPropertyValidationErrorsAsync(instance, validationContext, validateAllProperties, breakOnFirstError, ct));

        // We only proceed to Step 2 if there are no errors
        if (errors.Any())
        {
            return errors;
        }

        // Step 2: Validate the object's validation attributes
        IEnumerable<ValidationAttribute> attributes = _store.GetTypeValidationAttributes(validationContext);
        errors.AddRange(await GetValidationErrorsAsync(instance, validationContext, attributes, breakOnFirstError, ct));

        return errors;
    }

    /// <summary>
    /// Internal iterator to enumerate all the validation errors for all properties of the given object instance.
    /// </summary>
    /// <param name="instance">Object instance to test.</param>
    /// <param name="validationContext">Describes the object type.</param>
    /// <param name="validateAllProperties">If <c>true</c>, evaluates all the properties, otherwise just checks that
    /// ones marked with <see cref="RequiredAttribute"/> are not null.</param>
    /// <param name="breakOnFirstError">Whether to break on the first error or validate everything.</param>
    /// <param name="ct"></param>
    /// <returns>A list of <see cref="ValidationError"/> instances.</returns>
    private static async Task<IEnumerable<ValidationError>> GetObjectPropertyValidationErrorsAsync(object instance, ValidationContext validationContext, bool validateAllProperties, bool breakOnFirstError, CancellationToken ct)
    {
        ICollection<KeyValuePair<ValidationContext, object>> properties = GetPropertyValues(instance, validationContext);
        List<ValidationError> errors = new List<ValidationError>();

        foreach (KeyValuePair<ValidationContext, object> property in properties)
        {
            // get list of all validation attributes for this property
            IEnumerable<ValidationAttribute> attributes = _store.GetPropertyValidationAttributes(property.Key);

            if (validateAllProperties)
            {
                // validate all validation attributes on this property
                errors.AddRange(await GetValidationErrorsAsync(property.Value, property.Key, attributes, breakOnFirstError, ct));
            }
            else
            {
                // only validate the Required attributes
                RequiredAttribute reqAttr = attributes.FirstOrDefault(a => a is RequiredAttribute) as RequiredAttribute;
                if (reqAttr != null)
                {
                    // Note: we let the [Required] attribute do its own null testing,
                    // since the user may have subclassed it and have a deeper meaning to what 'required' means
                    ValidationResult validationResult = reqAttr.GetValidationResult(property.Value, property.Key);
                    if (validationResult != ValidationResult.Success)
                    {
                        errors.Add(new ValidationError(reqAttr, property.Value, validationResult));
                    }
                }
            }

            if (breakOnFirstError && errors.Any())
            {
                break;
            }
        }

        return errors;
    }

    /// <summary>
    /// Retrieves the property values for the given instance.
    /// </summary>
    /// <param name="instance">Instance from which to fetch the properties.</param>
    /// <param name="validationContext">Describes the entity being validated.</param>
    /// <returns>A set of key value pairs, where the key is a validation context for the property and the value is its current
    /// value.</returns>
    /// <remarks>Ignores indexed properties.</remarks>
    private static ICollection<KeyValuePair<ValidationContext, object>> GetPropertyValues(object instance, ValidationContext validationContext)
    {

        PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(instance);
        List<KeyValuePair<ValidationContext, object>> items = new List<KeyValuePair<ValidationContext, object>>(properties.Count);
        foreach (PropertyDescriptor property in properties)
        {
            ValidationContext context = CreateValidationContext(instance, validationContext);
            context.MemberName = property.Name;

            if (_store.GetPropertyValidationAttributes(context).Any())
            {
                items.Add(new KeyValuePair<ValidationContext, object>(context, property.GetValue(instance)));
            }
        }
        return items;
    }

    /// <summary>
    /// Internal iterator to enumerate all validation errors for an value.
    /// </summary>
    /// <remarks>
    /// If a <see cref="RequiredAttribute"/> is found, it will be evaluated first, and if that fails,
    /// validation will abort, regardless of the <paramref name="breakOnFirstError"/> parameter value.
    /// </remarks>
    /// <param name="value">The value to pass to the validation attributes.</param>
    /// <param name="validationContext">Describes the type/member being evaluated.</param>
    /// <param name="attributes">The validation attributes to evaluate.</param>
    /// <param name="breakOnFirstError">Whether or not to break on the first validation failure.  A
    /// <see cref="RequiredAttribute"/> failure will always abort with that sole failure.</param>
    /// <param name="ct"></param>
    /// <returns>The collection of validation errors.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="validationContext"/> is null.</exception>
    private static async Task<IEnumerable<ValidationError>> GetValidationErrorsAsync(object value, ValidationContext validationContext, IEnumerable<ValidationAttribute> attributes, bool breakOnFirstError, CancellationToken ct)
    {
        if (validationContext == null)
        {
            throw new ArgumentNullException("validationContext");
        }

        List<ValidationError> errors = new List<ValidationError>();

        // Get the required validator if there is one and test it first, aborting on failure
        RequiredAttribute required = attributes.FirstOrDefault(a => a is RequiredAttribute) as RequiredAttribute;
        if (required != null)
        {
            var result = await TryValidateAsync(value, validationContext, required, ct);
            if (!result.success)
            {
                errors.Add(result.error);
                return errors;
            }
        }

        // Iterate through the rest of the validators, skipping the required validator
        foreach (ValidationAttribute attr in attributes)
        {
            if (attr != required)
            {
                var result = await TryValidateAsync(value, validationContext, attr, ct);
                if (!result.success)
                {
                    errors.Add(result.error);

                    if (breakOnFirstError)
                    {
                        break;
                    }
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Tests whether a value is valid against a single <see cref="ValidationAttribute"/> using the <see cref="ValidationContext"/>.
    /// </summary>
    /// <param name="value">The value to be tested for validity.</param>
    /// <param name="validationContext">Describes the property member to validate.</param>
    /// <param name="attribute">The validation attribute to test.</param>
    /// <param name="ct"></param>
    /// <exception cref="ArgumentNullException">When <paramref name="validationContext"/> is null.</exception>
    private static async Task<(bool success, ValidationError error)> TryValidateAsync(object value, ValidationContext validationContext, ValidationAttribute attribute, CancellationToken ct)
    {
        if (validationContext == null)
        {
            throw new ArgumentNullException("validationContext");
        }

        ValidationResult validationResult;
        if (attribute is AsyncValidationAttribute asyncValidation)
        {
            validationResult = await asyncValidation.GetValidationResultAsync(value, validationContext, ct);
        }
        else
        {
            validationResult = attribute.GetValidationResult(value, validationContext);
        }
        if (validationResult != ValidationResult.Success)
        {
            return (false, new ValidationError(attribute, value, validationResult));
        }

        return (true, null);
    }

    /// <summary>
    /// Private helper class to encapsulate a ValidationAttribute with the failed value and the user-visible
    /// target name against which it was validated.
    /// </summary>
    private class ValidationError
    {
        internal ValidationError(ValidationAttribute validationAttribute, object value, ValidationResult validationResult)
        {
            ValidationAttribute = validationAttribute;
            ValidationResult = validationResult;
            Value = value;
        }

        internal object Value { get; set; }

        internal ValidationAttribute ValidationAttribute { get; set; }

        internal ValidationResult ValidationResult { get; set; }

        internal void ThrowValidationException()
        {
            throw new ValidationException(ValidationResult, ValidationAttribute, Value);
        }
    }
}
