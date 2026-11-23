using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBuilder.Web.Security;

internal static class SeedOwnerCommand
{
    public static bool Matches(string[] args) => args.Length > 0 && args[0] == "seed-owner";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        var email = Option(args, "--email");
        var tenantName = Option(args, "--tenant");
        var password = Option(args, "--password") ?? Environment.GetEnvironmentVariable("SEED_OWNER_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(tenantName) || string.IsNullOrWhiteSpace(password))
        {
            await Console.Error.WriteLineAsync("Usage: seed-owner --email <email> --tenant <name> --password <password> (or set SEED_OWNER_PASSWORD)");
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var time = sp.GetRequiredService<TimeProvider>();
        var users = sp.GetRequiredService<UserManager<AppUser>>();
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.MigrateAsync();
        if (await users.FindByEmailAsync(email) is not null)
        {
            await Console.Error.WriteLineAsync("A user with that email already exists.");
            return 1;
        }

        var tenant = Tenant.Create(tenantName, time.GetUtcNow());
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        if (!await roles.RoleExistsAsync(Roles.Owner))
        {
            await roles.CreateAsync(new IdentityRole(Roles.Owner));
        }

        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, TenantId = tenant.Id, DisplayName = email };
        var created = await users.CreateAsync(user, password);
        if (created.Succeeded)
        {
            created = await users.AddToRoleAsync(user, Roles.Owner);
        }

        if (!created.Succeeded)
        {
            await Console.Error.WriteLineAsync(string.Join("; ", created.Errors.Select(e => e.Description)));
            return 1;
        }

        Console.WriteLine("Owner created. Sign in at /account/sign-in; you will be asked to set up two-factor sign-in.");
        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
