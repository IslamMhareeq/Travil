using TRAVIL.Models;

namespace TRAVEL.Services
{
    public interface IAuthenticationService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task<LoginResponse> RegisterAsync(RegisterRequest request);
        Task<bool> ValidateTokenAsync(string token);
        Task LogoutAsync(int userId);
        string GenerateToken(User user);
        string HashPassword(string password);
        bool VerifyPassword(string password, string hash);
    }
}
