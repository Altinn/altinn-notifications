using System;
using System.Text;

using Altinn.Notifications.Tools.AcsMock;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.AcsMock;

public class HmacValidatorTests
{
    private const string PathAndQuery = "/emails:send?api-version=2025-09-01";
    private static readonly byte[] _body = Encoding.UTF8.GetBytes("{}");

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void IsValid_WithAHeaderMissing_IsRejected(bool withoutDate, bool withoutContentHash, bool withoutAuthorization)
    {
        string date = DateTimeOffset.UtcNow.ToString("r");
        string hash = HmacValidator.ContentHash(_body);
        string authorization = HmacValidator.Sign(AcsMockFactory.AccessKey, "POST", PathAndQuery, "localhost", date, hash);

        var request = new SignedRequest(
            "POST",
            PathAndQuery,
            "localhost",
            withoutDate ? null : date,
            withoutContentHash ? null : hash,
            withoutAuthorization ? null : authorization,
            _body);

        bool valid = HmacValidator.IsValid(AcsMockFactory.AccessKey, request, out string reason);

        Assert.False(valid);
        Assert.Equal("x-ms-date, x-ms-content-sha256 or Authorization is missing", reason);
    }

    [Fact]
    public void IsValid_WhenSignedWithTheAccessKey_IsAccepted()
    {
        string date = DateTimeOffset.UtcNow.ToString("r");
        string hash = HmacValidator.ContentHash(_body);
        string authorization = HmacValidator.Sign(AcsMockFactory.AccessKey, "POST", PathAndQuery, "localhost", date, hash);

        var request = new SignedRequest("POST", PathAndQuery, "localhost", date, hash, authorization, _body);

        Assert.True(HmacValidator.IsValid(AcsMockFactory.AccessKey, request, out string reason));
        Assert.Empty(reason);
    }
}
