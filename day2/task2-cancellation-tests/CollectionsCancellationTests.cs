using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CancellationApi.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CancellationApi.Tests;

public class CollectionsCancellationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CollectionsCancellationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CancellingClientToken_mid_request_prevents_operation_completion()
    {
        using var client = _factory.CreateClient();

        using var cts = new CancellationTokenSource();

        // Cancel shortly after sending to simulate client abort
        cts.CancelAfter(200);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/collections/process");

        Task<HttpResponseMessage>? sendTask = null;
        try
        {
            sendTask = client.SendAsync(request, cts.Token);
            await sendTask;
        }
        catch (TaskCanceledException)
        {
            // expected when client cancels
        }

        // Inspect repository to ensure it did not complete
        var repo = _factory.Services.GetRequiredService<ICollectionRepository>();
        Assert.False(repo.Completed, "Repository operation should not have completed after client cancellation.");
    }
}
