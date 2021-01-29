using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
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
        
        public AutManagementController(UserManager<IdentityUser> userManager, 
            IOptionsMonitor<JwtConfig> optionsMonitor, TodoContext context, TokenValidationParameters parameters)
        {
            _userManager = userManager;
            _jwtConfig = optionsMonitor.CurrentValue;
            _tokenValidationParameters = parameters;
            _dbContext = context;
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
                        Errors = new List<string>() { "Invalid authentication request"}
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
                Expires = DateTime.UtcNow.AddSeconds(30),
                NotBefore = DateTime.UtcNow,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_jwtConfig.Secret)),
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
