using InvoicesProjectEntities.Entities;

namespace InvoicesProjectApplication.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
}
