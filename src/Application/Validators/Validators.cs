using Application.DTOs;
using FluentValidation;

namespace Application.Validators;

public class ProductValidator : AbstractValidator<ProductWrite>
{
    public ProductValidator()
    {
        RuleFor(x => x.ProductName)
            .NotEmpty()
            .MaximumLength(255)
            .Must(x => !string.IsNullOrWhiteSpace(x));
    }
}

public class ItemValidator : AbstractValidator<ItemWrite>
{
    public ItemValidator()
    {
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
    }
}

public class PagingValidator : AbstractValidator<Paging>
{
    public PagingValidator()
    {
        RuleFor(x => x.PageNumber).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
    }
}

public class RefreshValidator : AbstractValidator<RefreshRequest>
{
    public RefreshValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(128);
    }
}
