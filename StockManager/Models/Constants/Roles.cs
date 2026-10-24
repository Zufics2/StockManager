namespace StockManager.Models.Constants;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Storekeeper = "Storekeeper";
    public const string Viewer = "Viewer";

    /// <summary>Full catalog management (create / update).</summary>
    public const string AdminOrManager = Admin + "," + Manager;

    /// <summary>Everyone who may register stock movements.</summary>
    public const string StockOperators = Admin + "," + Manager + "," + Storekeeper;

    public static readonly string[] All = [Admin, Manager, Storekeeper, Viewer];
}
