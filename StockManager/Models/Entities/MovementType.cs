namespace StockManager.Models.Entities;

public enum MovementType
{
    /// <summary>Stock receipt (goods arrive at the warehouse).</summary>
    Incoming = 1,

    /// <summary>Write-off / disposal / dispatch (goods leave the warehouse).</summary>
    Outgoing = 2
}
