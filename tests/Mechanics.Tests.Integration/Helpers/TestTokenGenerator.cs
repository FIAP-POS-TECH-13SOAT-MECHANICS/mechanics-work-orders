using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Mechanics.Tests.Integration.Helpers;

public class TestTokenGenerator(RSA rsa)
{
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public string GenerateAccessTokenByRoleName(string roleName)
    {
        var userId = roleName switch
        {
            "Administrator" => new Guid("11111111-1111-1111-1111-111111111111"),
            "Attendant" => new Guid("22222222-2222-2222-2222-222222222222"),
            "Mechanic" => new Guid("33333333-3333-3333-3333-333333333333"),
            "CustomerAdmin" => new Guid("44444444-4444-4444-4444-444444444444"),
            "CustomerUser" => new Guid("55555555-5555-5555-5555-555555555555"),
            "Service" => new Guid("66666666-6666-6666-6666-666666666666"),
            _ => Guid.NewGuid(),
        };

        var customerId = roleName.StartsWith("Customer", StringComparison.Ordinal)
            ? new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
            : Guid.Empty;

        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("customerId", customerId.ToString()),
            new("role", roleName),
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = "fiap-mechanics",
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256),
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow,
        };

        return _tokenHandler.CreateToken(tokenDescriptor);
    }
}
