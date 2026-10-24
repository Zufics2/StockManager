using StockManager.Models.DTOs;
using StockManager.Service.Validators;

namespace Tests.UnitTests;

public class ValidatorTests
{
    [Fact]
    public void WriteOff_WithoutReason_FailsWithClearMessage()
    {
        var result = new StockWriteOffRequestValidator().Validate(
            new StockWriteOffRequest { ProductId = 1, WarehouseId = 1, Quantity = 1, Reason = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "A reason is required for a write-off.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1_000_001)]
    public void Receipt_WithInvalidQuantity_Fails(int quantity)
    {
        var result = new StockReceiptRequestValidator().Validate(
            new StockReceiptRequest { ProductId = 1, WarehouseId = 1, Quantity = quantity });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(StockReceiptRequest.Quantity));
    }

    [Fact]
    public void Product_WithEveryFieldWrong_ReportsEachProblem()
    {
        var result = new ProductRequestValidator().Validate(
            new ProductRequest { Name = "", Sku = "bad sku!", Price = -1m, CategoryId = 0 });

        var fields = result.Errors.Select(e => e.PropertyName).Distinct().ToArray();
        Assert.Contains("Name", fields);
        Assert.Contains("Sku", fields);
        Assert.Contains("Price", fields);
        Assert.Contains("CategoryId", fields);
    }

    [Fact]
    public void Product_Valid_Passes() =>
        Assert.True(new ProductRequestValidator().Validate(
            new ProductRequest { Name = "Cable", Sku = "CBL-1.0_a", Price = 9.99m, CategoryId = 1 }).IsValid);

    [Fact]
    public void ProductFilter_MinGreaterThanMax_Fails()
    {
        var result = new ProductFilterValidator().Validate(new ProductFilterParameters { MinPrice = 10, MaxPrice = 5 });

        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("MinPrice must not be greater than MaxPrice"));
    }

    [Fact]
    public void ProductFilter_UnknownSortField_ListsAllowedFields()
    {
        var result = new ProductFilterValidator().Validate(new ProductFilterParameters { SortBy = "color" });

        var error = Assert.Single(result.Errors);
        Assert.Contains("price", error.ErrorMessage);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Paging_OutOfRange_Fails(int page, int pageSize)
    {
        var result = new ProductFilterValidator().Validate(new ProductFilterParameters { Page = page, PageSize = pageSize });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Register_WeakPassword_ReturnsAllPasswordProblems()
    {
        var result = new RegisterRequestValidator().Validate(
            new RegisterRequest { Email = "not-an-email", Password = "abc", FullName = "X" });

        Assert.Contains(result.Errors, e => e.PropertyName == "Email");
        Assert.True(result.Errors.Count(e => e.PropertyName == "Password") >= 3);
    }

    [Fact]
    public void AssignRole_UnknownRole_Fails() =>
        Assert.False(new AssignRoleRequestValidator().Validate(new AssignRoleRequest { Role = "Emperor" }).IsValid);
}
