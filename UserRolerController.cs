using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Users()
        {
            var users = _userManager.Users.ToList();
            var result = new List<AdminUserViewModel>();

            foreach (var user in users)
            {
                result.Add(new AdminUserViewModel
                {
                    Id = user.Id,
                    Email = user.Email ?? "",
                    IsAdmin = await _userManager.IsInRoleAsync(user, "Admin"),
                    IsCaptain = await _userManager.IsInRoleAsync(user, "Captain")
                });
            }

            return View(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCaptain(string id, bool makeCaptain)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["Error"] =
                    "Нельзя менять роль Captain у администратора.";

                return RedirectToAction(nameof(Users));
            }

            IdentityResult result;

            if (makeCaptain)
            {
                if (await _userManager.IsInRoleAsync(user, "Captain"))
                {
                    return RedirectToAction(nameof(Users));
                }

                result = await _userManager.AddToRoleAsync(user, "Captain");
            }
            else
            {
                if (!await _userManager.IsInRoleAsync(user, "Captain"))
                {
                    return RedirectToAction(nameof(Users));
                }

                result = await _userManager.RemoveFromRoleAsync(user, "Captain");
            }

            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(
                    "; ",
                    result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Users));
        }
    }
}





