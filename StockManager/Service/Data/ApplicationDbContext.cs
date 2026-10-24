using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using StockManager.Abstract;
using StockManager.Models.Entities;

namespace StockManager.Service.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUser)
    : IdentityDbContext<ApplicationUser>(options)
{
    private static readonly JsonSerializerOptions AuditJson = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e => e.Property(u => u.FullName).HasMaxLength(150));

        builder.Entity<Category>(e =>
        {
            e.Property(c => c.Name).IsRequired().HasMaxLength(100);
            e.Property(c => c.Description).HasMaxLength(500);
            e.HasIndex(c => c.Name).IsUnique();
        });

        builder.Entity<Supplier>(e =>
        {
            e.Property(s => s.Name).IsRequired().HasMaxLength(150);
            e.Property(s => s.ContactEmail).HasMaxLength(256);
            e.Property(s => s.Phone).HasMaxLength(30);
            e.Property(s => s.Address).HasMaxLength(300);
            e.HasIndex(s => s.Name);
        });

        builder.Entity<Warehouse>(e =>
        {
            e.Property(w => w.Name).IsRequired().HasMaxLength(100);
            e.Property(w => w.Location).HasMaxLength(300);
            e.HasIndex(w => w.Name).IsUnique();
        });

        builder.Entity<Product>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.Sku).IsRequired().HasMaxLength(50);
            e.Property(p => p.Description).HasMaxLength(2000);
            e.Property(p => p.ImagePath).HasMaxLength(300);
            e.Property(p => p.Price).HasPrecision(18, 2);

            e.HasIndex(p => p.Sku).IsUnique();
            e.HasIndex(p => p.Name);
            e.HasIndex(p => p.Price);

            e.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(p => p.Supplier)
                .WithMany(s => s.Products)
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StockLevel>(e =>
        {
            e.HasKey(l => new { l.ProductId, l.WarehouseId });
            e.Property(l => l.ConcurrencyStamp).IsConcurrencyToken();

            e.HasOne(l => l.Product)
                .WithMany(p => p.StockLevels)
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(l => l.Warehouse)
                .WithMany(w => w.StockLevels)
                .HasForeignKey(l => l.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            // Last line of defence: the balance can never become negative, even under a race.
            e.ToTable(t => t.HasCheckConstraint("CK_StockLevels_Quantity_NonNegative", "\"Quantity\" >= 0"));
        });

        builder.Entity<StockMovement>(e =>
        {
            e.Property(m => m.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.UnitCost).HasPrecision(18, 2);
            e.Property(m => m.Reason).HasMaxLength(500);
            e.Property(m => m.CreatedByUserId).HasMaxLength(450);
            e.Property(m => m.CreatedByUserName).HasMaxLength(256);

            e.HasIndex(m => new { m.ProductId, m.WarehouseId, m.CreatedAt });
            e.HasIndex(m => m.CreatedAt);

            e.HasOne(m => m.Product)
                .WithMany(p => p.StockMovements)
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.Warehouse)
                .WithMany(w => w.StockMovements)
                .HasForeignKey(m => m.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.Supplier)
                .WithMany(s => s.StockMovements)
                .HasForeignKey(m => m.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.Property(a => a.UserId).HasMaxLength(450);
            e.Property(a => a.UserName).HasMaxLength(256);
            e.Property(a => a.Action).IsRequired().HasMaxLength(50);
            e.Property(a => a.EntityName).IsRequired().HasMaxLength(100);
            e.Property(a => a.EntityId).HasMaxLength(100);
            e.Property(a => a.IpAddress).HasMaxLength(64);
            e.Property(a => a.Changes).HasColumnType("jsonb");

            e.HasIndex(a => a.Timestamp);
            e.HasIndex(a => new { a.EntityName, a.EntityId });
        });
    }

    // ------------------------------------------------------------------
    // Automatic audit trail for every entity implementing IAuditable.
    // Phase 1 saves the data (so generated keys are known), phase 2 saves the journal rows.
    // ------------------------------------------------------------------
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var pending = CollectAuditEntries();

        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        if (pending.Count == 0)
            return result;

        var now = DateTime.UtcNow;
        foreach (var item in pending)
        {
            AuditLogs.Add(new AuditLog
            {
                UserId = currentUser.UserId,
                UserName = currentUser.UserName,
                IpAddress = currentUser.IpAddress,
                Action = item.Action,
                EntityName = item.EntityName,
                EntityId = item.EntityId ?? GetKey(item.Entry),
                Changes = item.Changes,
                Timestamp = now
            });
        }

        await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        return result;
    }

    private List<PendingAudit> CollectAuditEntries()
    {
        ChangeTracker.DetectChanges();
        var list = new List<PendingAudit>();

        foreach (var entry in ChangeTracker.Entries<IAuditable>().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var data = new Dictionary<string, object?>();
            foreach (var prop in entry.Properties)
            {
                var name = prop.Metadata.Name;
                switch (entry.State)
                {
                    case EntityState.Added when !prop.Metadata.IsPrimaryKey():
                        data[name] = prop.CurrentValue;
                        break;
                    case EntityState.Deleted:
                        data[name] = prop.OriginalValue;
                        break;
                    case EntityState.Modified when prop.IsModified:
                        data[name] = new Dictionary<string, object?>
                        {
                            ["old"] = prop.OriginalValue,
                            ["new"] = prop.CurrentValue
                        };
                        break;
                }
            }

            if (data.Count == 0)
                continue;

            list.Add(new PendingAudit
            {
                Entry = entry,
                Action = entry.State switch
                {
                    EntityState.Added => "Create",
                    EntityState.Modified => "Update",
                    _ => "Delete"
                },
                EntityName = entry.Metadata.ClrType.Name,
                EntityId = entry.State == EntityState.Added ? null : GetKey(entry),
                Changes = JsonSerializer.Serialize(data, AuditJson)
            });
        }

        return list;
    }

    private static string? GetKey(EntityEntry entry) =>
        entry.Metadata.FindPrimaryKey() is { } key
            ? string.Join(",", key.Properties.Select(p => entry.Property(p.Name).CurrentValue))
            : null;

    private sealed class PendingAudit
    {
        public required EntityEntry Entry { get; init; }
        public required string Action { get; init; }
        public required string EntityName { get; init; }
        public string? EntityId { get; init; }
        public string? Changes { get; init; }
    }
}
