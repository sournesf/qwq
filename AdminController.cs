using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserRolesController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;
        private readonly ILogger<UserRolesController> _logger;

        public UserRolesController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext context,
            ILogger<UserRolesController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
        }

        // Список пользователей и их ролей
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users
                .OrderBy(u => u.UserName)
                .ToListAsync();

            var result = new List<UserRoleViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                result.Add(new UserRoleViewModel
                {
                    UserId = user.Id,
                    UserName = user.UserName ?? "Без имени",
                    Email = user.Email ?? "",
                    IsCaptain = roles.Contains("Captain"),
                    IsAdmin = roles.Contains("Admin")
                });
            }

            return View(result);
        }

        // Назначение роли капитана
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeCaptain(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest();

            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                TempData["Error"] = "Пользователь не найден.";
                return RedirectToAction(nameof(Index));
            }

            // Проверяем, существует ли роль Captain
            if (!await _roleManager.RoleExistsAsync("Captain"))
            {
                TempData["Error"] =
                    "Роль Captain не существует. Сначала создай эту роль.";

                return RedirectToAction(nameof(Index));
            }

            // Администраторов не изменяем
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["Error"] =
                    "Нельзя назначить роль капитана пользователю с ролью администратора.";

                return RedirectToAction(nameof(Index));
            }

            // Не добавляем роль повторно
            if (await _userManager.IsInRoleAsync(user, "Captain"))
            {
                TempData["Error"] = "Этот пользователь уже является капитаном.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _userManager.AddToRoleAsync(user, "Captain");

            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(
                    " ",
                    result.Errors.Select(e => e.Description));

                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation(
                "Administrator {Admin} assigned Captain role to user {UserId}",
                User.Identity?.Name,
                user.Id);

            TempData["Success"] =
                $"Пользователь {user.UserName} теперь капитан.";

            return RedirectToAction(nameof(Index));
        }

        // Снятие роли капитана
        [HttpPost]
        [ValidateAntiForgeryToken]
public async Task<IActionResult> RemoveCaptain(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest();

            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                TempData["Error"] = "Пользователь не найден.";
                return RedirectToAction(nameof(Index));
            }

            var isCaptain =
                await _userManager.IsInRoleAsync(user, "Captain");

            if (!isCaptain)
            {
                TempData["Error"] =
                    "Этот пользователь не является капитаном.";

                return RedirectToAction(nameof(Index));
            }

            // Нельзя снимать роль, если за пользователем закреплена команда
            var ownsTeam = await _context.Teams
                .AnyAsync(team => team.OwnerUserId == id);

            if (ownsTeam)
            {
                TempData["Error"] =
                    "Нельзя снять роль капитана: за пользователем закреплена команда. Сначала передай команду другому капитану.";

                return RedirectToAction(nameof(Index));
            }

            var result = await _userManager.RemoveFromRoleAsync(
                user,
                "Captain");

            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(
                    " ",
                    result.Errors.Select(e => e.Description));

                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation(
                "Administrator {Admin} removed Captain role from user {UserId}",
                User.Identity?.Name,
                user.Id);

            TempData["Success"] =
                $"Роль капитана у пользователя {user.UserName} снята.";

            return RedirectToAction(nameof(Index));
        }
    }

    public class UserRoleViewModel
    {
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsCaptain { get; set; }
        public bool IsAdmin { get; set; }
    }
}