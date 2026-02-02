using Authra.Domain.Exceptions;
using AwesomeAssertions;

namespace Authra.UnitTests.Domain;

public class ExceptionTests
{
    [Fact]
    public void NotFoundException_ShouldHave404StatusCode()
    {
        // Arrange & Act
        var exception = new NotFoundException("User", Guid.NewGuid());

        // Assert
        exception.StatusCode.Should().Be(404);
        exception.Message.Should().Contain("User");
        exception.Message.Should().Contain("was not found");
    }

    [Fact]
    public void ConflictException_ShouldHave409StatusCode()
    {
        // Arrange & Act
        var exception = new ConflictException("Email already exists");

        // Assert
        exception.StatusCode.Should().Be(409);
        exception.Message.Should().Be("Email already exists");
    }

    [Fact]
    public void ForbiddenException_ShouldHave403StatusCode()
    {
        // Arrange & Act
        var exception = ForbiddenException.ForPermission("roles:create");

        // Assert
        exception.StatusCode.Should().Be(403);
        exception.RequiredPermission.Should().Be("roles:create");
        exception.Message.Should().Contain("roles:create");
    }

    [Fact]
    public void UnauthorizedException_ShouldHave401StatusCode()
    {
        // Arrange & Act
        var exception = new UnauthorizedException();

        // Assert
        exception.StatusCode.Should().Be(401);
        exception.Message.Should().Be("Invalid credentials");
    }

    [Fact]
    public void ValidationException_ShouldHave400StatusCode()
    {
        // Arrange & Act
        var exception = new ValidationException("email", "Invalid email format");

        // Assert
        exception.StatusCode.Should().Be(400);
        exception.Errors.Should().ContainKey("email");
        exception.Errors["email"].Should().Contain("Invalid email format");
    }
}
