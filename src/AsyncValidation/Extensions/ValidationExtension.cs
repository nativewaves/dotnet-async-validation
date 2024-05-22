using Microsoft.AspNetCore.Mvc.AsyncValidation.Validation;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Mvc.AsyncValidation.Extensions;

public static class ValidationExtension
{
    public static IServiceCollection AddAsyncValidation(this IServiceCollection services)
    {
        services.AddSingleton<IConfigureOptions<MvcOptions>, ConfigureMVCOptionsSetup>();
        services.AddSingleton<ParameterBinder, AsyncParamterBinder>();
        services.AddSingleton<ValidatorCache>();
        services.TryAddSingleton<IAsyncObjectModelValidator>(s =>
        {
            var options = s.GetRequiredService<IOptions<MvcOptions>>().Value;
            var cache = s.GetRequiredService<ValidatorCache>();
            var metadataProvider = s.GetRequiredService<IModelMetadataProvider>();
            return new AsyncObjectModelValidator(metadataProvider, options.ModelValidatorProviders, cache, options);
        });
        return services;
    }

    internal sealed class ConfigureMVCOptionsSetup : IConfigureOptions<MvcOptions>
    {
        private readonly IStringLocalizerFactory _stringLocalizerFactory;
        private readonly IValidationAttributeAdapterProvider _validationAttributeAdapterProvider;
        private readonly IOptions<MvcDataAnnotationsLocalizationOptions> _dataAnnotationLocalizationOptions;

        public ConfigureMVCOptionsSetup(
            IValidationAttributeAdapterProvider validationAttributeAdapterProvider,
            IOptions<MvcDataAnnotationsLocalizationOptions> dataAnnotationLocalizationOptions)
        {
            ArgumentNullException.ThrowIfNull(validationAttributeAdapterProvider);
            ArgumentNullException.ThrowIfNull(dataAnnotationLocalizationOptions);

            _validationAttributeAdapterProvider = validationAttributeAdapterProvider;
            _dataAnnotationLocalizationOptions = dataAnnotationLocalizationOptions;
        }

        public ConfigureMVCOptionsSetup(
            IValidationAttributeAdapterProvider validationAttributeAdapterProvider,
            IOptions<MvcDataAnnotationsLocalizationOptions> dataAnnotationLocalizationOptions,
            IStringLocalizerFactory stringLocalizerFactory)
            : this(validationAttributeAdapterProvider, dataAnnotationLocalizationOptions)
        {
            _stringLocalizerFactory = stringLocalizerFactory;
        }

        public void Configure(MvcOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.ModelValidatorProviders.Insert(0, new DefaultAsyncModelValidatorProvider());
            options.ModelValidatorProviders.Insert(0, new AsyncDataAnnotationsModelValidatorProvider(
                _validationAttributeAdapterProvider,
                _dataAnnotationLocalizationOptions,
                _stringLocalizerFactory));
        }
    }
}
