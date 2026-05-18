using System.Net.Sockets;

using Xunit;

namespace Altinn.Notifications.Shared.TestInfrastructure.Utils;

/// <summary>
/// A custom xUnit <see cref="FactAttribute"/> that automatically skips the test
/// when a Kafka broker is not reachable at localhost:9092.
/// </summary>
/// <example>
/// <code>
/// [KafkaAvailableFact]
/// public async Task MyKafkaTest()
/// {
///     // test body — skipped automatically when Kafka is unavailable
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class KafkaAvailableFactAttribute : FactAttribute
{
    private const string _brokerHost = "localhost";
    private const int _brokerPort = 9092;
    private const string _skipReason = "Kafka broker is not available at localhost:9092.";

    private static readonly Lazy<bool> _isAvailable = new(CheckAvailability);

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaAvailableFactAttribute"/> class.
    /// Automatically sets <see cref="FactAttribute.Skip"/> if the Kafka broker is unreachable.
    /// </summary>
    public KafkaAvailableFactAttribute()
    {
        if (!_isAvailable.Value)
        {
            Skip = _skipReason;
        }
    }

    /// <summary>
    /// Attempts a TCP connection to the Kafka broker with a 500 ms timeout.
    /// </summary>
    /// <returns><c>true</c> if the broker is reachable; otherwise <c>false</c>.</returns>
    private static bool CheckAvailability()
    {
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            using var client = new TcpClient();
            client.ConnectAsync(_brokerHost, _brokerPort, cancellationTokenSource.Token).GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// A custom xUnit <see cref="TheoryAttribute"/> that automatically skips the test
/// when a Kafka broker is not reachable at localhost:9092.
/// </summary>
/// <example>
/// <code>
/// [KafkaAvailableTheory]
/// [InlineData("topic-a")]
/// [InlineData("topic-b")]
/// public async Task MyParameterizedKafkaTest(string topicName)
/// {
///     // test body — skipped automatically when Kafka is unavailable
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class KafkaAvailableTheoryAttribute : TheoryAttribute
{
    private const string _brokerHost = "localhost";
    private const int _brokerPort = 9092;
    private const string _skipReason = "Kafka broker is not available at localhost:9092.";

    private static readonly Lazy<bool> _isAvailable = new(CheckAvailability);

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaAvailableTheoryAttribute"/> class.
    /// Automatically sets <see cref="TheoryAttribute.Skip"/> if the Kafka broker is unreachable.
    /// </summary>
    public KafkaAvailableTheoryAttribute()
    {
        if (!_isAvailable.Value)
        {
            Skip = _skipReason;
        }
    }

    /// <summary>
    /// Attempts a TCP connection to the Kafka broker with a 500 ms timeout.
    /// </summary>
    /// <returns><c>true</c> if the broker is reachable; otherwise <c>false</c>.</returns>
    private static bool CheckAvailability()
    {
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            using var client = new TcpClient();
            client.ConnectAsync(_brokerHost, _brokerPort, cancellationTokenSource.Token).GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

