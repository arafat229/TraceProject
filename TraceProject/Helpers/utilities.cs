using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace TraceProject.Helpers
{
    public class Utilities
    {
        public static string GenerateJwtToken(IConfiguration config, IList<Claim> claims,DateTime expiry)
        {

            var secret = config["AppSettings:Token"];
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes($"{secret}-{secret}"));
            var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiry,
                SigningCredentials = cred,
                Issuer = config["AppSettings:Issuer"],
                IssuedAt = DateTime.UtcNow,
                Audience = config["AppSettings:Audience"]
            };

            var tokenHanlder = new JwtSecurityTokenHandler();
            var token = tokenHanlder.CreateToken(tokenDescriptor);
            return tokenHanlder.WriteToken(token);




        }
    }
}
