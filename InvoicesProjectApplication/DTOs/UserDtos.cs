namespace InvoicesProjectApplication.DTOs;

public record UserDto(
    Guid Id,
    string Name,
    string Email,
    bool IsActive,
    bool IsAdmin,
    DateTime CreatedAt
);

public record CreateUserDto(
    string Name,
    string Email,
    string Password
);

public record UpdateUserDto(
    string? Name,
    string? Email,
    string? CurrentPassword,
    string? NewPassword
);

public record LoginDto(
    string Email,
    string Password
);

public record LoginResponseDto(
    Guid UserId,
    string Name,
    string Email,
    bool IsAdmin,
    string Token
);
