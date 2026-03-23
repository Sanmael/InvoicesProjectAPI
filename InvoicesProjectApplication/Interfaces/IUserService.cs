using InvoicesProjectApplication.DTOs;

namespace InvoicesProjectApplication.Interfaces;

public interface IUserService
{
    Task<UserDto?> GetByIdAsync(Guid id);
    Task<UserDto> CreateAsync(CreateUserDto dto);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserDto dto);
    Task DeleteAsync(Guid id);
    Task<LoginResponseDto> LoginAsync(LoginDto dto);
}
