using System.ComponentModel.DataAnnotations;

namespace TodoApp.DB.Models.DTO.Requests
{
    public class TokenRequest
    {
        [Required] public string Token { get; set; }
        [Required] public string RefeshToken { get; set; }
    }
}