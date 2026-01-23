using ChatApp.Core.Entities;

namespace ChatApp.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(int id);
    Task<IEnumerable<User>> GetAllAsync();
    Task<bool> ExistsAsync(string email);
    Task<User> CreateAsync(User user);
}
