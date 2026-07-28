using Microsoft.EntityFrameworkCore;
using MyFantasy.Api.Domain;

namespace MyFantasy.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<League> Leagues => Set<League>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<PriceSnapshot> PriceSnapshots => Set<PriceSnapshot>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<AuthState> AuthStates => Set<AuthState>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<League>(e =>
        {
            e.HasIndex(x => x.ExternalId).IsUnique();
            e.Property(x => x.ExternalId).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200);
        });

        b.Entity<Player>(e =>
        {
            e.HasIndex(x => x.ExternalId).IsUnique();
            e.Property(x => x.ExternalId).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Team).HasMaxLength(120);
            e.Property(x => x.Position).HasConversion<int>();
        });

        b.Entity<PriceSnapshot>(e =>
        {
            // PK compuesta para UPSERT diario y evitar duplicados por (jugador, día).
            e.HasKey(x => new { x.PlayerId, x.Date });
            e.HasOne(x => x.Player)
                .WithMany(p => p.PriceSnapshots)
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Holding>(e =>
        {
            e.Property(x => x.Status).HasConversion<int>();
            e.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId);
            e.HasOne(x => x.League).WithMany(l => l.Holdings).HasForeignKey(x => x.LeagueId);
            // Un jugador solo puede estar activo una vez por liga.
            e.HasIndex(x => new { x.LeagueId, x.PlayerId, x.Status });
        });

        b.Entity<Sale>(e =>
        {
            e.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId);
            e.HasOne(x => x.League).WithMany(l => l.Sales).HasForeignKey(x => x.LeagueId);
            e.HasIndex(x => new { x.LeagueId, x.SaleDate });
        });

        b.Entity<AuthState>(e =>
        {
            // Fila única con Id fijo = 1 (no autoincremental).
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.RefreshTokenEnc).HasColumnType("longtext");
        });
    }
}
