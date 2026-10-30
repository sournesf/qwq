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
    [Authorize(Roles = "Captain")]
    public class CaptainController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        private const long MaxImageSize = 5 * 1024 * 1024;

        private static readonly string[] AllowedExtensions =
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        public CaptainController(
            AppDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        private string? CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Находит команду текущего капитана.
        private async Task<Team?> GetMyTeamAsync()
        {
            var userId = CurrentUserId;

            if (userId == null)
                return null;

            return await _context.Teams
                .FirstOrDefaultAsync(t => t.OwnerUserId == userId);
        }

        // Сохраняет изображение в wwwroot/images/{folder}.
        // Если folder пустой, сохраняет прямо в wwwroot/images.
        private async Task<string?> SaveImageAsync(
            IFormFile? imageFile,
            string folder = "")
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

            // Не позволяем передать в folder путь к другой директории.
            if (!string.IsNullOrWhiteSpace(folder) &&
                (folder != Path.GetFileName(folder) ||
                 folder == "." ||
                 folder == ".."))
            {
                throw new InvalidOperationException(
                    "Некорректная папка для изображения.");
            }

            var imagesPath = Path.Combine(
                _environment.WebRootPath,
                "images");

            var folderPath = string.IsNullOrWhiteSpace(folder)
                ? imagesPath
                : Path.Combine(imagesPath, folder);

            Directory.CreateDirectory(folderPath);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(folderPath, fileName);

            await using (var stream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write))
            {
                await imageFile.CopyToAsync(stream);
            }

            Console.WriteLine($"WEB ROOT: {_environment.WebRootPath}");
            Console.WriteLine($"FILE PATH: {filePath}");
            Console.WriteLine(
                $"FILE EXISTS: {System.IO.File.Exists(filePath)}");

            return string.IsNullOrWhiteSpace(folder)
                ? $"/images/{fileName}"
                : $"/images/{folder}/{fileName}";
        }
// Удаляет старое загруженное изображение из wwwroot/images.
        // Работает и с файлами в подпапках, например players и teams.
        private void DeleteImage(
            string? imagePath,
            string folder = "")
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

            var imagesRoot = Path.GetFullPath(
                Path.Combine(_environment.WebRootPath, "images"));

            var filePath = Path.GetFullPath(
                Path.Combine(imagesRoot, relativePath));

            var imagesPrefix = imagesRoot + Path.DirectorySeparatorChar;

            // Не разрешаем удалять файлы за пределами images.
            if (!filePath.StartsWith(
                    imagesPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }

        // ПАНЕЛЬ КАПИТАНА

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var team = await GetMyTeamAsync();

            ViewBag.HasTeam = team != null;

            if (team == null)
                return View();

            var players = await _context.Players
                .AsNoTracking()
                .Where(p => p.TeamId == team.TeamId)
                .OrderBy(p => p.Nickname)
                .ToListAsync();

            ViewBag.Team = team;
            ViewBag.Players = players;

            return View();
        }

        // СОЗДАНИЕ КОМАНДЫ

        [HttpGet]
        public async Task<IActionResult> CreateTeam()
        {
            if (await GetMyTeamAsync() != null)
                return RedirectToAction(nameof(Index));

            return View(new Team());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeam(
            Team input,
            IFormFile? imageFile)
        {
            var userId = CurrentUserId;

            if (userId == null)
                return Challenge();

            if (await GetMyTeamAsync() != null)
                return RedirectToAction(nameof(Index));

            ModelState.Remove(nameof(input.OwnerUserId));
            ModelState.Remove(nameof(input.OwnerUser));

            if (!ModelState.IsValid)
                return View(input);

            string imagePath = "";

            try
            {
                // Фото команды сохраняется прямо в wwwroot/images.
                var savedPath = await SaveImageAsync(imageFile);

                if (imageFile != null && imageFile.Length > 0)
                {
                    if (string.IsNullOrWhiteSpace(savedPath))
                    {
                        ModelState.AddModelError(
                            nameof(input.Image),
                            "Не удалось сохранить изображение команды.");

                        return View(input);
                    }

                    imagePath = savedPath;
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(input.Image), ex.Message);
                return View(input);
            }
            catch (IOException)
            {
                ModelState.AddModelError(
                    nameof(input.Image),
                    "Ошибка записи файла. Проверь папку для изображений.");
return View(input);
            }

            var team = new Team
            {
                Name = input.Name,
                Country = input.Country,
                Founded = input.Founded,
                Image = imagePath,
                OwnerUserId = userId
            };

            _context.Teams.Add(team);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Команда успешно создана!";

            return RedirectToAction(nameof(Index));
        }

        // РЕДАКТИРОВАНИЕ КОМАНДЫ

        [HttpGet]
        public async Task<IActionResult> EditTeam()
        {
            var team = await GetMyTeamAsync();

            if (team == null)
                return RedirectToAction(nameof(CreateTeam));

            return View(team);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTeam(
            Team input,
            IFormFile? imageFile)
        {
            var team = await GetMyTeamAsync();

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
                {
                    // Новое фото команды сохраняется прямо в images.
                    newImagePath = await SaveImageAsync(imageFile);

                    if (string.IsNullOrWhiteSpace(newImagePath))
                    {
                        ModelState.AddModelError(
                            nameof(input.Image),
                            "Не удалось сохранить новое изображение команды.");

                        return View(team);
                    }
                }
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
                    "Ошибка записи файла. Проверь папку для изображений.");

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

            TempData["Success"] = "Данные команды обновлены!";

            return RedirectToAction(nameof(Index));
        }

        // СПИСОК ИГРОКОВ

        [HttpGet]
        public async Task<IActionResult> Players()
        {
            var team = await GetMyTeamAsync();

            if (team == null)
            {
                TempData["Error"] = "Сначала создай свою команду.";
                return RedirectToAction(nameof(CreateTeam));
            }

            var players = await _context.Players
                .AsNoTracking()
                .Where(p => p.TeamId == team.TeamId)
                .OrderBy(p => p.Nickname)
                .ToListAsync();

            ViewBag.Team = team;

            return View(players);
        }

        // ДОБАВЛЕНИЕ ИГРОКА

        [HttpGet]
        public async Task<IActionResult> CreatePlayer()
        {
            if (await GetMyTeamAsync() == null)
                return RedirectToAction(nameof(CreateTeam));

            return View(new Player());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePlayer(
            Player input,
            IFormFile? imageFile)
        {
var team = await GetMyTeamAsync();

            if (team == null)
                return Forbid();

            ModelState.Remove(nameof(input.Team));
            ModelState.Remove(nameof(input.TeamId));

            if (!ModelState.IsValid)
                return View(input);

            string? imagePath;

            try
            {
                // Фото игрока сохраняется в wwwroot/images/players.
                imagePath = await SaveImageAsync(imageFile, "players");
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(input.Image), ex.Message);
                return View(input);
            }
            catch (IOException)
            {
                ModelState.AddModelError(
                    nameof(input.Image),
                    "Ошибка записи файла. Проверь папку для изображений.");

                return View(input);
            }

            var player = new Player
            {
                Nickname = input.Nickname,
                Position = input.Position,
                Image = imagePath ?? "",
                TeamId = team.TeamId
            };

            _context.Players.Add(player);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Игрок добавлен!";

            return RedirectToAction(nameof(Players));
        }

        // РЕДАКТИРОВАНИЕ ИГРОКА

        [HttpGet]
        public async Task<IActionResult> EditPlayer(int id)
        {
            var team = await GetMyTeamAsync();

            if (team == null)
                return Forbid();

            var player = await _context.Players
                .FirstOrDefaultAsync(p =>
                    p.PlayerId == id &&
                    p.TeamId == team.TeamId);

            if (player == null)
                return NotFound();

            return View(player);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPlayer(
            int id,
            Player input,
            IFormFile? imageFile)
        {
            var team = await GetMyTeamAsync();

            if (team == null)
                return Forbid();

            var player = await _context.Players
                .FirstOrDefaultAsync(p =>
                    p.PlayerId == id &&
                    p.TeamId == team.TeamId);

            if (player == null)
                return NotFound();

            ModelState.Remove(nameof(input.Team));
            ModelState.Remove(nameof(input.TeamId));

            if (!ModelState.IsValid)
                return View(input);

            string? newImagePath = null;

            try
            {
                if (imageFile != null && imageFile.Length > 0)
                    newImagePath = await SaveImageAsync(imageFile, "players");
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(input.Image), ex.Message);
                return View(input);
            }
            catch (IOException)
            {
                ModelState.AddModelError(
                    nameof(input.Image),
                    "Ошибка записи файла. Проверь папку для изображений.");

                return View(input);
            }

            var oldImagePath = player.Image;

            player.Nickname = input.Nickname;
            player.Position = input.Position;

            if (newImagePath != null)
                player.Image = newImagePath;

            await _context.SaveChangesAsync();

            if (newImagePath != null)
                DeleteImage(oldImagePath, "players");

            TempData["Success"] = "Данные игрока обновлены!";

            return RedirectToAction(nameof(Players));
        }

        // УДАЛЕНИЕ ИГРОКА

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePlayer(int id)
        {
var team = await GetMyTeamAsync();

            if (team == null)
                return Forbid();

            var player = await _context.Players
                .FirstOrDefaultAsync(p =>
                    p.PlayerId == id &&
                    p.TeamId == team.TeamId);

            if (player == null)
                return NotFound();

            var imagePath = player.Image;

            _context.Players.Remove(player);
            await _context.SaveChangesAsync();

            DeleteImage(imagePath, "players");

            TempData["Success"] = "Игрок удалён!";

            return RedirectToAction(nameof(Players));
        }
    }
}