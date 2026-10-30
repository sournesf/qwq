using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using WebApplication2.Data;
using WebApplication2.Models;


namespace WebApplication2.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }


        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            var heroes = _context.Heroes.ToList();
            return View(heroes);
        }


        public IActionResult Contact()
        {
            var tournaments = _context.Tournaments.ToList();
            return View(tournaments);
        }


        public IActionResult Teams()
        {
            var teams = _context.Teams
                .OrderByDescending(t => t.TeamId)
                .ToList();
            return View(teams);
        }


        public IActionResult Players()
        {
            var players = _context.Players
                .Include(p => p.Team)
                .ToList();

            return View(players);
        }


        public IActionResult Items()
        {
            var items = _context.Items.ToList();
            return View(items);
        }
        public IActionResult Matches()
        {
            var matches = _context.Matches
                .Include(m => m.Tournament)
                .Include(m => m.Team1)
                .Include(m => m.Team2)
                .Include(m => m.Winner)
                .ToList();

            return View(matches);
        }
        [HttpGet]
        public IActionResult CreateTeam()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return RedirectToPage("/Account/Login", new
                {
                    area = "Identity"
                });
            }

            return View();

        }
        [HttpPost]
        [Authorize(Roles = "Admin,Captain")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeam(Team team)
        {
            var userId = User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            // Капитан может создать только одну команду.
            // Администратор может создавать несколько команд.
            if (User.IsInRole("Captain") && !User.IsInRole("Admin"))
            {
                var alreadyHasTeam = await _context.Teams
                    .AnyAsync(t => t.OwnerUserId == userId);

                if (alreadyHasTeam)
                {
                    return Forbid();
                }

                team.OwnerUserId = userId;
            }
            else
            {
                // Если администратор создаёт команду, не позволяем
                // подменить владельца произвольным значением из формы.
                team.OwnerUserId = null;
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _context.Teams.Add(team);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Teams));
        }



        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
