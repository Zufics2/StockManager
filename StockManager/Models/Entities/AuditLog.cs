namespace StockManager.Models.Entities;

/// <summary>Operations journal entry.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }

    /// <summary>Create, Update, Delete, Register, Login, AssignRole ...</summary>
    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }

    /// <summary>JSON: new values (create), old values (delete) or {old,new} pairs (update).</summary>
    public string? Changes { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}
