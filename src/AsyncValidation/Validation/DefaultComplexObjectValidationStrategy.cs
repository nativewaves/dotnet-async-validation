﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Collections;

namespace Microsoft.AspNetCore.Mvc.AsyncValidation.Validation;

internal sealed class DefaultComplexObjectValidationStrategy : IValidationStrategy
{
    /// <summary>
    /// Gets an instance of <see cref="DefaultComplexObjectValidationStrategy"/>.
    /// </summary>
    public static readonly IValidationStrategy Instance = new DefaultComplexObjectValidationStrategy();

    private DefaultComplexObjectValidationStrategy()
    {
    }

    /// <inheritdoc />
    public IEnumerator<ValidationEntry> GetChildren(
        ModelMetadata metadata,
        string key,
        object model)
    {
        return new Enumerator(metadata, key, model);
    }

    private sealed class Enumerator : IEnumerator<ValidationEntry>
    {
        private readonly string _key;
        private readonly object _model;
        private readonly int _count;
        private readonly ModelMetadata _modelMetadata;
        private readonly IReadOnlyList<ModelMetadata> _parameters;
        private readonly IReadOnlyList<ModelMetadata> _properties;

        private ValidationEntry _entry;
        private int _index;

        public Enumerator(
            ModelMetadata modelMetadata,
            string key,
            object model)
        {
            _modelMetadata = modelMetadata;
            _key = key;
            _model = model;

            if (_modelMetadata.BoundConstructor == null)
            {
                _parameters = Array.Empty<ModelMetadata>();
            }
            else
            {
                _modelMetadata.GetType().GetMethod("ThrowIfRecordTypeHasValidationOnProperties", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(modelMetadata, null);

                //_modelMetadata.ThrowIfRecordTypeHasValidationOnProperties();
                _parameters = _modelMetadata.BoundConstructor.BoundConstructorParameters!;
            }

            _properties = _modelMetadata.GetType().GetProperty("BoundProperties", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(modelMetadata) as IReadOnlyList<ModelMetadata>;
            _count = _properties.Count + _parameters.Count;

            _index = -1;
        }

        public ValidationEntry Current => _entry;

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            _index++;

            if (_index >= _count)
            {
                return false;
            }

            if (_index < _parameters.Count)
            {
                var parameter = _parameters[_index];
                var parameterName = parameter.BinderModelName ?? parameter.ParameterName;
                var key = ModelNames.CreatePropertyModelName(_key, parameterName);

                if (_model is null)
                {
                    _entry = new ValidationEntry(parameter, key, model: null);
                }
                else
                {
                    var parametersPerProperty = _modelMetadata.GetType().GetProperty("BoundConstructorParameterMapping", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .GetValue(_modelMetadata) as IReadOnlyDictionary<ModelMetadata, ModelMetadata>;

                    if (!parametersPerProperty.TryGetValue(parameter, out var property))
                    {
                        throw new InvalidOperationException(_modelMetadata.ModelType.Name);
                    }

                    _entry = new ValidationEntry(parameter, key, () => GetModel(_model, property));
                }
            }
            else
            {
                var property = _properties[_index - _parameters.Count];
                var propertyName = /*property.ValidationModelName ??*/ property.BinderModelName ?? property.PropertyName;
                var key = ModelNames.CreatePropertyModelName(_key, propertyName);

                if (_model == null)
                {
                    // Performance: Never create a delegate when container is null.
                    _entry = new ValidationEntry(property, key, model: null);
                }
                else
                {
                    _entry = new ValidationEntry(property, key, () => GetModel(_model, property));
                }
            }

            return true;
        }

        public void Dispose()
        {
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        private static object GetModel(object container, ModelMetadata property)
        {
            return property.PropertyGetter!(container);
        }
    }
}
