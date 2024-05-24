﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Microsoft.AspNetCore.Mvc.AsyncValidation.Validation;

public interface IAsyncObjectModelValidator
{
    /// <summary>
    /// Validates the provided object.
    /// </summary>
    /// <param name="actionContext">The Microsoft.AspNetCore.Mvc.ActionContext associated with the current request.</param>
    /// <param name="validationState"> The Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidationStateDictionary.
    /// May be null.</param>
    /// <param name="prefix">The model prefix. Used to map the model object to entries in validationState.</param>
    /// <param name="model">The model object.</param>
    /// <param name="metadata"></param>
    /// <param name="container"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ValidateAsync(
        ActionContext actionContext,
        ValidationStateDictionary validationState,
        string prefix,
        object model,
        ModelMetadata metadata = null,
        object container = null,
        CancellationToken ct = default);
}

internal class AsyncObjectModelValidator : IAsyncObjectModelValidator
{
    private readonly IModelMetadataProvider _modelMetadataProvider;
    private readonly IModelValidatorProvider _modelValidatorProvider;
    private readonly ValidatorCache _validatorCache;
    private readonly MvcOptions _mvcOptions;

    public AsyncObjectModelValidator(
        IModelMetadataProvider modelMetadataProvider,
        IList<IModelValidatorProvider> validatorProviders,
        ValidatorCache validatorCache,
        MvcOptions mvcOptions
        )
    {
        _modelValidatorProvider = new CompositeModelValidatorProvider(validatorProviders);
        _modelMetadataProvider = modelMetadataProvider;
        _validatorCache = validatorCache;
        _mvcOptions = mvcOptions;
    }

    public virtual async Task ValidateAsync(
        ActionContext actionContext,
        ValidationStateDictionary validationState,
        string prefix,
        object model,
        ModelMetadata metadata = null,
        object container = null,
        CancellationToken ct = default
        )
    {
        var visitor = GetValidationVisitor(
            actionContext,
            _modelValidatorProvider,
            _validatorCache,
            _modelMetadataProvider,
            validationState);

        await visitor.ValidateAsync(
            metadata: metadata,
            key: prefix,
            model: model,
            alwaysValidateAtTopLevel: metadata.IsRequired,
            container: container,
            ct: ct);
    }

    public virtual AsyncValidationVisitor GetValidationVisitor(
        ActionContext actionContext,
        IModelValidatorProvider validatorProvider,
        ValidatorCache validatorCache,
        IModelMetadataProvider metadataProvider,
        ValidationStateDictionary validationState)
    {
        return new AsyncValidationVisitor(
            actionContext,
            validatorProvider,
            validatorCache,
            metadataProvider,
            validationState)
        {
            MaxValidationDepth = _mvcOptions.MaxValidationDepth,
            ValidateComplexTypesIfChildValidationFails = _mvcOptions.ValidateComplexTypesIfChildValidationFails,
        };
    }
}
