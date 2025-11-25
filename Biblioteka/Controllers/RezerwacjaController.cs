using Biblioteka.Data;
using Biblioteka.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using System;

namespace Biblioteka.Controllers
{
    [Authorize]
    public class RezerwacjaController : Controller
    {
        private readonly BibliotekaContext _context;

        public RezerwacjaController(BibliotekaContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "worker,admin")]
        public async Task<IActionResult> Index(string? searchString, bool? readyForPickup)
        {
            var rezerwacjeQuery = _context.Rezerwacje
                .Where(r => r.IsActive)
                .Include(r => r.User)
                .Include(r => r.Ksiazka)
                .AsQueryable();

            if (readyForPickup.HasValue && readyForPickup.Value)
            {
                rezerwacjeQuery = rezerwacjeQuery.Where(r => r.Ksiazka!.stan == "Gotowa do Odbioru");
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.ToLower();
                rezerwacjeQuery = rezerwacjeQuery.Where(r =>
                    r.Ksiazka!.tytul.ToLower().Contains(searchString) ||
                    r.User!.Nazwisko.ToLower().Contains(searchString));
            }

            var aktywneRezerwacje = await rezerwacjeQuery
                .OrderByDescending(r => r.Ksiazka!.stan == "Gotowa do Odbioru")
                .ThenBy(r => r.KsiazkaId)
                .ThenBy(r => r.DataRezerwacji)
                .ToListAsync();

            ViewBag.CurrentSearch = searchString;
            ViewBag.IsReady = readyForPickup;

            return View(aktywneRezerwacje);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
            {
                return Unauthorized();
            }

            var rezerwacja = await _context.Rezerwacje.Include(r => r.Ksiazka).FirstOrDefaultAsync(r => r.Id == id);

            if (rezerwacja == null || !rezerwacja.IsActive)
            {
                TempData["Message"] = "Błąd: Aktywna rezerwacja nie została znaleziona.";
                return RedirectToAction("UserPage", "Home");
            }

            if (rezerwacja.UserId != userId && !User.IsInRole("worker") && !User.IsInRole("admin"))
            {
                return Forbid();
            }

            rezerwacja.IsActive = false;
            _context.Rezerwacje.Update(rezerwacja);
            await _context.SaveChangesAsync();

            TempData["Message"] = $"Pomyślnie anulowano rezerwację dla '{rezerwacja.Ksiazka?.tytul}'.";
            
            if (User.IsInRole("worker") || User.IsInRole("admin"))
            {
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction("UserPage", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "worker,admin")]
        public async Task<IActionResult> FinalizePickup(int id)
        {
            var rezerwacja = await _context.Rezerwacje
                .Include(r => r.Ksiazka)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);

            if (rezerwacja == null || rezerwacja.Ksiazka?.stan != "Gotowa do Odbioru")
            {
                TempData["Message"] = "Błąd: Rezerwacja jest nieaktywna lub książka nie jest gotowa do odbioru.";
                return RedirectToAction(nameof(Index));
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var noweWypozyczenie = new Wypozyczenie
                {
                    UserId = rezerwacja.UserId,
                    KsiazkaId = rezerwacja.KsiazkaId,
                    DataWypozyczenia = DateTime.Now,
                    OczekiwanaDataZwrotu = DateTime.Now.AddDays(14),
                    Przedluzono = false
                };
                _context.Wypozyczenia.Add(noweWypozyczenie);

                rezerwacja.Ksiazka!.stan = "Wypożyczona";
                _context.Ksiazki.Update(rezerwacja.Ksiazka);

                rezerwacja.IsActive = false;
                _context.Rezerwacje.Update(rezerwacja);

                rezerwacja.User!.iloscWypKsiazek++;
                _context.Users.Update(rezerwacja.User);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Message"] = $"Pomyślnie wypożyczono książkę '{rezerwacja.Ksiazka.tytul}' użytkownikowi {rezerwacja.User?.email}.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Message"] = "Wystąpił błąd podczas finalizacji odbioru.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
