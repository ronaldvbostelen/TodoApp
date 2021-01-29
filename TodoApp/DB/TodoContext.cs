using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TodoApp.DB.Models;

namespace TodoApp.DB
{
    public class TodoContext : IdentityDbContext
    {
        public DbSet<ItemData> Items { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public TodoContext()
        {
            
        }

        public TodoContext(DbContextOptions<TodoContext> options) : base(options)
        {
            
        }
    }
}