using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.SendCondition;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Shared;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class SendConditionEvaluatorTests
{
    private static readonly Uri _endpoint = new("http://test.com/condition");

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Evaluate_SuccessfulCheck_ReturnsEndpointResult(bool endpointResult, bool isRetry)
    {
        var evaluator = CreateEvaluator(endpointResult);

        var result = await evaluator.Evaluate(_endpoint, Guid.NewGuid(), isRetry);

        Assert.Equal(endpointResult, result.IsSendConditionMet);
    }

    [Fact]
    public async Task Evaluate_FailedCheckOnFirstAttempt_ReturnsInconclusive()
    {
        var evaluator = CreateEvaluator(new ConditionClientError { StatusCode = 500 });

        var result = await evaluator.Evaluate(_endpoint, Guid.NewGuid(), isRetry: false);

        Assert.Null(result.IsSendConditionMet);
    }

    [Fact]
    public async Task Evaluate_FailedCheckOnRetry_TreatsConditionAsMet()
    {
        var evaluator = CreateEvaluator(new ConditionClientError { StatusCode = 500 });

        var result = await evaluator.Evaluate(_endpoint, Guid.NewGuid(), isRetry: true);

        Assert.True(result.IsSendConditionMet);
    }

    private static SendConditionEvaluator CreateEvaluator(Result<bool, ConditionClientError> clientResult)
    {
        var conditionClient = new Mock<IConditionClient>();
        conditionClient
            .Setup(c => c.CheckSendCondition(It.IsAny<Uri>()))
            .ReturnsAsync(clientResult);

        return new SendConditionEvaluator(conditionClient.Object, new Mock<ILogger<SendConditionEvaluator>>().Object);
    }
}
