namespace WarehouseApi.Common;

/// <summary>
/// Role names used throughout the app. Kept as constants instead of magic strings
/// so a typo becomes a compile-time-visible error instead of a silent authz bypass.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Viewer = "Viewer";

    public static readonly string[] All = { Admin, Manager, Viewer };
}

/// <summary>
/// Authorization policy names. Policies are additive over roles so the mapping
/// between "who can do this" and "which roles satisfy it" lives in one place (Program.cs).
/// </summary>
public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string ManagerOrAdmin = "ManagerOrAdmin";
    public const string ViewerOrAbove = "ViewerOrAbove";
}
