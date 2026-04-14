namespace RentalApp.Database.Models;

public static class RoleConstants
{
    public const string Admin = "Admin";
    public const string OrdinaryUser = "OrdinaryUser";
    public const string SpecialUser = "SpecialUser";
    
    public static readonly string[] AllRoles = { Admin, OrdinaryUser, SpecialUser };
}