using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class PlayersController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;


    public PlayersController(
        AppDbContext context,
        IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // Список игроков доступен всем
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var players = await _context.Players
                .AsNoTracking()
                .Include(p => p.Team)
                .OrderBy(p => p.Team!.Name)
                .ThenBy(p => p.Nickname)
                .ToListAsync();

            return View("~/Views/Home/Players.cshtml", players);
        }

        // Страница добавления игрока
        [Authorize(Roles = "Admin,Captain")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadTeamsAsync();
            return View();
        }

        // Добавление игрока
        [Authorize(Roles = "Admin,Captain")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Player player,
            IFormFile? imageFile)
        {
            if (!await _context.Teams.AnyAsync(t => t.TeamId == player.TeamId))
            {
                ModelState.AddModelError("TeamId", "Выбранная команда не найдена.");
            }

            if (imageFile != null && imageFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(
                        "imageFile",
                        "Разрешены только JPG, JPEG, PNG и WEBP.");
                }

                if (imageFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError(
                        "imageFile",
                        "Размер изображения не должен превышать 5 МБ.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadTeamsAsync();
                return View(player);
            }

            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(
                    _environment.WebRootPath,
                    "images",
                    "players");

                Directory.CreateDirectory(uploadsFolder);

                var fileName = Guid.NewGuid().ToString("N")
                    + Path.GetExtension(imageFile.FileName).ToLowerInvariant();

                var filePath = Path.Combine(uploadsFolder, fileName);

                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                player.Image = "players/" + fileName;
            }
            else
            {
                player.Image = "";
            }

            _context.Players.Add(player);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Игрок {player.Nickname} добавлен.";

            return RedirectToAction(nameof(Index));
        }

        // Удаление игрока
        [Authorize(Roles = "Admin,Captain")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var player = await _context.Players
                .FirstOrDefaultAsync(p => p.PlayerId == id);

            if (player == null)
            {
                return NotFound();
            }

            // Удаляем изображение только из папки images/players
            if (!string.IsNullOrWhiteSpace(player.Image))
            {
                var imageName = player.Image.Replace('\\', '/');

                if (imageName.StartsWith("players/", StringComparison.OrdinalIgnoreCase))
                {
                    var fileName = Path.GetFileName(imageName);

                    var imagePath = Path.Combine(
                        _environment.WebRootPath,
                        "images",
                        "players",
                        fileName);

                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }
            }

            _context.Players.Remove(player);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Игрок {player.Nickname} удалён.";

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadTeamsAsync()
        {
            var teams = await _context.Teams
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .ToListAsync();

            ViewBag.Teams = new SelectList(
                teams,
                "TeamId",
                "Name");
        }
    }
}