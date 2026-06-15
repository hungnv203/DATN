using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher passwordHasher)
    {
        // 1. Seed Roles
        var defaultRoles = new[]
        {
            new Role { Name = "Admin", Description = "Administrator role" },
            new Role { Name = "Customer", Description = "Customer role" },
            new Role { Name = "Staff", Description = "Staff role" }
        };

        var dbRoles = await db.Roles.ToListAsync();

        foreach (var defaultRole in defaultRoles)
        {
            var exists = dbRoles.Any(r => r.Name.Equals(defaultRole.Name, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                db.Roles.Add(defaultRole);
            }
        }

        // Save role changes to DB so we can reference Admin role ID
        await db.SaveChangesAsync();

        // Re-fetch roles to ensure we have the IDs (both existing and newly added ones)
        dbRoles = await db.Roles.ToListAsync();
        var adminRole = dbRoles.FirstOrDefault(r => r.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase));

        if (adminRole == null)
        {
            throw new InvalidOperationException("Admin role could not be seeded or found.");
        }

        // 2. Seed Admin User
        var adminEmail = "admin@gmail.com";
        var adminUser = await db.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == adminEmail.ToLower());

        if (adminUser == null)
        {
            adminUser = new User
            {
                FullName = "admin",
                Email = adminEmail,
                PhoneNumber = "0123456789",
                PasswordHash = passwordHasher.Hash("admin123"),
                Status = "Active"
            };

            db.Users.Add(adminUser);
            await db.SaveChangesAsync();

            // 3. Map user to Admin role
            var userRole = new UserRole
            {
                UserId = adminUser.Id,
                RoleId = adminRole.Id
            };
            db.UserRoles.Add(userRole);
            await db.SaveChangesAsync();
        }

        // 4. Seed All Standard Permissions
        var controllers = new[]
        {
            "BookingPromotions", "Bookings", "Cinemas", "Genres", "LoyaltyPoints",
            "MovieGenres", "Movies", "Notifications", "PaymentLogs", "Payments",
            "Permissions", "PointTransactions", "Promotions", "RolePermissions",
            "Roles", "Rooms", "SeatHolds", "Seats", "Showtimes", "Tickets",
            "UserRoles", "Users"
        };
        var actions = new[] { "Create", "Update", "Delete" };

        var dbPermissions = await db.Permissions.ToListAsync();
        var permissionsToAdd = new List<Permission>();

        foreach (var controller in controllers)
        {
            foreach (var action in actions)
            {
                var permissionName = $"Permissions.{controller}.{action}";
                if (!dbPermissions.Any(p => p.Name == permissionName))
                {
                    permissionsToAdd.Add(new Permission
                    {
                        Name = permissionName,
                        Description = $"Auto-generated permission for {action} on {controller}"
                    });
                }
            }
        }

        if (permissionsToAdd.Any())
        {
            db.Permissions.AddRange(permissionsToAdd);
            await db.SaveChangesAsync();
        }
    }
}
