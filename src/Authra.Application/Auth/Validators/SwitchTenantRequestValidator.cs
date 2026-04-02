using Authra.Application.Auth.DTOs;
using FluentValidation;

namespace Authra.Application.Auth.Validators;

public class SwitchTenantRequestValidator : AbstractValidator<SwitchTenantRequest>
{
    public SwitchTenantRequestValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");
    }
}
