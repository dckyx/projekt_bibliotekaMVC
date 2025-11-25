using Biblioteka.Data;
using Biblioteka.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering; // Potrzebne dla SelectList

namespace Biblioteka.Controllers
{
    [Authorize]
    public class WypozyczenieController : Controller
    {
        private readonly BibliotekaContext _context;
        private const string KoszykSessionKey = "KsiazkiKoszyk";
        private const int MaxBooksLimit = 5; // limit książek

        public WypozyczenieController(BibliotekaContext context)
        {
            _context = context;
        }

        // Pomocnicza metoda do pobierania listy ID z sesji
        private List<int> GetBasketItems()
        {
            var sessionData = HttpContext.Session.GetString(KoszykSessionKey);
            return sessionData == null
                ? new List<int>()
                : JsonSerializer.Deserialize<List<int>>(sessionData) ?? new List<int>();
        }

        // wypożyczenie z koszyka
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalizeWypozyczenie(int okresWypozyczenia)
        {
            var koszykIds = GetBasketItems();

            if (!koszykIds.Any())
            {
                TempData["Message"] = "Błąd: Koszyk jest pusty.";
                return RedirectToAction("ViewBasket", "Ksiazka");
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
            {
                TempData["Message"] = "Błąd autoryzacji użytkownika.";
                return RedirectToAction("Login", "Home");
            }

            var user = await _context.Users.FindAsync(userId);

            // czy user jest zablokowany
            if (user!.IsBlocked)
            {
                TempData["Message"] = $"Twoje konto jest zablokowane z powodu niezapłaconych kar lub zaległych książek. Skontaktuj się z obsługą.";
                return RedirectToAction("UserPage", "Home");
            }

            var ksiazkiDoWypozyczenia = await _context.Ksiazki
                .Where(k => koszykIds.Contains(k.Id))
                .ToListAsync();

            // limit wypożyczeń
            int currentBorrowedCount = user!.iloscWypKsiazek;
            int newBooksCount = ksiazkiDoWypozyczenia.Count;

            if (currentBorrowedCount >= MaxBooksLimit)
            {
                TempData["Message"] = $"Osiągnąłeś/aś maksymalny limit wypożyczeń ({MaxBooksLimit} książek). Zwróć książki, aby wypożyczyć nowe.";
                return RedirectToAction("ViewBasket", "Ksiazka");
            }

            if (currentBorrowedCount + newBooksCount > MaxBooksLimit)
            {
                int canBorrow = MaxBooksLimit - currentBorrowedCount;
                TempData["Message"] = $"Nie można zrealizować koszyka. Posiadasz już {currentBorrowedCount} aktywnych wypożyczeń. Możesz wypożyczyć tylko {canBorrow} więcej książek.";
                return RedirectToAction("ViewBasket", "Ksiazka");
            }


            // dostępność książek
            if (ksiazkiDoWypozyczenia.Any(k => k.stan != "Dostępna"))
            {
                TempData["Message"] = "Błąd: Co najmniej jedna książka w koszyku nie jest już dostępna do wypożyczenia.";
                return RedirectToAction("ViewBasket", "Ksiazka");
            }

            // transakcja + zapis
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var dataWypozyczenia = DateTime.Now;
                var dataZwrotu = dataWypozyczenia.AddDays(okresWypozyczenia);
                var liczbaWypozyczonych = 0;

                foreach (var ksiazka in ksiazkiDoWypozyczenia)
                {
                    // tworzenie wypożyczenia 
                    var noweWypozyczenie = new Wypozyczenie
                    {
                        UserId = userId,
                        KsiazkaId = ksiazka.Id,
                        DataWypozyczenia = dataWypozyczenia,
                        OczekiwanaDataZwrotu = dataZwrotu,
                        FaktycznaDataZwrotu = null
                    };
                    _context.Wypozyczenia.Add(noweWypozyczenie);

                    ksiazka.stan = "Wypożyczona";
                    _context.Ksiazki.Update(ksiazka);

                    liczbaWypozyczonych++;
                }

                // licznik wypożyczeń ++
                user!.iloscWypKsiazek += liczbaWypozyczonych;
                _context.Users.Update(user);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                HttpContext.Session.Remove(KoszykSessionKey);

                TempData["Message"] = $"Pomyślnie wypożyczono {liczbaWypozyczonych} książki. Termin zwrotu: {dataZwrotu.ToShortDateString()}.";
                return RedirectToAction("UserPage", "Home");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Message"] = "Wystąpił nieoczekiwany błąd podczas finalizacji wypożyczenia. Spróbuj ponownie.";
                return RedirectToAction("ViewBasket", "Ksiazka");
            }
        }

        // lista aktywnych wypożyczeń dla workera i admina
        [HttpGet]
        [Authorize(Roles = "worker,admin")]
        public async Task<IActionResult> Index(string? searchString, bool? overdue)
        {
            // base zapytanie
            var wypozyczeniaQuery = _context.Wypozyczenia
                .Where(w => w.FaktycznaDataZwrotu == null) // Tylko aktywne wypożyczenia
                .Include(w => w.User)
                .Include(w => w.Ksiazka)
                    .ThenInclude(k => k.Kategoria)
                .AsQueryable();

            // przeterminowane
            if (overdue.HasValue && overdue.Value)
            {
                wypozyczeniaQuery = wypozyczeniaQuery.Where(w => w.OczekiwanaDataZwrotu < DateTime.Now);
            }

            // wyszukiwanie tekstowe
            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.ToLower();
                wypozyczeniaQuery = wypozyczeniaQuery.Where(w =>
                    w.Ksiazka!.tytul.ToLower().Contains(searchString) ||
                    w.User!.Nazwisko.ToLower().Contains(searchString) ||
                    w.User!.Imie.ToLower().Contains(searchString));
            }

            // pobieranie i sort
            var aktywneWypozyczenia = await wypozyczeniaQuery
                .OrderBy(w => w.OczekiwanaDataZwrotu)
                .ToListAsync();

            ViewBag.CurrentSearch = searchString;
            ViewBag.IsOverdue = overdue;

            return View(aktywneWypozyczenia);
        }

        // zwrot książki
        [HttpPost]
        [Authorize(Roles = "worker,admin")]
        public async Task<IActionResult> Return(int id) // ID Wypożyczenia
        {
            var wypozyczenie = await _context.Wypozyczenia
                .Include(w => w.User)
                .Include(w => w.Ksiazka)
                .FirstOrDefaultAsync(w => w.Id == id && w.FaktycznaDataZwrotu == null);

            if (wypozyczenie == null)
            {
                TempData["Message"] = "Błąd: Aktywne wypożyczenie nie zostało znalezione.";
                return RedirectToAction(nameof(Index));
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // aktualizacja wypożyczenia
                wypozyczenie.FaktycznaDataZwrotu = DateTime.Now;
                _context.Wypozyczenia.Update(wypozyczenie);

                // licznik usera --
                wypozyczenie.User!.iloscWypKsiazek--;
                _context.Users.Update(wypozyczenie.User);

                // pierwsza aktywna rezerwacja dla książki
                var najstarszaRezerwacja = await _context.Rezerwacje
                    .Where(r => r.KsiazkaId == wypozyczenie.KsiazkaId && r.IsActive)
                    .Include(r => r.User)
                    .OrderBy(r => r.DataRezerwacji)
                    .FirstOrDefaultAsync();

                // akutalizacja stanu książki
                if (najstarszaRezerwacja != null)
                {
                    wypozyczenie.Ksiazka.stan = "Gotowa do Odbioru";

                    // 2 dni na odbiór
                    najstarszaRezerwacja.DataWygasniecia = DateTime.Now.AddDays(2);
                    _context.Rezerwacje.Update(najstarszaRezerwacja);

                    // dla personelu info
                    TempData["Message"] = $"Zwrócono '{wypozyczenie.Ksiazka.tytul}'. Stan: GOTOWA DO ODBIORU. Aktywowano rezerwację dla: {najstarszaRezerwacja.User?.email}. Termin odbioru: {najstarszaRezerwacja.DataWygasniecia.ToShortDateString()}.";
                }
                else
                {
                    wypozyczenie.Ksiazka.stan = "Dostępna";
                    TempData["Message"] = $"Zwrócono '{wypozyczenie.Ksiazka.tytul}'. Stan: Dostępna.";
                }
                _context.Ksiazki.Update(wypozyczenie.Ksiazka);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Message"] = "Wystąpił błąd podczas rejestracji zwrotu.";
                return RedirectToAction(nameof(Index));
            }
        }

        // przedluzenie
        [HttpPost]
        [Authorize(Roles = "user")]
        public async Task<IActionResult> Extend(int wypozyczenieId, int dni)
        {
            var wypozyczenie = await _context.Wypozyczenia
                .Include(w => w.Ksiazka)
                .FirstOrDefaultAsync(w => w.Id == wypozyczenieId && w.FaktycznaDataZwrotu == null);

            if (wypozyczenie == null)
            {
                TempData["Message"] = "Nie znaleziono aktywnego wypożyczenia.";
                return RedirectToAction("UserPage", "Home");
            }

            // blokada przedluzenia
            var maRezerwacje = await _context.Rezerwacje
                .AnyAsync(r => r.KsiazkaId == wypozyczenie.KsiazkaId && r.IsActive);

            if (maRezerwacje)
            {
                TempData["Message"] = $"Nie można przedłużyć wypożyczenia książki '{wypozyczenie.Ksiazka.tytul}', ponieważ jest na nią aktywna rezerwacja.";
                return RedirectToAction("UserPage", "Home");
            }

            // tylko jedno przedluzenie moze byc
            if (wypozyczenie.Przedluzono)
            {
                TempData["Message"] = $"Nie można przedłużyć wypożyczenia książki '{wypozyczenie.Ksiazka.tytul}' po raz kolejny.";
                return RedirectToAction("UserPage", "Home");
            }

            int dniPrzedluzenia = dni;

            // update daty zwrotu
            wypozyczenie.OczekiwanaDataZwrotu = wypozyczenie.OczekiwanaDataZwrotu.AddDays(dni);
            wypozyczenie.Przedluzono = true;
            _context.Wypozyczenia.Update(wypozyczenie);
            await _context.SaveChangesAsync();
            TempData["Message"] = $"Pomyślnie przedłużono wypożyczenie książki '{wypozyczenie.Ksiazka.tytul}' o {dniPrzedluzenia} dni. Nowy termin zwrotu: {wypozyczenie.OczekiwanaDataZwrotu.ToShortDateString()}.";
            return RedirectToAction("UserPage", "Home");
        }
    }
}