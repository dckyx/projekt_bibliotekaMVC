using Biblioteka.Data;
using Biblioteka.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;

namespace Biblioteka.Controllers
{
    [Authorize(Roles = "admin")]
    public class UserController : Controller
    {
        private readonly BibliotekaContext _context;

        public UserController(BibliotekaContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users.OrderBy(u => u.Nazwisko).ToListAsync();
            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var viewModel = new UserRoleViewModel
            {
                User = user,
                AvailableRoles = new SelectList(new List<string> { "user", "worker", "admin" }, user.Rola)
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserRoleViewModel viewModel)
        {
            if (id != viewModel.User.Id) return NotFound();

            var userToUpdate = await _context.Users.FindAsync(id);
            if (userToUpdate == null) return NotFound();

            if (await TryUpdateModelAsync<User>(userToUpdate, "User", u => u.Rola, u => u.Kara, u => u.IsBlocked))
            {
                try
                {
                    await _context.SaveChangesAsync();
                    TempData["Message"] = $"Dane użytkownika '{userToUpdate.email}' zostały zaktualizowane.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Users.Any(e => e.Id == id)) return NotFound();
                    else throw;
                }
            }
            viewModel.AvailableRoles = new SelectList(new List<string> { "user", "worker", "admin" }, viewModel.User.Rola);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userToDelete = await _context.Users.FindAsync(id);
            if (userToDelete == null)
            {
                TempData["Message"] = "Nie znaleziono użytkownika.";
                return RedirectToAction(nameof(Index));
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userToDelete.Id.ToString() == currentUserId)
            {
                TempData["Message"] = "Nie można usunąć własnego konta.";
                return RedirectToAction(nameof(Index));
            }

            _context.Users.Remove(userToDelete);
            await _context.SaveChangesAsync();

            TempData["Message"] = $"Użytkownik {userToDelete.email} został usunięty.";
            return RedirectToAction(nameof(Index));
        }
    }
}
