using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Entities;
using InvoicesProjectEntities.Interfaces;

namespace InvoicesProjectApplication.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public UserService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    public async Task<UserDto?> GetByIdAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user is null ? null : MapToDto(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserDto dto)
    {
        if (await _userRepository.EmailExistsAsync(dto.Email))
        {
            throw new InvalidOperationException("Email já está em uso.");
        }

        var user = new User
        {
            Name = dto.Name,
            Email = dto.Email.ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(dto.Password)
        };

        await _userRepository.AddAsync(user);
        return MapToDto(user);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserDto dto)
    {
        var user = await _userRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");

        if (dto.Name is not null)
            user.Name = dto.Name;

        if (dto.Email is not null)
        {
            var emailLower = dto.Email.ToLowerInvariant();
            if (emailLower != user.Email && await _userRepository.EmailExistsAsync(emailLower))
            {
                throw new InvalidOperationException("Email já está em uso.");
            }
            user.Email = emailLower;
        }

        if (dto.NewPassword is not null)
        {
            if (dto.CurrentPassword is null || !_passwordHasher.Verify(dto.CurrentPassword, user.PasswordHash))
                throw new UnauthorizedAccessException("Senha atual incorreta.");
            user.PasswordHash = _passwordHasher.Hash(dto.NewPassword);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
        return MapToDto(user);
    }

    public async Task DeleteAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
    }

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _userRepository.GetByEmailAsync(dto.Email.ToLowerInvariant())
            ?? throw new UnauthorizedAccessException("Credenciais inválidas.");

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Usuário inativo.");
        }

        if (!_passwordHasher.Verify(dto.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Credenciais inválidas.");
        }

        var token = _jwtService.GenerateToken(user);

        return new LoginResponseDto(user.Id, user.Name, user.Email, user.IsAdmin, token);
    }

    private static UserDto MapToDto(User user) =>
        new(user.Id, user.Name, user.Email, user.IsActive, user.IsAdmin, user.CreatedAt);
}
