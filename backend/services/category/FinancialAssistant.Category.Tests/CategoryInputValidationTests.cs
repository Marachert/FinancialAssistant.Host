using System.Net;
using System.Net.Http.Json;
using System.Text;
using FinancialAssistant.Category.Application.Categories;
using FinancialAssistant.Category.Contracts;

namespace FinancialAssistant.Category.Tests;

public sealed class CategoryInputValidationTests
{
    [Theory]
    [InlineData(false, "{}")]
    [InlineData(false, "{\"userId\":null}")]
    [InlineData(true, "{}")]
    [InlineData(true, "{\"userId\":null}")]
    public async Task MissingRegistrationOwnerReturns400InBothAdapters(bool native, string body)
    {
        await using var factory = new CategoryContractWebApplicationFactory(native);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, CategoryApiRoutes.UserRegisteredEvent)
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add(CategoryGatewayHeaders.Authentication, CategoryContractWebApplicationFactory.GatewaySecret);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_registration_event", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedCorrelationReturns400BeforeAnyStoreAccess(bool native)
    {
        await using var factory = new CategoryContractWebApplicationFactory(native);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, "/categories/expense.food/aliases")
        { Content = JsonContent.Create(new UpdateCategoryAliasesRequest(new[] { "synthetic" }, new string('x', 257))) };
        request.Headers.Add(CategoryGatewayHeaders.Authentication, CategoryContractWebApplicationFactory.GatewaySecret);
        request.Headers.Add(CategoryGatewayHeaders.UserId, "synthetic-validation-owner");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_category_aliases", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void CorrelationBoundaryUsesNormalizedValueAndRejectsControlCharacters()
    {
        Assert.Equal("synthetic", CategoryService.ValidateAliasRequest(" synthetic ", "expense.food",
            new UpdateCategoryAliasesRequest(Array.Empty<string>(), " " + new string('x', 256) + " ")));
        Assert.Throws<ArgumentException>(() => CategoryService.ValidateAliasRequest("synthetic", "expense.food",
            new UpdateCategoryAliasesRequest(Array.Empty<string>(), "synthetic\ncorrelation")));
    }
}
