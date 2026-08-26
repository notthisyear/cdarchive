using CdArchiveBackend.Data.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CdArchiveBackend.Services
{
    public class JwtService
    {
        private readonly IConfigurationSection _jwtSection;
        private readonly string _apiSigningKey;
        private readonly int _accessTokenExpiryMinutes;

        private const string JwtSectionName = "Jwt";
        private const string JwtSectionIssuerName = "Issuer";
        private const string JwtSectionAudienceName = "Audience";
        private const string JwtSectionExpiryTimeMinutesName = "TokenLifetimeMinutes";
        private const string ApiSigningKeyName = "API:SigningKey";

        public JwtService(IConfiguration configuration)
        {
            (_jwtSection, _apiSigningKey) = GetFromConfiguration(configuration);
            _accessTokenExpiryMinutes = int.Parse(_jwtSection[JwtSectionExpiryTimeMinutesName] ?? string.Empty, NumberStyles.Integer);
        }

        public string CreateToken(UserData userData)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userData.Id.ToString()),
                new Claim(ClaimTypes.Name, userData.Username)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_apiSigningKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSection[JwtSectionIssuerName],
                audience: _jwtSection[JwtSectionAudienceName],
                expires: DateTime.UtcNow.AddMinutes(_accessTokenExpiryMinutes),
                claims: claims,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static TokenValidationParameters GetTokenValidationParameters(IConfiguration configuration)
        {
            var (jwt, apiSigningKey) = GetFromConfiguration(configuration);
            return new()
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwt["Issuer"],
                ValidAudience = jwt["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(apiSigningKey))
            };
        }

        private static (IConfigurationSection jwtSection, string apiSigningKey) GetFromConfiguration(IConfiguration configuration)
            => (
                configuration.GetSection(JwtSectionName),
                configuration[ApiSigningKeyName] ?? throw new ArgumentException("Could not get API signing key")
            );
    }
}
