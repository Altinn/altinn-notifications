using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Common.AccessTokenClient.Services;
using Altinn.Notifications.Integrations;
using Altinn.Notifications.Tests.Mocks;
using Altinn.Platform.Profile.Models;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.UnitTests.Integrations
{
    public class ProfileClientTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        };

        private Mock<IOptions<PlatformSettings>> _platformSettings = new Mock<IOptions<PlatformSettings>>();
        private Mock<IAccessTokenGenerator> _accessTokenGenerator = new Mock<IAccessTokenGenerator>();
        private Mock<IUserTokenProvider> _userTokenProvider = new Mock<IUserTokenProvider>();

        public ProfileClientTests()
        {
            _platformSettings.Setup(s => s.Value).Returns(new PlatformSettings
            {
                ProfileEndpointAddress = "http://real.domain.com",
                ProfileSubscriptionKeyHeaderName = "arealheadername",
                ProfileSubscriptionKey = "notarealkey"
            }); 
            
            _accessTokenGenerator.Setup(a => a.GenerateAccessToken(
                It.Is<string>(issuer => issuer == "platform"), 
                It.Is<string>(app => app == "notification"))).Returns("accesstoken");

            _userTokenProvider.Setup(u => u.GetUserToken()).Returns(
                "usertoken");
        }

        [Fact]
        public async Task GetUserProfile_InputValidUserId_ProfileReturnsOk_ReturnsCorrectProfile()
        {
            // Arrange
            const int UserId = 23;

            HttpRequestMessage? sblRequest = null;
            DelegatingHandlerStub messageHandler = new(async (HttpRequestMessage request, CancellationToken token) =>
            {
                sblRequest = request;

                return await Task.FromResult(new HttpResponseMessage
                {
                    Content = JsonContent.Create(
                        new UserProfile
                        {
                            UserId = UserId
                        },
                        options: _jsonSerializerOptions)
                });
            });

            var target = new ProfileClient(
                new HttpClient(messageHandler), 
                _platformSettings.Object, 
                _accessTokenGenerator.Object,
                _userTokenProvider.Object);

            // Act
            var actual = await target.GetUserProfile(UserId, CancellationToken.None);

            // Assert
            _accessTokenGenerator.VerifyAll();
            _userTokenProvider.VerifyAll();

            Assert.Equal(HttpMethod.Get, sblRequest!.Method);
            Assert.Equal("Bearer usertoken", sblRequest!.Headers.Authorization!.ToString());
            Assert.Equal("accesstoken", sblRequest!.Headers.GetValues("PlatformAccessToken").First());
            Assert.StartsWith("http://real.domain.com", sblRequest!.RequestUri!.ToString());
            Assert.EndsWith("users/23", sblRequest!.RequestUri!.ToString());
            Assert.Equal("notarealkey", sblRequest!.Headers.GetValues("arealheadername").First());

            Assert.Equal(UserId, actual!.UserId);
        }

        [Fact]
        public async Task GetUserProfile_InputInvalidUserId_ProfileReturnsNotFound_ReturnsNull()
        {
            HttpRequestMessage? sblRequest = null;
            DelegatingHandlerStub messageHandler = new(async (HttpRequestMessage request, CancellationToken token) =>
            {
                sblRequest = request;

                return await Task.FromResult(new HttpResponseMessage() { StatusCode = HttpStatusCode.NotFound });
            });

            var target = new ProfileClient(
                new HttpClient(messageHandler),
                _platformSettings.Object,
                _accessTokenGenerator.Object,
                _userTokenProvider.Object);

            // Act
            var actual = await target.GetUserProfile(23, CancellationToken.None);

            // Assert
            Assert.Null(actual);
        }

        [Fact]
        public async Task GetUserProfile_InputValidUserId_ProfileReturnsUnhandledStatusCode_ThrowsException()
        {
            HttpRequestMessage? sblRequest = null;
            DelegatingHandlerStub messageHandler = new(async (HttpRequestMessage request, CancellationToken token) =>
            {
                sblRequest = request;

                return await Task.FromResult(new HttpResponseMessage() 
                    { 
                        StatusCode = HttpStatusCode.ServiceUnavailable, 
                        Content = new StringContent("Error message.")
                    });
            });

            var target = new ProfileClient(
                new HttpClient(messageHandler),
                _platformSettings.Object,
                _accessTokenGenerator.Object,
                _userTokenProvider.Object);

            // Act
            UnhandledHttpResponseException? actual = null;
            try
            {
                _ = await target.GetUserProfile(23, CancellationToken.None);
            }
            catch (UnhandledHttpResponseException ex)
            {
                actual = ex;
            }

            // Assert
            Assert.NotNull(actual);
            Assert.Equal((int)HttpStatusCode.ServiceUnavailable, actual!.StatusCode);
            Assert.Equal("Service Unavailable", actual!.ReasonPhrase);
            Assert.Equal("Error message.", actual!.Content);
        }
    }
}
