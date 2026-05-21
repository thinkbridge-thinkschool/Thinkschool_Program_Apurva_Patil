using Microsoft.EntityFrameworkCore;
using QuotesJwt.Models;

namespace QuotesJwt.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
}
