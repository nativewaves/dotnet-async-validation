﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.AsyncValidation.Validation;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Mvc.ModelBinding;

public partial class AsyncParamterBinder : ParameterBinder
{
    private readonly IModelMetadataProvider _modelMetadataProvider;
    private readonly IAsyncObjectModelValidator _validator;

    public AsyncParamterBinder(
        IModelMetadataProvider modelMetadataProvider,
        IModelBinderFactory modelBinderFactory,
        IAsyncObjectModelValidator validator,
        IObjectModelValidator legacyValidator,
        IOptions<MvcOptions> mvcOptions,
        ILoggerFactory loggerFactory
        )
        : base(
            modelMetadataProvider: modelMetadataProvider,
            modelBinderFactory: modelBinderFactory,
            validator: legacyValidator,
            mvcOptions: mvcOptions,
            loggerFactory: loggerFactory)
    {
        _modelMetadataProvider = modelMetadataProvider;
        _validator = validator;
    }

    public override async ValueTask<ModelBindingResult> BindModelAsync(
        ActionContext actionContext,
        IModelBinder modelBinder,
        IValueProvider valueProvider,
        ParameterDescriptor parameter,
        ModelMetadata metadata,
        object value,
        object container
        )
    {
        ArgumentNullException.ThrowIfNull(actionContext);
        ArgumentNullException.ThrowIfNull(modelBinder);
        ArgumentNullException.ThrowIfNull(valueProvider);
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(metadata);

        Log.AttemptingToBindParameterOrProperty(Logger, parameter, metadata);

        if (parameter.BindingInfo?.RequestPredicate?.Invoke(actionContext) == false)
        {
            Log.ParameterBinderRequestPredicateShortCircuit(Logger, parameter, metadata);
            return ModelBindingResult.Failed();
        }

        var modelBindingContext = DefaultModelBindingContext.CreateBindingContext(
            actionContext,
            valueProvider,
            metadata,
            parameter.BindingInfo,
            parameter.Name);
        modelBindingContext.Model = value;

        var parameterModelName = parameter.BindingInfo?.BinderModelName ?? metadata.BinderModelName;
        if (parameterModelName != null)
        {
            // The name was set explicitly, always use that as the prefix.
            modelBindingContext.ModelName = parameterModelName;
        }
        else if (modelBindingContext.ValueProvider.ContainsPrefix(parameter.Name))
        {
            // We have a match for the parameter name, use that as that prefix.
            modelBindingContext.ModelName = parameter.Name;
        }
        else
        {
            // No match, fallback to empty string as the prefix.
            modelBindingContext.ModelName = string.Empty;
        }

        await modelBinder.BindModelAsync(modelBindingContext);

        Log.DoneAttemptingToBindParameterOrProperty(Logger, parameter, metadata);
        var modelBindingResult = modelBindingContext.Result;

        Log.AttemptingToValidateParameterOrProperty(Logger, parameter, metadata);

        await EnforceBindRequiredAndValidateAsync(
            _validator,
            actionContext,
            parameter,
            metadata,
            modelBindingContext,
            modelBindingResult,
            container,
            ct: default); // why?!??!

        Log.DoneAttemptingToValidateParameterOrProperty(Logger, parameter, metadata);
        return modelBindingResult;
    }

    private async Task EnforceBindRequiredAndValidateAsync(
        IAsyncObjectModelValidator validator,
        ActionContext actionContext,
        ParameterDescriptor parameter,
        ModelMetadata metadata,
        ModelBindingContext modelBindingContext,
        ModelBindingResult modelBindingResult,
        object container,
        CancellationToken ct = default
        )
    {
        RecalculateModelMetadata(parameter, modelBindingResult, ref metadata);

        if (!modelBindingResult.IsModelSet && metadata.IsBindingRequired)
        {
            // Enforce BindingBehavior.Required (e.g., [BindRequired])
            var modelName = modelBindingContext.FieldName;
            var message = metadata.ModelBindingMessageProvider.MissingBindRequiredValueAccessor(modelName);
            actionContext.ModelState.TryAddModelError(modelName, message);
        }
        else if (modelBindingResult.IsModelSet)
        {
            // Enforce any other validation rules
            await validator.ValidateAsync(
                actionContext: actionContext,
                validationState: modelBindingContext.ValidationState,
                prefix: modelBindingContext.ModelName,
                model: modelBindingResult.Model,
                metadata: metadata,
                container: container,
                ct: ct);
        }
        else if (metadata.IsRequired)
        {
            // We need to special case the model name for cases where a 'fallback' to empty
            // prefix occurred but binding wasn't successful. For these cases there will be no
            // entry in validation state to match and determine the correct key.
            //
            // See https://github.com/aspnet/Mvc/issues/7503
            //
            // This is to avoid adding validation errors for an 'empty' prefix when a simple
            // type fails to bind. The fix for #7503 uncovered this issue, and was likely the
            // original problem being worked around that regressed #7503.
            var modelName = modelBindingContext.ModelName;

            if (string.IsNullOrEmpty(modelBindingContext.ModelName) &&
                parameter.BindingInfo?.BinderModelName == null)
            {
                // If we get here then this is a fallback case. The model name wasn't explicitly set
                // and we ended up with an empty prefix.
                modelName = modelBindingContext.FieldName;
            }

            // Run validation, we expect this to validate [Required].
            await validator.ValidateAsync(actionContext: actionContext,
                validationState: modelBindingContext.ValidationState,
                prefix: modelName,
                model: modelBindingResult.Model,
                metadata: metadata,
                container: container,
                ct: ct);
        }
    }

    private void RecalculateModelMetadata(
        ParameterDescriptor parameter,
        ModelBindingResult modelBindingResult,
        ref ModelMetadata metadata
        )
    {
        // Attempt to recalculate ModelMetadata for top level parameters and properties using the actual
        // model type. This ensures validation uses a combination of top-level validation metadata
        // as well as metadata on the actual, rather than declared, model type.

        if (!modelBindingResult.IsModelSet ||
            modelBindingResult.Model == null ||
            _modelMetadataProvider is not ModelMetadataProvider modelMetadataProvider)
        {
            return;
        }

        var modelType = modelBindingResult.Model.GetType();
        if (parameter is IParameterInfoParameterDescriptor parameterInfoParameter)
        {
            var parameterInfo = parameterInfoParameter.ParameterInfo;
            if (modelType != parameterInfo.ParameterType)
            {
                metadata = modelMetadataProvider.GetMetadataForParameter(parameterInfo, modelType);
            }
        }
        else if (parameter is IPropertyInfoParameterDescriptor propertyInfoParameter)
        {
            var propertyInfo = propertyInfoParameter.PropertyInfo;
            if (modelType != propertyInfo.PropertyType)
            {
                metadata = modelMetadataProvider.GetMetadataForProperty(propertyInfo, modelType);
            }
        }
    }
}
