using Xunit;

namespace Altinn.Notifications.Email.Tests
{
    /// <summary>
    /// Demo test class.
    /// </summary>
    public class DemoTests
    {
        /// <summary>
        /// Initial demo test case.
        /// </summary>
        public static void Test01()
        {
            string actual = "this is a test";

            Assert.Equal("This is a test", actual);
        }
    }
}
