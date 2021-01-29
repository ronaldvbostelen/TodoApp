using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;

namespace TodoApp.DB.Models
{
    public class RefreshToken : BaseDbModel
    {
        public string UserId { get; set; }
        public string Token { get; set; }
        public string JwtId { get; set; }
        public bool IsUsed { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime AddedDate { get; set; }
        public DateTime ExpiryData { get; set; }
        
        [ForeignKey(nameof(UserId))] 
        public IdentityUser User { get; set; }
    }
}