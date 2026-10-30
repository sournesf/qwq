using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    [Authorize(Roles = "Admin,Captain")]
    public class TeamsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        private const long MaxImageSize = 5 * 1024 * 1024;

        private static readonly string[] AllowedExtensions =
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        public TeamsController(
            AppDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        private string? CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Получение команды, доступной пользователю для редактирования.
        private async Task<Team?> GetEditableTeamAsync(int id)
        {
            var team = await _context.Teams
                .FirstOrDefaultAsync(t => t.TeamId == id);

            if (team == null)
                return null;

            if (User.IsInRole("Admin"))
                return team;

            if (User.IsInRole("Captain") &&
                team.OwnerUserId == CurrentUserId)
            {
                return team;
            }

            return null;
        }

        // Сохранение нового изображения в wwwroot/images.
        private async Task<string?> SaveImageAsync(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                return null;

            if (imageFile.Length > MaxImageSize)
            {
                throw new InvalidOperationException(
                    "Размер изображения не должен превышать 5 МБ.");
            }

            var extension = Path
                .GetExtension(imageFile.FileName)
                .ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException(
                    "Поддерживаются только JPG, JPEG, PNG и WEBP.");
            }

            if (string.IsNullOrWhiteSpace(_environment.WebRootPath))
            {
                throw new InvalidOperationException(
                    "Не удалось определить папку wwwroot.");
            }

            var imagesPath = Path.Combine(
                _environment.WebRootPath,
                "images");

            Directory.CreateDirectory(imagesPath);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(imagesPath, fileName);

            await using (var stream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write))
            {
                await imageFile.CopyToAsync(stream);
            }

            return $"/images/{fileName}";
        }

        // Удаление старого загруженного изображения.
        private void DeleteImage(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) ||
                string.IsNullOrWhiteSpace(_environment.WebRootPath))
            {
                return;
            }

            var normalizedPath = imagePath.Replace('\\', '/');

            if (!normalizedPath.StartsWith(
                    "/images/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var relativePath = normalizedPath
                .Substring("/images/".Length)
                .Replace('/', Path.DirectorySeparatorChar);

            var imagesRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "images"));

            var filePath = Path.GetFullPath(
                Path.Combine(imagesRoot, relativePath));

            var rootPrefix = imagesRoot + Path.DirectorySeparatorChar;

            if (!filePath.StartsWith(
                    rootPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }

        // СПИСОК КОМАНД

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var teams = await _context.Teams
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View(teams);
        }

        // ОТКРЫТИЕ ФОРМЫ РЕДАКТИРОВАНИЯ

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var team = await GetEditableTeamAsync(id);

            if (team == null)
                return NotFound();

            return View(team);
        }

        // СОХРАНЕНИЕ ИЗМЕНЕНИЙ

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Team input,
            IFormFile? imageFile)
        {
            var team = await GetEditableTeamAsync(id);

            if (team == null)
                return NotFound();

            ModelState.Remove(nameof(input.OwnerUserId));
            ModelState.Remove(nameof(input.OwnerUser));

            if (!ModelState.IsValid)
                return View(team);

            string? newImagePath = null;

            try
            {
                if (imageFile != null && imageFile.Length > 0)
                    newImagePath = await SaveImageAsync(imageFile);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(input.Image), ex.Message);
                return View(team);
            }
            catch (IOException)
            {
                ModelState.AddModelError(
                    nameof(input.Image),
                    "Не удалось записать изображение.");

                return View(team);
            }

            var oldImagePath = team.Image;

            team.Name = input.Name;
            team.Country = input.Country;
            team.Founded = input.Founded;

            if (newImagePath != null)
                team.Image = newImagePath;

            await _context.SaveChangesAsync();

            if (newImagePath != null)
                DeleteImage(oldImagePath);

            TempData["Success"] = "Команда успешно обновлена!";

            return RedirectToAction(nameof(Index));
        }

        // УДАЛЕНИЕ КОМАНДЫ — ТОЛЬКО АДМИНИСТРАТОР

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var team = await _context.Teams
                .FirstOrDefaultAsync(t => t.TeamId == id);

            if (team == null)
                return NotFound();

            var players = await _context.Players
                .Where(p => p.TeamId == id)
                .ToListAsync();

            var imagePath = team.Image;

            _context.Players.RemoveRange(players);
            _context.Teams.Remove(team);

            await _context.SaveChangesAsync();

            DeleteImage(imagePath);

            TempData["Success"] = "Команда и её игроки удалены.";

            return RedirectToAction(nameof(Index));
        }
    }
}