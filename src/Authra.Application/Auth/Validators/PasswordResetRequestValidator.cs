using Authra.Application.Auth.DTOs;
using FluentValidation;

namespace Authra.Application.Auth.Validators;

public class PasswordResetRequestValidator : AbstractValidator<PasswordResetRequestDto>
{
    public PasswordResetRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");
    }
}
