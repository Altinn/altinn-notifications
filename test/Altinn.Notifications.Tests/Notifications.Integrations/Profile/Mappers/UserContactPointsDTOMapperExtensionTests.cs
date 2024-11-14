using Altinn.Notifications.Integrations.Profile.Mappers;
using Altinn.Notifications.Integrations.Profile.Models;
using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.Profile.Mappers;

public class UserContactPointsDTOMapperExtensionTests
{
    [Fact]
    public void ToUserContactPoint_NullValues_MapsCorrectly()   
    {
        // Arrange
        var userContactPointsDTO = new UserContactPointsDTO
        {
            UserId = null,
            NationalIdentityNumber = null,
            Email = null,
            IsReserved = false,
            MobileNumber = null
        };
    
        // Act
        var mappedResult = userContactPointsDTO.ToUserContactPoint();
    
        // Assert
        Assert.Equal(0, mappedResult.UserId);
        Assert.Equal(string.Empty, mappedResult.NationalIdentityNumber);
        Assert.Equal(string.Empty, mappedResult.Email);
        Assert.Equal(string.Empty, mappedResult.MobileNumber);
        Assert.False(mappedResult.IsReserved);
    }

    [Fact]
    public void ToUserContactPoint_WithValues_MapsCorrectly()   
    {
        // Arrange
        var testData = new
        {
            userId = 123,
            nationalIdentityNumber = "12345678910",
            email = "test@machinery.no",
            isReserved = false,
            mobileNumber = "+4712345678"
        };
        var userContactPointsDTO = new UserContactPointsDTO
        {
            UserId = testData.userId,
            NationalIdentityNumber = testData.nationalIdentityNumber,
            Email = testData.email,
            IsReserved = testData.isReserved,
            MobileNumber = testData.mobileNumber
        };
    
        // Act
        var mappedResult = userContactPointsDTO.ToUserContactPoint();
    
        // Assert
        Assert.Equal(testData.userId, mappedResult.UserId);
        Assert.Equal(testData.nationalIdentityNumber, mappedResult.NationalIdentityNumber);
        Assert.Equal(testData.email, mappedResult.Email);
        Assert.Equal(testData.mobileNumber, mappedResult.MobileNumber);
        Assert.Equal(testData.isReserved, mappedResult.IsReserved);
    }
}
