using Rubato.Data.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Rubato.Data;

public class RubatoDataContext(DbContextOptions<RubatoDataContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Entry> Entries { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
}