using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.V4.Pages.Internal.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Converters;
using TodoApp.Configuration;
using TodoApp.DB;
using TodoApp.DB.Models;
using TodoApp.DB.Models.DTO.Requests;
using TodoApp.DB.Models.DTO.Responses;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace TodoApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AutManagementController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly JwtConfig _jwtConfig;
        private readonly TokenValidationParameters _tokenValidationParameters;
        private readonly TodoContext _dbContext;
        private readonly ILogger<AutManagementController> _logger;

        public AutManagementController(UserManager<IdentityUser> userManager,
            IOptionsMonitor<JwtConfig> optionsMonitor, TodoContext context, TokenValidationParameters parameters,
            ILogger<AutManagementController> logger)
        {
            _userManager = userManager;
            _jwtConfig = optionsMonitor.CurrentValue;
            _tokenValidationParameters = parameters;
            _dbContext = context;
            _logger = logger;
        }

        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] UserRegistrationRequestDto user)
        {
            if (ModelState.IsValid)
            {
                var existingUser = await _userManager.FindByEmailAsync(user.Email);

                if (existingUser != null)
                {
                    return BadRequest(new RegistrationResponse
                    {
                        Success = false,
                        Errors = new List<string>() {"Email already exist"}
                    });
                }

                var newUser = new IdentityUser(user.Email) {Email = user.Email};
                var isCreated = await _userManager.CreateAsync(newUser, user.Password);

                if (isCreated.Succeeded)
                {
                    return Ok(await GenerateJwtAsync(newUser));
                }

                return new JsonResult(new RegistrationResponse()
                    {
                        Success = false,
                        Errors = isCreated.Errors.Select(x => x.Description).ToList()
                    })
                    {StatusCode = 500};
            }

            return BadRequest(new RegistrationResponse()
            {
                Success = false,
                Errors = new List<string>() {"Invalid payload"}
            });
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] UserLoginRequest user)
        {
            if (ModelState.IsValid)
            {
                var existingUser = await _userManager.FindByEmailAsync(user.Email);

                if (existingUser == null)
                {
                    return BadRequest(new RegistrationResponse()
                    {
                        Success = false,
                        Errors = new List<string>() {"Invalid authentication request"}
                    });
                }

                var isCorrect = await _userManager.CheckPasswordAsync(existingUser, user.Password);

                if (isCorrect)
                {
                    return Ok(await GenerateJwtAsync(existingUser));
                }
                else
                {
                    return BadRequest(new RegistrationResponse()
                    {
                        Success = false,
                        Errors = new List<string>() {"Invalid authentication request"}
                    });
                }
            }

            return BadRequest(new RegistrationResponse()
            {
                Success = false,
                Errors = new List<string>() {"Invalid payload"}
            });
        }

        [HttpPost("RefreshToken")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenRequest tokenRequest)
        {
            if (ModelState.IsValid)
            {
                var refresh = await VerifyToken(tokenRequest);

                if (refresh == null)
                {
                    return BadRequest(new RegistrationResponse()
                    {
                        Success = false,
                        Errors = new List<string>() {"Invalid Jwt"}
                    });
                }

                return Ok(refresh);
            }

            return BadRequest(new RegistrationResponse()
            {
                Success = false,
                Errors = new List<string>() {"Invalid payload"}
            });
        }

        private async Task<AuthResult> VerifyToken(TokenRequest tokenRequest)
        {
            var jwtHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_jwtConfig.Secret)),
                ValidateIssuer = true,
                ValidIssuer = _jwtConfig.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtConfig.Audience,
                RequireExpirationTime = true,
                ValidateLifetime = false,
                ClockSkew = TimeSpan.Zero
            };
            
            try
            {
                var tokenInVerification = jwtHandler.ValidateToken(tokenRequest.Token, validationParameters, out var validatedToken);
                
                if (validatedToken is JwtSecurityToken jwtSecurityToken)
                {
                    var correctAlg = jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha512,
                        StringComparison.InvariantCultureIgnoreCase);

                    if (!correctAlg)
                    {
                        return null;
                    }
                }

                var utcExpiryDate =
                    long.Parse(tokenInVerification.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Exp)?.Value ??
                               string.Empty);

                if (utcExpiryDate <= 0)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Errors = new List<string>() { "Invalid Jwt" }
                    };
                }
                
                var expiryDate = UnixTimeStampToDateTime(utcExpiryDate);

                if (expiryDate > DateTime.UtcNow)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Errors = new List<string>() {"Jwt not expired"}
                    };
                }

                var storedRefreshToken =
                    await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == tokenRequest.RefreshToken);

                if (storedRefreshToken == null)
                {
                    return new AuthResult()
                    {
                        Errors = new List<string>() { "refresh token doesnt exist" },
                        Success = false
                    };
                }

                if (DateTime.UtcNow > storedRefreshToken.ExpiryData)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Errors = new List<string>() { "Refreshtoken has expired, login required" }
                    };
                }

                if (storedRefreshToken.IsUsed)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Errors = new List<string>() { "Token already used" }
                    };
                }

                if (storedRefreshToken.IsRevoked)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Errors = new List<string>() { "Token has been revoked" }
                    };
                }

                var jti = tokenInVerification.Claims.SingleOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti).Value;

                if (storedRefreshToken.JwtId != jti)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Errors = new List<string>() { "Token does not match" },
                    };
                }

                storedRefreshToken.IsUsed = true;
                _dbContext.Entry(storedRefreshToken).State = EntityState.Modified;
                await _dbContext.SaveChangesAsync();

                var dbUser = await _userManager.FindByIdAsync(storedRefreshToken.UserId);
                return await GenerateJwtAsync(dbUser);

            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                throw;
            }
        }

        private DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            var dateTimeVal = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTimeVal = dateTimeVal.AddSeconds(unixTimeStamp).ToUniversalTime();
            return dateTimeVal;
        }

        private async Task<AuthResult> GenerateJwtAsync(IdentityUser user)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("Id", user.Id),
                    new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                    new Claim(JwtRegisteredClaimNames.Email, user.Email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                }),
                Issuer = _jwtConfig.Issuer,
                Audience = _jwtConfig.Audience,
                Expires = DateTime.UtcNow.AddMinutes(5),
                NotBefore = DateTime.UtcNow,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_jwtConfig.Secret)),
                    SecurityAlgorithms.HmacSha512Signature)
            };

            var token = jwtTokenHandler.CreateToken(tokenDescriptor);
            var jwt = jwtTokenHandler.WriteToken(token);

            var refreshToken = new RefreshToken()
            {
                JwtId = token.Id,
                UserId = user.Id,
                AddedDate = DateTime.UtcNow,
                ExpiryData = DateTime.UtcNow.AddYears(1),
                Token = RandomString(25) + Guid.NewGuid()
            };

            await _dbContext.RefreshTokens.AddAsync(refreshToken);
            await _dbContext.SaveChangesAsync();

            return new AuthResult()
            {
                Token = jwt,
                Success = true,
                RefreshToken = refreshToken.Token
            };
        }

        private string RandomString(int length)
        {
            var rdm = new Random();
            var rdmChars = new char[length];
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            for (int i = 0; i < length; i++)
            {
                rdmChars[i] = chars[rdm.Next(chars.Length)];
            }

            return new string(rdmChars);
        }
    }
}