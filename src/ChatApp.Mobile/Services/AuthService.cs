using System.Threading.Tasks;

namespace ChatApp.Mobile.Services
{
    public class AuthService : IAuthService
    {
        public static string BaseUrl = "http://localhost:5000";

        public Task<string> GetTokenAsync()
        {
            return Task.FromResult("dummy_token");
        }
    }
}
