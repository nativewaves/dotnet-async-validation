using Microsoft.AspNetCore.Mvc.AsyncValidation.Validation;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.AsyncValidation.Test;

public class Tests
{
    private readonly IServiceCollection _services = new ServiceCollection();
    private const string AvailableString = "available";

    public Tests()
    {
        _services.AddSingleton<ITestService, TestService>();
    }

    public interface ITestService
    {
        bool IsCalled { get; }
        Task<bool> IsInDbAsync(string value, CancellationToken ct = default);
    }

    internal class TestService : ITestService
    {
        public bool IsCalled { get; set; }

        public Task<bool> IsInDbAsync(string value, CancellationToken ct = default) // we pretend
        {
            IsCalled = true;
            return Task.FromResult(value == AvailableString);
        }
    }

    internal class StringInDatabaseAttribute : AsyncValidationAttribute
    {
        protected override async Task<ValidationResult> IsValidAsync(object value, ValidationContext validationContext, CancellationToken ct = default)
        {
            if (value is string strValue)
            {
                var service = validationContext.GetRequiredService<ITestService>();

                if (await service.IsInDbAsync(strValue, ct) == false)
                {
                    return new ValidationResult($"Invalid string {value}.");
                }
            }

            return ValidationResult.Success;
        }
    }

    internal class TestModel
    {
        [StringInDatabase] public string MyDbString { get; set; }
    }

    internal class TestModelSyncValidated
    {
        [Required] public string MyString { get; set; }
    }

    [Fact]
    internal async Task Validate_ValidData_ValidateOk()
    {
        var provider = _services.BuildServiceProvider();
        var service = provider.GetService<ITestService>();

        var target = new TestModel { MyDbString = AvailableString };

        await Should.NotThrowAsync(AsyncValidator.ValidateObjectAsync(
            instance: target,
            validationContext: new ValidationContext(target, provider, null),
            validateAllProperties: true,
            ct: CancellationToken.None));

        // ensure validation was called
        service.IsCalled.ShouldBeTrue();
    }

    [Fact]
    internal async Task Validate_InvalidData_ValidateThrows()
    {
        var provider = _services.BuildServiceProvider();

        var target = new TestModel { MyDbString = "invalid" };

        await Should.ThrowAsync<ValidationException>(AsyncValidator.ValidateObjectAsync(
            instance: target,
            validationContext: new ValidationContext(target, provider, null),
            validateAllProperties: true,
            ct: CancellationToken.None));
    }

    [Fact]
    internal void ValidateSynchronously_ValidateThrows()
    {
        var provider = _services.BuildServiceProvider();

        var target = new TestModel { MyDbString = "invalid" };

        Should.Throw<InvalidOperationException>(() => Validator.ValidateObject(
            instance: target,
            validationContext: new ValidationContext(target, provider, null),
            validateAllProperties: true))
            .Message.ShouldBe("Async validation called synchronously.");
    }

    [Fact]
    internal async Task ValidateAsync_ValidatesSynchronousValidationAttributesAsync()
    {
        var target = new TestModelSyncValidated { MyString = null }; // invalid

        await Should.ThrowAsync<ValidationException>(() => AsyncValidator.ValidateObjectAsync(
            instance: target,
            validationContext: new ValidationContext(target),
            validateAllProperties: true,
            CancellationToken.None));
    }
}
