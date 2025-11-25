using Biblioteka.Data;
using Biblioteka.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.Json;
using System.Security.Claims;

namespace Biblioteka.Controllers
{
    public class KsiazkaController : Controller
    {
        private readonly BibliotekaContext _context;
        private const string KoszykSessionKey = "KsiazkiKoszyk";

        public KsiazkaController(BibliotekaContext context)
        {
            _context = context;
        }

        private List<int> GetBasketItems()
        {
            var sessionData = HttpContext.Session.GetString(KoszykSessionKey);
            return sessionData == null ? new List<int>() : JsonSerializer.Deserialize<List<int>>(sessionData) ?? new List<int>();
        }

        private void SaveBasketItems(List<int> basketItems)
        {
            HttpContext.Session.SetString(KoszykSessionKey, JsonSerializer.Serialize(basketItems));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Index(int? kategoriaId, string? searchString)
        {
            ViewBag.KategoriaId = new SelectList(await _context.Kategorie.OrderBy(k => k.Nazwa).ToListAsync(), "Id", "Nazwa", kategoriaId);
            ViewBag.SelectedKategoriaId = kategoriaId;
            ViewBag.CurrentFilter = searchString;

            var ksiazkiQuery = _context.Ksiazki.Include(k => k.Kategoria).AsQueryable();

            if (kategoriaId.HasValue && kategoriaId.Value > 0)
            {
                ksiazkiQuery = ksiazkiQuery.Where(k => k.KategoriaId == kategoriaId.Value);
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                var allBooksInQuery = await ksiazkiQuery.ToListAsync();
                var orGroups = searchString.Split(new[] { " OR " }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var finalResults = new HashSet<Ksiazka>();

                if (!orGroups.Any() && !string.IsNullOrWhiteSpace(searchString))
                {
                    orGroups = new[] { searchString };
                }

                foreach (var group in orGroups)
                {
                    var groupResults = allBooksInQuery.AsEnumerable();
                    var andTerms = group.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    foreach (var term in andTerms)
                    {
                        var currentTerm = term.ToUpper();
                        if (string.IsNullOrWhiteSpace(currentTerm)) continue;

                        if (currentTerm.StartsWith("-"))
                        {
                            var notTerm = currentTerm.Substring(1);
                            if (string.IsNullOrWhiteSpace(notTerm)) continue;
                            
                            groupResults = groupResults.Where(k => 
                                !((k.tytul ?? "").ToUpper().Contains(notTerm) ||
                                  (k.autor ?? "").ToUpper().Contains(notTerm) ||
                                  k.isbn.ToString().Contains(notTerm) ||
                                  (k.tag ?? "").ToUpper().Contains(notTerm))
                            );
                        }
                        else
                        {
                            groupResults = groupResults.Where(k =>
                                (k.tytul ?? "").ToUpper().Contains(currentTerm) ||
                                (k.autor ?? "").ToUpper().Contains(currentTerm) ||
                                k.isbn.ToString().Contains(currentTerm) ||
                                (k.tag ?? "").ToUpper().Contains(currentTerm)
                            );
                        }
                    }
                    
                    foreach(var book in groupResults)
                    {
                        finalResults.Add(book);
                    }
                }
                return View(finalResults.OrderBy(k => k.tytul).ToList());
            }

            var ksiazki = await ksiazkiQuery.OrderBy(k => k.tytul).ToListAsync();
            return View(ksiazki);
        }

        [HttpGet]
        [Authorize(Roles = "worker,admin")]
        public async Task<IActionResult> Create()
        {
            ViewBag.KategoriaId = new SelectList(await _context.Kategorie.ToListAsync(), "Id", "Nazwa");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "worker,admin")]
        public async Task<IActionResult> Create(Ksiazka ksiazka)
        {
            if (ModelState.IsValid)
            {
                ksiazka.stan = "Dostępna";
                _context.Add(ksiazka);
                await _context.SaveChangesAsync();
                TempData["Message"] = $"Dodano książkę: {ksiazka.tytul}";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.KategoriaId = new SelectList(await _context.Kategorie.ToListAsync(), "Id", "Nazwa", ksiazka.KategoriaId);
            return View(ksiazka);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "worker,admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var ksiazka = await _context.Ksiazki.FindAsync(id);
            if (ksiazka == null) return NotFound();
            _context.Ksiazki.Remove(ksiazka);
            await _context.SaveChangesAsync();
            TempData["Message"] = $"Usunięto książkę: {ksiazka.tytul}";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "admin,worker")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var ksiazka = await _context.Ksiazki.FindAsync(id);
            if (ksiazka == null) return NotFound();
            ViewData["KategoriaId"] = new SelectList(_context.Kategorie.OrderBy(k => k.Nazwa), "Id", "Nazwa", ksiazka.KategoriaId);
            ViewBag.StanList = new SelectList(new List<string> { "Dostępna", "Wypożyczona", "Zarezerwowana", "Wycofana" }, ksiazka.stan);
            return View(ksiazka);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,worker")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,tytul,autor,isbn,KategoriaId,tag,stan,SpisTresci")] Ksiazka ksiazka)
        {
            if (id != ksiazka.Id) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(ksiazka);
                    await _context.SaveChangesAsync();
                    TempData["Message"] = $"Książka '{ksiazka.tytul}' została pomyślnie zaktualizowana.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Ksiazki.Any(e => e.Id == ksiazka.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["KategoriaId"] = new SelectList(_context.Kategorie.OrderBy(k => k.Nazwa), "Id", "Nazwa", ksiazka.KategoriaId);
            ViewBag.StanList = new SelectList(new List<string> { "Dostępna", "Wypożyczona", "Zarezerwowana", "Wycofana" }, ksiazka.stan);
            return View(ksiazka);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var ksiazka = await _context.Ksiazki
                .Include(k => k.Kategoria)
                .Select(k => new KsiazkaDetailsViewModel
                {
                    Ksiazka = k,
                    AktywneRezerwacje = _context.Rezerwacje.Where(r => r.KsiazkaId == k.Id && r.IsActive).Include(r => r.User).OrderBy(r => r.DataRezerwacji).ToList()
                })
                .FirstOrDefaultAsync(vm => vm.Ksiazka.Id == id);
            if (ksiazka == null) return NotFound();
            return View(ksiazka);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddToBasket(int id)
        {
            var ksiazka = await _context.Ksiazki.FindAsync(id);
            if (ksiazka == null || ksiazka.stan != "Dostępna")
            {
                TempData["Message"] = $"Książka jest niedostępna lub nie istnieje.";
                return RedirectToAction(nameof(Index));
            }
            var koszyk = GetBasketItems();
            if (koszyk.Contains(id))
            {
                TempData["Message"] = "Książka jest już w koszyku.";
            }
            else
            {
                koszyk.Add(id);
                SaveBasketItems(koszyk);
                TempData["Message"] = $"Dodano '{ksiazka.tytul}' do koszyka.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ViewBasket()
        {
            var koszykIds = GetBasketItems();
            var ksiazkiWKoszyku = await _context.Ksiazki.Where(k => koszykIds.Contains(k.Id)).Include(k => k.Kategoria).ToListAsync();
            ViewBag.OkresyWypozyczenia = new SelectList(new List<int> { 7, 14, 30 }.Select(d => new { Value = d, Text = $"{d} dni" }), "Value", "Text");
            return View(ksiazkiWKoszyku);
        }

        [HttpPost]
        [Authorize]
        public IActionResult RemoveFromBasket(int id)
        {
            var koszyk = GetBasketItems();
            if (koszyk.Remove(id))
            {
                SaveBasketItems(koszyk);
                var ksiazka = _context.Ksiazki.Find(id);
                TempData["Message"] = $"Usunięto '{ksiazka?.tytul ?? "książkę"}' z koszyka.";
            }
            return RedirectToAction(nameof(ViewBasket));
        }

        [HttpPost]
        [Authorize]
        public IActionResult FinalizeWypozyczenie(int okresWypozyczenia)
        {
            return RedirectToAction("FinalizeWypozyczenie", "Wypozyczenie", new { okresWypozyczenia });
        }

        [HttpPost]
        [Authorize(Roles = "user,admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reserve(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();
            var ksiazka = await _context.Ksiazki.FindAsync(id);
            if (ksiazka == null) return NotFound();
            if (ksiazka.stan != "Wypożyczona")
            {
                TempData["Message"] = $"Książka '{ksiazka.tytul}' nie może być zarezerwowana.";
                return RedirectToAction(nameof(Index));
            }
            if (await _context.Rezerwacje.AnyAsync(r => r.KsiazkaId == id && r.UserId == userId && r.IsActive))
            {
                TempData["Message"] = "Już masz aktywną rezerwację na tę książkę.";
                return RedirectToAction(nameof(Index));
            }
            var nowaRezerwacja = new Rezerwacja { UserId = userId, KsiazkaId = id, DataRezerwacji = DateTime.Now, DataWygasniecia = DateTime.Now.AddDays(3) };
            _context.Rezerwacje.Add(nowaRezerwacja);
            await _context.SaveChangesAsync();
            TempData["Message"] = $"Pomyślnie zarezerwowano '{ksiazka.tytul}'.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSearch(string? searchString, string nazwaZapisu)
        {
            if (string.IsNullOrEmpty(searchString) || string.IsNullOrEmpty(nazwaZapisu))
            {
                TempData["Message"] = "Wprowadź frazę i nazwę zapisu.";
                return RedirectToAction(nameof(Index));
            }
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();
            var nowyZapis = new HistoriaWyszukiwan { UserId = userId, Nazwa = nazwaZapisu, Zapytanie = searchString, DataZapisu = DateTime.Now };
            _context.HistoriaWyszukiwan.Add(nowyZapis);
            await _context.SaveChangesAsync();
            TempData["Message"] = $"Wyszukiwanie '{nazwaZapisu}' zostało zapisane.";
            return RedirectToAction(nameof(Index));
        }
    }
}