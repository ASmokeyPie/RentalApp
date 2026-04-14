using Microsoft.EntityFrameworkCore;
using RentalApp.Database.Data;

Console.WriteLine("Running migrations...");
using var context = new AppDbContext();
context.Database.Migrate();
Console.WriteLine("Migrations complete.");
