using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using Application.Validators;
using Domain.Entities;
using FluentValidation;
using Moq;
using Xunit;

namespace Assessment.Tests;

public class UnitTests
{
    readonly Mock<IProductRepository> repo = new();

    ProductService Service() =>
        new(repo.Object, new ProductValidator(), new ItemValidator(), new PagingValidator());

    [Fact]
    public async Task Create_UsesActorAndUtcAudit()
    {
        repo.Setup(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((p, _) => p.Id = 7)
            .Returns(Task.CompletedTask);
        var result = await Service().CreateAsync(new(" Keyboard "), "admin", default);
        Assert.Equal(7, result.Id);
        Assert.Equal("Keyboard", result.ProductName);
        Assert.Equal("admin", result.CreatedBy);
        Assert.Equal(DateTimeKind.Utc, result.CreatedOn.Kind);
        repo.Verify(x => x.SaveAsync(default), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidName_DoesNotWrite(string name)
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => Service().CreateAsync(new(name), "admin", default)
        );
        repo.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingProduct_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => Service().GetAsync(42, default));

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 101)]
    [InlineData(-1, 20)]
    public async Task InvalidPagination_Rejected(int page, int size) =>
        await Assert.ThrowsAsync<ValidationException>(
            () => Service().ListAsync(new(page, size), default)
        );

    [Fact]
    public async Task NegativeQuantity_Rejected() =>
        await Assert.ThrowsAsync<ValidationException>(
            () => Service().AddItemAsync(1, new(-1), default)
        );

    [Fact]
    public async Task Update_SetsModifiedAudit()
    {
        var product = new Product
        {
            Id = 1,
            ProductName = "Old",
            CreatedBy = "creator",
        };
        repo.Setup(x => x.FindAsync(1, default, true)).ReturnsAsync(product);
        await Service().UpdateAsync(1, new("New"), "admin", default);
        Assert.Equal("New", product.ProductName);
        Assert.Equal("creator", product.CreatedBy);
        Assert.Equal("admin", product.ModifiedBy);
        Assert.NotNull(product.ModifiedOn);
    }
}
