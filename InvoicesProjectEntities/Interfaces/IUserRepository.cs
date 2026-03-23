using InvoicesProjectEntities.Entities;

namespace InvoicesProjectEntities.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<bool> EmailExistsAsync(string email);
}
