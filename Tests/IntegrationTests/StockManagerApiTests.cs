using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Moq;
using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;

namespace Tests.IntegrationTests;

public class StockManagerApiTests : IClassFixture<StockManagerFactory>
{
    private const string AdminEmail = "admin@stockmanager.local";
    private const string AdminPassword = "Admin123!";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly StockManagerFactory _factory;

    public StockManagerApiTests(StockManagerFactory factory) => _factory = factory;

    // ---------------- helpers ----------------

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = AdminEmail, Password = AdminPassword });
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    // ---------------- tests ----------------

    [Fact]
    public async Task Anonymous_RequestToProtectedEndpoint_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Registration_WithWeakPassword_Returns400WithFieldMessages()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = "nope", Password = "abc", FullName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Password must be at least 8 characters long.", body);
        Assert.Contains("Email is not a valid email address.", body);
        Assert.Contains("Full name is required.", body);
    }

    [Fact]
    public async Task Viewer_CanRead_ButCannotCreateProducts()
    {
        var client = _factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = $"{Guid.NewGuid():N}@test.local", Password = "Passw0rd1", FullName = "Read Only" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var auth = await ReadAsync<AuthResponse>(register);
        Assert.Equal(new[] { "Viewer" }, auth.Roles.ToArray());

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/products")).StatusCode);

        var create = await client.PostAsJsonAsync("/api/products",
            new ProductRequest { Name = "X", Sku = "X-1", Price = 1, CategoryId = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit-logs")).StatusCode);
    }

    [Fact]
    public async Task FullInventoryFlow_ReceiptThenWriteOff_RespectsAvailableStock()
    {
        var admin = await AdminClientAsync();

        // --- catalog set-up ---
        var category = await ReadAsync<CategoryResponse>(await admin.PostAsJsonAsync("/api/categories",
            new CategoryRequest { Name = Unique("Cat") }));
        var warehouse = await ReadAsync<WarehouseResponse>(await admin.PostAsJsonAsync("/api/warehouses",
            new WarehouseRequest { Name = Unique("WH"), Location = "Almaty" }));
        var supplier = await ReadAsync<SupplierResponse>(await admin.PostAsJsonAsync("/api/suppliers",
            new SupplierRequest { Name = "Acme", ContactEmail = "sales@acme.test" }));

        var createProduct = await admin.PostAsJsonAsync("/api/products", new ProductRequest
        {
            Name = "USB-C cable", Sku = Unique("sku"), Price = 4.99m, CategoryId = category.Id, SupplierId = supplier.Id
        });
        Assert.Equal(HttpStatusCode.Created, createProduct.StatusCode);
        Assert.NotNull(createProduct.Headers.Location);
        var product = await ReadAsync<ProductResponse>(createProduct);
        Assert.Equal(0, product.TotalStock);

        // --- receipt: +10 ---
        var receipt = await admin.PostAsJsonAsync("/api/stock-movements/receipts", new StockReceiptRequest
        {
            ProductId = product.Id, WarehouseId = warehouse.Id, Quantity = 10, SupplierId = supplier.Id, UnitCost = 2m, Note = "PO-1"
        });
        Assert.Equal(HttpStatusCode.Created, receipt.StatusCode);
        Assert.Equal(10, (await ReadAsync<StockMovementResponse>(receipt)).QuantityAfter);

        // --- write-off: -4 is fine ---
        var ok = await admin.PostAsJsonAsync("/api/stock-movements/write-offs", new StockWriteOffRequest
        {
            ProductId = product.Id, WarehouseId = warehouse.Id, Quantity = 4, Reason = "Damaged"
        });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal(6, (await ReadAsync<StockMovementResponse>(ok)).QuantityAfter);

        // --- write-off of 100 must be refused with a clear message and change nothing ---
        var tooMuch = await admin.PostAsJsonAsync("/api/stock-movements/write-offs", new StockWriteOffRequest
        {
            ProductId = product.Id, WarehouseId = warehouse.Id, Quantity = 100, Reason = "Oops"
        });
        Assert.Equal(HttpStatusCode.Conflict, tooMuch.StatusCode);
        var problem = await tooMuch.Content.ReadAsStringAsync();
        Assert.Contains("Insufficient stock", problem);
        Assert.Contains("requested 100, available 6", problem);

        // --- write-off without a reason is a validation error ---
        var noReason = await admin.PostAsJsonAsync("/api/stock-movements/write-offs", new StockWriteOffRequest
        {
            ProductId = product.Id, WarehouseId = warehouse.Id, Quantity = 1, Reason = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        // --- balances and history ---
        var stock = await ReadAsync<PagedResult<StockLevelResponse>>(await admin.GetAsync($"/api/warehouses/{warehouse.Id}/stock"));
        Assert.Equal(6, Assert.Single(stock.Items).Quantity);

        var history = await ReadAsync<PagedResult<StockMovementResponse>>(
            await admin.GetAsync($"/api/stock-movements?productId={product.Id}"));
        Assert.Equal(2, history.TotalCount);

        var reloaded = await ReadAsync<ProductResponse>(await admin.GetAsync($"/api/products/{product.Id}"));
        Assert.Equal(6, reloaded.TotalStock);

        // --- a product with history cannot be deleted ---
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/products/{product.Id}")).StatusCode);

        // --- audit journal recorded the operations ---
        var audit = await ReadAsync<PagedResult<AuditLogResponse>>(
            await admin.GetAsync($"/api/audit-logs?entityName=StockMovement&pageSize=50"));
        Assert.Contains(audit.Items, a => a.Action == "Create" && a.UserName == AdminEmail);
    }

    [Fact]
    public async Task Products_SupportSearchFilterSortAndPaging()
    {
        var admin = await AdminClientAsync();
        var category = await ReadAsync<CategoryResponse>(await admin.PostAsJsonAsync("/api/categories",
            new CategoryRequest { Name = Unique("Cat") }));

        foreach (var (name, price) in new[] { ("Alpha lamp", 30m), ("Beta lamp", 10m), ("Gamma lamp", 20m), ("Delta chair", 99m) })
        {
            var r = await admin.PostAsJsonAsync("/api/products", new ProductRequest
            {
                Name = name, Sku = Unique("s"), Price = price, CategoryId = category.Id
            });
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        }

        var page = await ReadAsync<PagedResult<ProductResponse>>(await admin.GetAsync(
            $"/api/products?categoryId={category.Id}&search=lamp&sortBy=price&sortDirection=asc&page=1&pageSize=2"));

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(new[] { "Beta lamp", "Gamma lamp" }, page.Items.Select(p => p.Name).ToArray());

        var priced = await ReadAsync<PagedResult<ProductResponse>>(await admin.GetAsync(
            $"/api/products?categoryId={category.Id}&minPrice=25&maxPrice=50"));
        Assert.Equal("Alpha lamp", Assert.Single(priced.Items).Name);

        var invalid = await admin.GetAsync("/api/products?minPrice=50&maxPrice=10&pageSize=1000");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task DuplicateSku_Returns409()
    {
        var admin = await AdminClientAsync();
        var category = await ReadAsync<CategoryResponse>(await admin.PostAsJsonAsync("/api/categories",
            new CategoryRequest { Name = Unique("Cat") }));
        var sku = Unique("dup");
        var body = new ProductRequest { Name = "P", Sku = sku, Price = 1m, CategoryId = category.Id };

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/products", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/products", body)).StatusCode);
    }

    [Fact]
    public async Task UnknownProduct_Returns404ProblemDetails()
    {
        var admin = await AdminClientAsync();

        var response = await admin.GetAsync("/api/products/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("was not found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ImageUpload_WithDisallowedType_Returns400()
    {
        var admin = await AdminClientAsync();
        var category = await ReadAsync<CategoryResponse>(await admin.PostAsJsonAsync("/api/categories",
            new CategoryRequest { Name = Unique("Cat") }));
        var product = await ReadAsync<ProductResponse>(await admin.PostAsJsonAsync("/api/products",
            new ProductRequest { Name = "P", Sku = Unique("img"), Price = 1m, CategoryId = category.Id }));

        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent("MZ not an image"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, "file", "virus.exe");

        var response = await admin.PostAsync($"/api/products/{product.Id}/image", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not allowed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PriceConversion_UsesExternalRate_AndReturns503WhenProviderIsDown()
    {
        var admin = await AdminClientAsync();
        var category = await ReadAsync<CategoryResponse>(await admin.PostAsJsonAsync("/api/categories",
            new CategoryRequest { Name = Unique("Cat") }));
        var product = await ReadAsync<ProductResponse>(await admin.PostAsJsonAsync("/api/products",
            new ProductRequest { Name = "P", Sku = Unique("prc"), Price = 10m, CategoryId = category.Id }));

        _factory.Currency.Setup(c => c.GetRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRateResult("USD", "EUR", 0.9m, DateTime.UtcNow));
        _factory.Currency.Setup(c => c.GetRateAsync("USD", "KZT", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StockManager.Models.Exceptions.ExternalServiceException("provider down"));

        var ok = await ReadAsync<ProductPriceResponse>(await admin.GetAsync($"/api/products/{product.Id}/price?currency=eur"));
        Assert.Equal(9.00m, ok.ConvertedPrice);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await admin.GetAsync($"/api/products/{product.Id}/price?currency=KZT")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/products/{product.Id}/price?currency=12")).StatusCode);
    }

    [Fact]
    public async Task Admin_CanPromoteUser_AndPromotedUserGainsAccess()
    {
        var admin = await AdminClientAsync();
        var anon = _factory.CreateClient();

        var email = $"{Guid.NewGuid():N}@test.local";
        var registered = await ReadAsync<AuthResponse>(await anon.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "Passw0rd1", FullName = "Future Manager" }));

        var promote = await admin.PutAsJsonAsync($"/api/auth/users/{registered.UserId}/role", new AssignRoleRequest { Role = "Manager" });
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);

        // roles are baked into the JWT, so the user logs in again to receive the new one
        var relogin = await ReadAsync<AuthResponse>(await anon.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = "Passw0rd1" }));
        Assert.Equal(new[] { "Manager" }, relogin.Roles.ToArray());

        var manager = _factory.CreateClient();
        manager.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", relogin.Token);
        var created = await manager.PostAsJsonAsync("/api/categories", new CategoryRequest { Name = Unique("Cat") });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // ... but Manager still cannot delete (Admin only)
        var category = await ReadAsync<CategoryResponse>(created);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.DeleteAsync($"/api/categories/{category.Id}")).StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = AdminEmail, Password = "wrong-Password1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EveryResponse_CarriesACorrelationId()
    {
        var response = await _factory.CreateClient().GetAsync("/api/products");

        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }
}
