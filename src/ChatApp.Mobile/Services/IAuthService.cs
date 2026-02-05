using System.Threading.Tasks;

namespace ChatApp.Mobile.Services
{
    public interface IAuthService
    {
        Task<string> GetTokenAsync();
    }
}
