using ChatApp.Core.Entities;

namespace ChatApp.Core.Interfaces;

public interface IAuthService
{
    Task<(User? User, string Token)> LoginAsync(string email, string password);
    Task<(User? User, string ErrorMessage)> RegisterAsync(string username, string email, string password);
}
