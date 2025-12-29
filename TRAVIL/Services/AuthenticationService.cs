using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TRAVEL.Data;
using TRAVEL.Models;

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

    public class AuthenticationService : IAuthenticationService
    {
        private readonly TravelDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthenticationService> _logger;

        public AuthenticationService(
            TravelDbContext context,
            IConfiguration configuration,
            IEmailService emailService,
            ILogger<AuthenticationService> logger)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                // Validate request
                if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Email and password are required"
                    };
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

                if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Invalid email or password"
                    };
                }

                if (user.Status == UserStatus.Suspended || user.Status == UserStatus.Deleted)
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Your account is suspended or deleted"
                    };
                }

                // Update last login time
                user.LastLoginAt = DateTime.UtcNow;

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogWarning($"Could not update last login time: {dbEx.InnerException?.Message}");
                    // Continue - updating last login is not critical
                }

                var token = GenerateToken(user);
                var userDto = MapToUserDto(user);

                return new LoginResponse
                {
                    Success = true,
                    Message = "Login successful",
                    Token = token,
                    User = userDto
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for email: {Email}", request?.Email);
                return new LoginResponse
                {
                    Success = false,
                    Message = "An error occurred during login. Please try again."
                };
            }
        }

        public async Task<LoginResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                // Validate request
                if (request == null)
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Registration data is required"
                    };
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.FirstName))
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "First name is required"
                    };
                }

                if (string.IsNullOrWhiteSpace(request.LastName))
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Last name is required"
                    };
                }

                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Email is required"
                    };
                }

                if (string.IsNullOrWhiteSpace(request.Password))
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Password is required"
                    };
                }

                if (request.Password.Length < 6)
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Password must be at least 6 characters"
                    };
                }

                // Check if email already exists (case-insensitive)
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

                if (existingUser != null)
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Email already registered"
                    };
                }

                // Create new user
                var user = new User
                {
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = request.Email.Trim().ToLower(),
                    PasswordHash = HashPassword(request.Password),
                    PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty,
                    Address = request.Address?.Trim() ?? string.Empty,
                    City = request.City?.Trim() ?? string.Empty,
                    Country = request.Country?.Trim() ?? string.Empty,
                    PostalCode = request.PostalCode?.Trim() ?? string.Empty,
                    ProfileImageUrl = null,
                    Role = UserRole.User,
                    Status = UserStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = null,
                    LastLoginAt = null,
                    EmailVerified = false,
                    EmailVerifiedAt = null
                };

                _context.Users.Add(user);

                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("User registered successfully: {Email}", user.Email);
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "Database error during registration for email: {Email}", request.Email);

                    // Check for specific database errors
                    var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;

                    if (innerMessage.Contains("duplicate") || innerMessage.Contains("unique"))
                    {
                        return new LoginResponse
                        {
                            Success = false,
                            Message = "Email already registered"
                        };
                    }

                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Registration failed. Please try again."
                    };
                }

                // Send welcome email (don't fail registration if email fails)
                try
                {
                    await _emailService.SendWelcomeEmailAsync(user.Email, user.FirstName);
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, "Failed to send welcome email to: {Email}", user.Email);
                    // Continue - email failure shouldn't fail registration
                }

                var token = GenerateToken(user);
                var userDto = MapToUserDto(user);

                return new LoginResponse
                {
                    Success = true,
                    Message = "Registration successful",
                    Token = token,
                    User = userDto
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration error for email: {Email}", request?.Email);
                return new LoginResponse
                {
                    Success = false,
                    Message = "An error occurred during registration. Please try again."
                };
            }
        }

        public string GenerateToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JWT");
            var secretKeyString = jwtSettings["SecretKey"];

            if (string.IsNullOrEmpty(secretKeyString))
            {
                throw new InvalidOperationException("JWT SecretKey is not configured");
            }

            // Ensure minimum key length (at least 32 characters for HMAC-SHA256)
            if (secretKeyString.Length < 32)
            {
                throw new InvalidOperationException("JWT SecretKey must be at least 32 characters long");
            }

            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKeyString));
            var credentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("UserId", user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var expirationMinutes = 60;
            if (int.TryParse(jwtSettings["ExpirationMinutes"], out int parsedMinutes))
            {
                expirationMinutes = parsedMinutes;
            }

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be null or empty");
            }

            using (var rng = RandomNumberGenerator.Create())
            {
                var salt = new byte[16];
                rng.GetBytes(salt);

                using (var pbkdf2 = new Rfc2898DeriveBytes(
                    password,
                    salt,
                    10000,
                    HashAlgorithmName.SHA256))
                {
                    var hash = pbkdf2.GetBytes(20);
                    var hashWithSalt = new byte[36];

                    Buffer.BlockCopy(salt, 0, hashWithSalt, 0, 16);
                    Buffer.BlockCopy(hash, 0, hashWithSalt, 16, 20);

                    return Convert.ToBase64String(hashWithSalt);
                }
            }
        }

        public bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
            {
                return false;
            }

            try
            {
                var hashBytes = Convert.FromBase64String(hash);

                if (hashBytes.Length != 36)
                {
                    return false;
                }

                var salt = new byte[16];
                Buffer.BlockCopy(hashBytes, 0, salt, 0, 16);

                using (var pbkdf2 = new Rfc2898DeriveBytes(
                    password,
                    salt,
                    10000,
                    HashAlgorithmName.SHA256))
                {
                    var hash2 = pbkdf2.GetBytes(20);

                    // Use constant-time comparison to prevent timing attacks
                    var result = 0;
                    for (int i = 0; i < 20; i++)
                    {
                        result |= hashBytes[i + 16] ^ hash2[i];
                    }

                    return result == 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public Task<bool> ValidateTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return Task.FromResult(false);
            }

            try
            {
                var jwtSettings = _configuration.GetSection("JWT");
                var secretKeyString = jwtSettings["SecretKey"];

                if (string.IsNullOrEmpty(secretKeyString))
                {
                    return Task.FromResult(false);
                }

                var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKeyString));
                var tokenHandler = new JwtSecurityTokenHandler();

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = secretKey,
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Token validation failed");
                return Task.FromResult(false);
            }
        }

        public async Task LogoutAsync(int userId)
        {
            try
            {
                // You could implement token blacklisting here if needed
                _logger.LogInformation("User {UserId} logged out", userId);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout error for user: {UserId}", userId);
            }
        }

        private UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                UserId = user.UserId,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Role = user.Role,
                Status = user.Status,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                EmailVerified = user.EmailVerified
            };
        }
    }
}