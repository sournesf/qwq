using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    [Authorize(Roles = "Admin")]
    public class HeroesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<HeroesController> _logger;

        private const long MaxFileSize = 5 * 1024 * 1024;

        private static readonly string[] AllowedExtensions =
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        public HeroesController(
            AppDbContext context,
            IWebHostEnvironment environment,
            ILogger<HeroesController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        // Список героев
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var heroes = await _context.Heroes
                .AsNoTracking()
                .OrderBy(h => h.Name)
                .ToListAsync();

            return View(heroes);
        }

        // Страница создания
        [HttpGet]
        public IActionResult Create()
        {
            return View(new Hero());
        }

        // Создание героя с диагностикой загрузки фото
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> Create(
            Hero hero,
            IFormFile? imageFile)
        {
            _logger.LogWarning(
                "HERO DEBUG 1: запрос получен. ContentLength={Length}, FilePresent={FilePresent}",
                Request.ContentLength,
                imageFile != null);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "HERO DEBUG: ModelState invalid");

                return View(hero);
            }

            string? savedImage = null;

            try
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    _logger.LogWarning(
                        "HERO DEBUG 2: файл получен. Name={Name}, Length={Length}, ContentType={ContentType}",
                        Path.GetFileName(imageFile.FileName),
                        imageFile.Length,
                        imageFile.ContentType);

                    _logger.LogWarning(
                        "HERO DEBUG 3: начинаем сохранение файла");

                    savedImage = await SaveImageAsync(imageFile);

                    _logger.LogWarning(
                        "HERO DEBUG 4: SaveImageAsync завершён. Image={Image}",
                        savedImage ?? "(null)");

                    if (savedImage == null)
                        return View(hero);

                    hero.Image = savedImage;
                }
                else
                {
                    _logger.LogWarning(
                        "HERO DEBUG: файл не передан или пуст");
                }

                _logger.LogWarning(
                    "HERO DEBUG 5: начинаем сохранение героя в БД");

                _context.Heroes.Add(hero);
                await _context.SaveChangesAsync();

                _logger.LogWarning(
                    "HERO DEBUG 6: герой сохранён. ID={HeroId}, Image={Image}",
                    hero.HeroId,
                    hero.Image);

                TempData["Success"] = "Герой успешно добавлен!";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "HERO DEBUG: исключение при создании героя");

                if (savedImage != null)
                {
                    try
                    {
await DeleteImageIfUnusedAsync(savedImage);
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogError(
                            cleanupEx,
                            "HERO DEBUG: не удалось очистить загруженный файл");
                    }
                }

                ModelState.AddModelError(
                    string.Empty,
                    "Ошибка создания героя. Подробности записаны в лог.");

                return View(hero);
            }
        }

        // Страница редактирования
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var hero = await _context.Heroes.FindAsync(id);

            if (hero == null)
                return NotFound();

            return View(hero);
        }

        // Редактирование героя
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> Edit(
            int id,
            Hero hero,
            IFormFile? imageFile)
        {
            if (id != hero.HeroId)
                return BadRequest();

            var existingHero = await _context.Heroes.FindAsync(id);

            if (existingHero == null)
                return NotFound();

            ModelState.Remove(nameof(hero.Image));

            if (!ModelState.IsValid)
                return View(existingHero);

            string? newImage = null;
            var oldImage = existingHero.Image;

            try
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    _logger.LogWarning(
                        "HERO EDIT DEBUG: получен файл {Name}, размер {Length}",
                        Path.GetFileName(imageFile.FileName),
                        imageFile.Length);

                    newImage = await SaveImageAsync(imageFile);

                    if (newImage == null)
                        return View(existingHero);
                }

                existingHero.Name = hero.Name;
                existingHero.Role = hero.Role;
                existingHero.Description = hero.Description;

                if (newImage != null)
                    existingHero.Image = newImage;

                await _context.SaveChangesAsync();

                if (newImage != null &&
                    !string.IsNullOrWhiteSpace(oldImage))
                {
                    await DeleteImageIfUnusedAsync(oldImage);
                }

                TempData["Success"] = "Данные героя обновлены!";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "HERO EDIT DEBUG: ошибка редактирования героя {HeroId}",
                    id);

                if (newImage != null)
                {
                    try
                    {
                        await DeleteImageIfUnusedAsync(newImage);
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogError(
                            cleanupEx,
                            "Не удалось удалить новый файл {Image}",
                            newImage);
                    }
                }

                ModelState.AddModelError(
                    string.Empty,
                    "Не удалось сохранить изменения. Подробности записаны в лог.");

                return View(existingHero);
            }
        }

        // Удаление героя
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var hero = await _context.Heroes.FindAsync(id);

            if (hero == null)
                return NotFound();

            var imageName = hero.Image;

            try
            {
               
_context.Heroes.Remove(hero);
                await _context.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(imageName))
                    await DeleteImageIfUnusedAsync(imageName);

                TempData["Success"] = "Герой удалён!";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Ошибка удаления героя {HeroId}",
                    id);

                return Content(
                    "Не удалось удалить героя. Подробности записаны в лог.");
            }
        }

        // Сохранение изображения на сервере
        private async Task<string?> SaveImageAsync(IFormFile imageFile)
        {
            if (imageFile.Length <= 0)
            {
                ModelState.AddModelError(
                    "Image",
                    "Выбранный файл пуст.");

                return null;
            }

            if (imageFile.Length > MaxFileSize)
            {
                ModelState.AddModelError(
                    "Image",
                    "Размер изображения не должен превышать 5 МБ.");

                return null;
            }

            var extension = Path
                .GetExtension(imageFile.FileName)
                .ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    "Image",
                    "Разрешены только JPG, JPEG, PNG и WEBP.");

                return null;
            }

            var webRoot = _environment.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(
                    _environment.ContentRootPath,
                    "wwwroot");
            }

            var imagesFolder = Path.Combine(webRoot, "images");

            Directory.CreateDirectory(imagesFolder);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(imagesFolder, fileName);

            try
            {
                _logger.LogWarning(
                    "HERO DEBUG: сохранение в папку {Folder}",
                    imagesFolder);

                await using (var stream = new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
                {
                    await imageFile.CopyToAsync(stream);
                    await stream.FlushAsync();
                }

                _logger.LogWarning(
                    "HERO DEBUG: файл успешно сохранён: {FileName}",
                    fileName);

                return fileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "HERO DEBUG: ошибка записи файла в {FilePath}",
                    filePath);

                if (System.IO.File.Exists(filePath))
                {
                    try
                    {
                        System.IO.File.Delete(filePath);
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogError(
                            cleanupEx,
                            "Не удалось удалить неполный файл {FilePath}",
                            filePath);
                    }
                }

                throw;
            }
        }

        // Удаление файла, если он больше не используется
        private async Task DeleteImageIfUnusedAsync(string imageName)
        {
            var fileName = Path.GetFileName(imageName);

            if (string.IsNullOrWhiteSpace(fileName))
                return;

            var isUsed = await _context.Heroes.AnyAsync(h => h.Image == imageName);

            if (isUsed)
                return;

            var webRoot = _environment.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(
                    _environment.ContentRootPath,
                    "wwwroot");
            }

            var imagesFolder = Path.GetFullPath(
                Path.Combine(webRoot, "images"));

            var imagePath = Path.GetFullPath(
                Path.Combine(imagesFolder, fileName));

            if (!imagePath.StartsWith(
                    imagesFolder + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (System.IO.File.Exists(imagePath))
                System.IO.File.Delete(imagePath);
        }
    }
}