# Async-Validation for ASP .Net Core

This library enhances the default validation mechanism in ASP .Net Core by introducing asynchronous capabilities. It is primarily derived from the codebases of [dotnet/aspnetcore](https://github.com/dotnet/aspnetcore) and [microsoft/referencesource](https://github.com/microsoft/referencesource), with modifications to support asynchronous operations while preserving the core functionalities. The `AsyncParameterBinder`, an extension of the traditional parameter binder, allows for asynchronous model binding through its `BindModelAsync()` method.

Synchronous validation is used as a fallback, when the asynchronous validators, `DefaultAsyncModelValidatorProvider` or `AsyncDataAnnotationsModelValidatorProvider`, do not yield any results, ensuring continuous validation functionality.

## Usage

To utilize the Async-Validation library in your ASP .Net Core project, start by integrating it into your service collection within the `Startup.cs` file or wherever you configure your services.

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddAsyncValidation();
}
```

## Using the Async Validator

The library provides an asynchronous variant of the traditional `Validator`, enabling non-blocking validation of models. This is particularly useful in scenarios requiring database lookups or other I/O operations during validation.

### Example

Here's a sample method demonstrating how to use the `ValidateObjectAsync` method from the async validator. This method is an asynchronous counterpart to the traditional `Validator.ValidateObject` method, offering the same interface with the added benefit of asynchronous execution.

```csharp
public async Task SomeMethodAsync(MyModel model, CancellationToken ct = default)
{
    var context = new ValidationContext(model, serviceProvider: null, items: null);
    await Validator.ValidateObjectAsync(
        instance: model,
        validationContext: context,
        validateAllProperties: true,
        cancellationToken: ct
    );
    // Async versions are available for all other public 'Validator' methods as well
}
```

In this example, `ValidateObjectAsync` is used to asynchronously validate an instance of `MyModel`. This method takes into account all properties of the model for validation and supports cancellation through the `CancellationToken`.
