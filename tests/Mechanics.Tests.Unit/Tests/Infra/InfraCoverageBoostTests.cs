using Amazon.SQS;
using Amazon.SQS.Model;
using Mechanics.Infra.Messaging.Helpers;
using Mechanics.Infra.Messaging.Options;
using Mechanics.Infra.Messaging.Publishers;
using Mechanics.Infra.Security;
using Mechanics.Infra.Security.Extensions;
using Mechanics.Infra.Security.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Security.Claims;

namespace Mechanics.Tests.Unit.Tests.Infra;

[TestClass]
[TestCategory("Infra")]
public class InfraCoverageBoostTests
{
    [TestMethod("QueueUrlResolver deve resolver URL e reutilizar cache por evento")]
    public async Task It_ShouldResolveQueueUrlAndReuseCache()
    {
        var sqsClient = new Mock<IAmazonSQS>();
        sqsClient.Setup(mock => mock.GetQueueUrlAsync("queue-sample", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                QueueUrl = "https://sqs.local/queue-sample",
            });
        var options = Options.Create(new MessagingOptions
        {
            DisableConsumers = false,
            QueueNames = new Dictionary<string, string> { ["Sample"] = "queue-sample" },
        });
        var resolver = new QueueUrlResolver(sqsClient.Object, options);

        var firstUrl = await resolver.ResolveAsync<SampleEvent>(CancellationToken.None);
        var secondUrl = await resolver.ResolveAsync<SampleEvent>(CancellationToken.None);

        Assert.AreEqual("https://sqs.local/queue-sample", firstUrl);
        Assert.AreEqual(firstUrl, secondUrl);
        sqsClient.Verify(mock => mock.GetQueueUrlAsync("queue-sample", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod("QueueUrlResolver deve falhar quando evento não está configurado")]
    public async Task It_ShouldThrow_WhenQueueIsNotConfigured()
    {
        var sqsClient = new Mock<IAmazonSQS>();
        var options = Options.Create(new MessagingOptions
        {
            DisableConsumers = false,
            QueueNames = new Dictionary<string, string>(),
        });
        var resolver = new QueueUrlResolver(sqsClient.Object, options);

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await resolver.ResolveAsync<SampleEvent>(CancellationToken.None));

        StringAssert.Contains(exception.Message, "Queue not configured");
    }

    [TestMethod("QueueUrlResolver deve falhar quando SQS retorna status diferente de 200")]
    public async Task It_ShouldThrow_WhenQueueUrlResponseIsNotOk()
    {
        var sqsClient = new Mock<IAmazonSQS>();
        sqsClient.Setup(mock => mock.GetQueueUrlAsync("queue-sample", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse
            {
                HttpStatusCode = HttpStatusCode.InternalServerError,
                QueueUrl = string.Empty,
            });
        var options = Options.Create(new MessagingOptions
        {
            DisableConsumers = false,
            QueueNames = new Dictionary<string, string> { ["Sample"] = "queue-sample" },
        });
        var resolver = new QueueUrlResolver(sqsClient.Object, options);

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await resolver.ResolveAsync<SampleEvent>(CancellationToken.None));

        StringAssert.Contains(exception.Message, "Error fetching URL");
    }

    [TestMethod("QueueUrlResolver deve respeitar cancelamento imediato")]
    public async Task It_ShouldThrow_WhenResolveIsCancelled()
    {
        var sqsClient = new Mock<IAmazonSQS>();
        var options = Options.Create(new MessagingOptions
        {
            DisableConsumers = false,
            QueueNames = new Dictionary<string, string> { ["Sample"] = "queue-sample" },
        });
        var resolver = new QueueUrlResolver(sqsClient.Object, options);
        var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await resolver.ResolveAsync<SampleEvent>(cancellationTokenSource.Token));
    }

    [TestMethod("EventPublisher deve publicar mensagem serializada na fila resolvida")]
    public async Task It_ShouldPublishMessageToResolvedQueue()
    {
        var sqsClient = new Mock<IAmazonSQS>();
        sqsClient.Setup(mock => mock.GetQueueUrlAsync("queue-publish", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                QueueUrl = "https://sqs.local/queue-publish",
            });

        SendMessageRequest? capturedRequest = null;
        sqsClient.Setup(mock => mock.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendMessageRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new SendMessageResponse { HttpStatusCode = HttpStatusCode.OK });

        var resolver = new QueueUrlResolver(sqsClient.Object, Options.Create(new MessagingOptions
        {
            DisableConsumers = false,
            QueueNames = new Dictionary<string, string> { ["Publish"] = "queue-publish" },
        }));
        var publisher = new EventPublisher(sqsClient.Object, resolver);
        var payload = new PublishEvent { Code = "EVT-123" };

        await publisher.PublishAsync(payload, CancellationToken.None);

        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual("https://sqs.local/queue-publish", capturedRequest.QueueUrl);
        StringAssert.Contains(capturedRequest.MessageBody, "EVT-123");
        sqsClient.Verify(mock => mock.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod("CurrentUserService deve montar e cachear dados do usuário")]
    public void It_ShouldBuildAndCacheCurrentUserData()
    {
        var userId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "MECHANIC"),
            new Claim("customerId", customerId.ToString()),
        ], "jwt"));
        var service = new CurrentUserService(principal);

        var firstCall = service.GetData();
        var secondCall = service.GetData();

        Assert.AreEqual(userId, firstCall.UserId);
        Assert.AreEqual(customerId, firstCall.CustomerId);
        Assert.AreEqual("MECHANIC", firstCall.Role);
        Assert.AreSame(firstCall, secondCall);
    }

    [TestMethod("CurrentUserService deve lançar quando claims obrigatórias não existem")]
    public void It_ShouldThrow_WhenRequiredClaimsAreMissing()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "MECHANIC")], "jwt"));
        var service = new CurrentUserService(principal);

        Assert.ThrowsExactly<ArgumentNullException>(() => service.GetData());
    }

    [TestMethod("AuthExtensions deve registrar autenticação e CurrentUserService")]
    public void It_ShouldRegisterAuthenticationAndCurrentUserService()
    {
        var services = new ServiceCollection();

        services.AddJwtAuthentication(validateInDebugMode: false);

        Assert.IsTrue(services.Any(descriptor => descriptor.ServiceType == typeof(ICurrentUserService)));
        Assert.IsTrue(services.Any(descriptor => descriptor.ServiceType == typeof(IHttpContextAccessor)));
    }

    private sealed class SampleEvent;

    private sealed class PublishEvent
    {
        public required string Code { get; init; }
    }
}
