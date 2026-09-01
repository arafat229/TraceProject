using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace TraceProject.Helpers;

public static class Utilities
{
    // Builds a signed JWT containing whatever claims you pass in (identity,
    // name, email, role, custom permissions — see AccountController.Login).
    // "Signed" means: anyone can READ the token's contents (JWTs are just
    // base64, not encrypted), but nobody can FORGE or MODIFY one without
    // knowing the secret key from appsettings.json — because the signature
    // at the end would no longer match.
    public static string GenerateJwtToken(IConfiguration config, List<Claim> claims, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
