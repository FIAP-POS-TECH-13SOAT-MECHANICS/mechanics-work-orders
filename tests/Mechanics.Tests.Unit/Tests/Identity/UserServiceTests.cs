using Mechanics.Application.Identity.Services;
using System.Net;
using System.Text;

namespace Mechanics.Tests.Unit.Tests.Identity;

[TestClass]
[TestCategory("Identity")]
public class UserServiceTests
{
    [TestMethod("Retorna null quando usuário não existe")]
    public async Task It_ShouldReturnNull_WhenUserIsNotFound()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var service = new UserService(client);

        var result = await service.GetUserById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod("Retorna usuário desserializado quando endpoint responde sucesso")]
    public async Task It_ShouldReturnUser_WhenResponseIsSuccessful()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var json = $$"""
                     {
                       "id": "{{userId}}",
                       "fullName": "Mecanico Teste",
                       "cpfNumber": "11144477735",
                       "role": {
                         "id": "{{roleId}}",
                         "name": "Mechanic"
                       }
                     }
                     """;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.AreEqual($"/identity/users/{userId}", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var service = new UserService(client);

        var result = await service.GetUserById(userId, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(userId, result.Id);
        Assert.AreEqual("Mecanico Teste", result.FullName);
        Assert.AreEqual("11144477735", result.CpfNumber);
        Assert.AreEqual(roleId, result.Role.Id);
        Assert.AreEqual("Mechanic", result.Role.Name);
    }

    [TestMethod("Lança exceção quando endpoint responde erro diferente de not found")]
    public async Task It_ShouldThrow_WhenResponseIsNotSuccessful()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var service = new UserService(client);

        await Assert.ThrowsExactlyAsync<HttpRequestException>(async () =>
            await service.GetUserById(Guid.NewGuid(), CancellationToken.None));
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
