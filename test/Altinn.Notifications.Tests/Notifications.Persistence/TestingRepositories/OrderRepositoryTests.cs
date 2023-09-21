using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Persistence.Repository;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Persistence.TestingRepositories;

public class OrderRepositoryTests
{
    [Fact]
    public void ExtractTemplates_EmailTemplate()
    {
        // Arrange
        EmailTemplate expected = new()
        {
            Body = "body",
            ContentType = EmailContentType.Plain,
            FromAddress = "from@domain.com",
            Subject = "subject",
            Type = NotificationTemplateType.Email
        };

        NotificationOrder input = new()
        {
            Templates = new()
            {
                new EmailTemplate("from@domain.com", "subject", "body", EmailContentType.Plain)
            }
        };

        // Act
        EmailTemplate? actual = OrderRepository.ExtractTemplates(input);

        // Assert
        Assert.NotNull(actual);
        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public void ExtractTemplates_EmptyListOfTemplates_NullForAllTemplates()
    {
        // Arrange
        NotificationOrder input = new();

        // Act
        EmailTemplate? actualEmailTemplate = OrderRepository.ExtractTemplates(input);

        // Assert
        Assert.Null(actualEmailTemplate);
    }
}
