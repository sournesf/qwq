using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

var builder = WebApplication.CreateBuilder(args);

// MVC и Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Подключение к базе данных
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DotaConnection")));

// Настройка авторизации и ролей
builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

var app = builder.Build();

// Логирование ошибок всех HTTP-запросов
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.WriteLine("========== ОШИБКА ЗАПРОСА ==========");
        Console.WriteLine($"Путь: {context.Request.Path}");
        Console.WriteLine($"Метод: {context.Request.Method}");
        Console.WriteLine($"Тип ошибки: {ex.GetType().FullName}");
        Console.WriteLine($"Сообщение: {ex.Message}");
        Console.WriteLine($"Время: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine("Подробности:");
        Console.WriteLine(ex);
        Console.WriteLine("====================================");

        throw;
    }
});


// Обработка ошибок в Production
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Создание ролей и назначение роли администратора
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var roleManager =
        services.GetRequiredService<RoleManager<IdentityRole>>();

    var userManager =
        services.GetRequiredService<UserManager<ApplicationUser>>();

    string[] roles = { "Admin", "Captain" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var result = await roleManager.CreateAsync(
                new IdentityRole(role));

            if (!result.Succeeded)
            {
                throw new Exception(
                    $"Ошибка создания роли {role}: " +
                    string.Join("; ",
                        result.Errors.Select(e => e.Description)));
            }
        }
    }

    string adminEmail = "rsaitov953@gmail.com";

    var adminUser =
        await userManager.FindByEmailAsync(adminEmail);

    if (adminUser != null &&
        !await userManager.IsInRoleAsync(adminUser, "Admin"))
    {
        var result = await userManager.AddToRoleAsync(
            adminUser, "Admin");

        if (!result.Succeeded)
        {
            throw new Exception(
                "Ошибка назначения роли Admin: " +
                string.Join("; ",
                    result.Errors.Select(e => e.Description)));
        }
    }
}

// Маршруты
app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();