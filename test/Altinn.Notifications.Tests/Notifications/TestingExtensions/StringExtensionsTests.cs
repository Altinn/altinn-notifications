using Altinn.Notifications.Extensions;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingExtensions;

public class StringExtensionsTests
{
    [Theory]
    [InlineData("altinn.no", false)]
    [InlineData("No URLs here!", true)]
    [InlineData("http://localhost", false)]
    [InlineData("http://example.com", false)]
    [InlineData("http://192.168.1.1", false)]
    [InlineData("https://example.com", false)]
    [InlineData("http://127.0.0.1:8080", false)]
    [InlineData("user:pass@example.com", false)]
    [InlineData("https://127.0.0.1:8080", false)]
    [InlineData("ftp://example.com/resource", false)]
    [InlineData("http://example.com/path/with%20spaces", false)]
    [InlineData("Visit http://example.com for more info.", false)]
    [InlineData("http://example.com/path/with%2Fencoded%2Fslashes", false)]
    [InlineData("Text with newlines\nhttp://example.com\nMore text", false)]
    [InlineData("Check out https://www.example.com/path?query=string.", false)]
    [InlineData("Complete your payment here: www.example.com/payment.", false)]
    [InlineData("Please verify your account at http://www.example[dot]com/login.", false)]
    [InlineData("Your invoice is overdue. Pay now at http://bit.ly/securepayment.", false)]
    [InlineData("Your invoice is overdue. Pay now at http%3A%2F%2Fbit.ly%2Fsecurepayment.", false)]
    [InlineData("Click here to claim your prize: http://example.com/win?user=you&id=12345.", false)]
    [InlineData("Encoded: %68%74%74%70%3A%2F%2F%77%77%77%2E%65%78%61%6D%70%6C%65%2E%63%6F%6D", false)]
    [InlineData("Your account requires verification. Please go to https://example.com:8080/verify.", false)]
    [InlineData("Important notice: <a href='http://example.com/reset-password'>Reset your password here</a>.", false)]
    [InlineData("For immediate action, visit http%3A%2F%2F192.168.1.1%2Fsecurity%2Fupdate to secure your account.", false)]
    [InlineData("Alert: Unusual activity detected on your account. Log in at https://login.example-bank.com immediately.", false)]
    [InlineData("https://example.com/very-long-url-with-many-segments/that-are-really-long-and-may-cause-performance-issues", false)]
    [InlineData("Dear user, your account has been compromised. Please verify your details at http%3A%2F%2Fexample.com%2Flogin to avoid suspension.", false)]
    public void Validate_BodyMustNotContainUrl(string testString, bool expectedResult)
    {
        var result = testString.DoesNotContainUrl();
        Assert.Equal(expectedResult, result);
    }
}
