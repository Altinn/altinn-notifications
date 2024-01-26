using Altinn.Notifications.Sms.Core.Status;

using Altinn.Notifications.Sms.Integrations.LinkMobility;

using LinkMobility.PSWin.Client.Model;

using Moq;

using LinkMobilityModel = global::LinkMobility.PSWin.Client.Model;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations
{
    public class SmsClientTests
    {
        private readonly Mock<IAltinnGatewayClient> _clientMock = new();

        [Fact]
        public async Task SendAsync_GatewayReturnsNonSuccess_UnknownError()
        {
            // Arrange
            var gatewayResult = new MessageResult(null, false, "This is an unknown error message", null);

            _clientMock.Setup(cm => cm.SendAsync(It.IsAny<LinkMobilityModel.Sms>()))
                .ReturnsAsync(gatewayResult);

            SmsClient smsClient = new(_clientMock.Object);

            // Act
            var result = await smsClient.SendAsync(new Notifications.Sms.Core.Sending.Sms());

            // Assert
            Assert.True(result.IsError);
            await result.Match(
              async actualGatewayId =>
              {
                  await Task.CompletedTask;
                  throw new ArgumentException("Should not be able to get gateway reference from error response");
              },
              async actualErrorResponse =>
              {
                  await Task.CompletedTask;
                  Assert.Equal(SmsSendResult.Failed, actualErrorResponse.SendResult);
              });
        }

        [Fact]
        public async Task SendAsync_GatewayReturnsNonSuccess_InvalidReceiver()
        {
            // Arrange
            var gatewayResult = new MessageResult(null, false, "Invalid RCV '12345678'. Receiver number must be at least 9 digits.", null);

            _clientMock.Setup(cm => cm.SendAsync(It.IsAny<LinkMobilityModel.Sms>()))
                .ReturnsAsync(gatewayResult);

            SmsClient smsClient = new(_clientMock.Object);

            // Act
            var result = await smsClient.SendAsync(new Notifications.Sms.Core.Sending.Sms());

            // Assert
            Assert.True(result.IsError);
            await result.Match(
              async actualGatewayId =>
              {
                  await Task.CompletedTask;
                  throw new ArgumentException("Should not be able to get gateway reference from error response");
              },
              async actualErrorResponse =>
              {
                  await Task.CompletedTask;
                  Assert.Equal(SmsSendResult.Failed_InvalidReceiver, actualErrorResponse.SendResult);
                  Assert.NotNull(actualErrorResponse.ErrorMessage);
              });
        }

        [Fact]
        public async Task SendAsync_GatewayReturnsSuccess_GatewayRefReturned()
        {
            // Arrange
            string gatewayReference = Guid.NewGuid().ToString();

            var gatewayResult = new MessageResult(gatewayReference, true, string.Empty, new LinkMobilityModel.Sms(string.Empty, string.Empty, string.Empty));

            _clientMock.Setup(cm => cm.SendAsync(It.IsAny<LinkMobilityModel.Sms>()))
                .ReturnsAsync(gatewayResult);

            SmsClient smsClient = new(_clientMock.Object);

            // Act
            var result = await smsClient.SendAsync(new Notifications.Sms.Core.Sending.Sms());

            // Assert
            Assert.True(result.IsSuccess);

            await result.Match(
                async actualGatewayId =>
               {
                   await Task.CompletedTask;
                   Assert.Equal(gatewayReference, actualGatewayId);
               },
                async clientErrorResponse =>
                {
                    await Task.CompletedTask;
                    throw new ArgumentException("Should not be able to get client error response from success result");
                });
        }
    }
}
