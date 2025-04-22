using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Recipient;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators.ExtensionsTests;

/// <summary>
/// Tests for the ValidatorRegistrationExtensions class for using assembly scan for dependency injection.
/// </summary>
public class ValidatorRegistrationExtensionsTests
{
    [Fact]
    public void AddValidatorsFromAssemblyWithDuplicateCheck_ThrowsException_WhenDuplicatesFound()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var assembly = new MockAssemblyWithDuplicates(); // Simulate an assembly with duplicate validators

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            serviceCollection.AddValidatorsFromAssemblyWithDuplicateCheck(assembly);
        });

        // Verify exception contains the expected type name
        Assert.Contains("NotificationOrderBaseExt", exception.Message);
    }

    [Fact]
    public void AddValidatorsFromAssemblyWithDuplicateCheck_Succeeds_WhenNoDuplicatesFound()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        // Create a mock assembly with no duplicates
        var assembly = new MockAssemblyWithoutDuplicates();

        // Act
        serviceCollection.AddValidatorsFromAssemblyWithDuplicateCheck(assembly);

        // Assert
        Assert.Contains(serviceCollection, d => d.ServiceType == typeof(IValidator<NotificationOrderBaseExt>));
        Assert.Contains(serviceCollection, d => d.ServiceType == typeof(IValidator<RecipientBaseExt>));
    }

    private class MockAssemblyWithDuplicates : Assembly
    {
        public override Type[] GetTypes()
        {
            return [
                typeof(DuplicateValidator1),
                typeof(DuplicateValidator2)
            ];
        }

        public override IEnumerable<TypeInfo> DefinedTypes => GetTypes().Select(t => t.GetTypeInfo());
    }

    // Helper class to simulate assembly without duplicates
    private class MockAssemblyWithoutDuplicates : Assembly
    {
        public override Type[] GetTypes()
        {
            return [typeof(UniqueValidator), typeof(UniqueValidator2)];
        }

        public override IEnumerable<TypeInfo> DefinedTypes => GetTypes().Select(t => t.GetTypeInfo());
    }

    public class UniqueValidator : AbstractValidator<NotificationOrderBaseExt>
    {
        public UniqueValidator()
        {
            RuleFor(s => s).NotEmpty();
        }
    }

    public class UniqueValidator2 : AbstractValidator<RecipientBaseExt>
    {
        public UniqueValidator2()
        {
            RuleFor(s => s).NotEmpty();
        }
    }

    /// <summary>
    /// Should be collected from the assembly.
    /// </summary>
    public class DuplicateValidator1 : AbstractValidator<NotificationOrderBaseExt>
    {
        public DuplicateValidator1()
        {
            RuleFor(s => s).NotEmpty();
        }
    }

    /// <summary>
    /// Should be collected from the assembly, and result in an exception thrown because both validators validate the same class.
    /// </summary>
    public class DuplicateValidator2 : AbstractValidator<NotificationOrderBaseExt>
    {
        public DuplicateValidator2()
        {
            RuleFor(s => s).NotEmpty();
        }
    }
}
