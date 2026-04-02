using Authra.Application.Auth.DTOs;
using FluentValidation;

namespace Authra.Application.Auth.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .MaximumLength(128).WithMessage("Password must not exceed 128 characters")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit");

        RuleFor(x => x.Username)
            .MinimumLength(3).When(x => !string.IsNullOrEmpty(x.Username))
            .WithMessage("Username must be at least 3 characters")
            .MaximumLength(50).When(x => !string.IsNullOrEmpty(x.Username))
            .WithMessage("Username must not exceed 50 characters")
            .Matches(@"^[a-zA-Z0-9_-]+$").When(x => !string.IsNullOrEmpty(x.Username))
            .WithMessage("Username can only contain letters, numbers, underscores, and hyphens");
    }
}
