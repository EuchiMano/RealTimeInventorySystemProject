using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RealTimeInventorySystem.DTOs;
using Xunit;

namespace Tests.RealTimeInventorySystem.Tests;

public class InventoryApiTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public InventoryApiTests(
        CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static HttpRequestMessage CreatePatch(
        string uri,
        object body)
    {
        var request =
            new HttpRequestMessage(
                new HttpMethod("PATCH"),
                uri);

        request.Content =
            new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

        return request;
    }

    [Fact]
    public async Task Patch_Should_Be_Idempotent()
    {
        var inventory =
            await _client.GetFromJsonAsync<InventoryResponse>(
                "/api/inventory/1/1");

        Assert.NotNull(inventory);

        var request = new UpdateStockRequest
        {
            WarehouseId = 1,
            Quantity = 100,
            Version = inventory!.Version
        };

        var firstResponse =
            await _client.SendAsync(
                CreatePatch("/api/inventory/1", request));

        Assert.Equal(
            HttpStatusCode.NoContent,
            firstResponse.StatusCode);

        var updatedInventory =
            await _client.GetFromJsonAsync<InventoryResponse>(
                "/api/inventory/1/1");

        request.Version = updatedInventory!.Version;

        var secondResponse =
            await _client.SendAsync(
                CreatePatch("/api/inventory/1", request));

        Assert.Equal(
            HttpStatusCode.NoContent,
            secondResponse.StatusCode);

        var finalInventory =
            await _client.GetFromJsonAsync<InventoryResponse>(
                "/api/inventory/1/1");

        Assert.Equal(100, finalInventory!.Quantity);
    }

    [Fact]
    public async Task Patch_With_Negative_Quantity_Should_Return_BadRequest()
    {
        var request = new UpdateStockRequest
        {
            WarehouseId = 1,
            Quantity = -10,
            Version = 0
        };

        var response =
            await _client.SendAsync(
                CreatePatch("/api/inventory/1", request));

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }
}