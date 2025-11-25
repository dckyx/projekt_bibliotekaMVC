namespace Biblioteka.Models;

using System.ComponentModel.DataAnnotations;
public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}



public class User
{
    public int Id { get; set; }
    public string Imie { get; set; } = string.Empty;
    public string Nazwisko { get; set; } = string.Empty;
    public string email { get; set; } = string.Empty;
    public string haslo { get; set; } = string.Empty;
    public DateTime dataRejestracji { get; set; } = DateTime.Now;
    public int iloscWypKsiazek { get; set; } = 0;

    public string Rola { get; set; } = "user";
    [Display(Name = "Saldo Kar")]
    public decimal Kara { get; set; } = 0.00M;

    [Display(Name = "Blokada Wypożyczeń")]
    public bool IsBlocked { get; set; } = false;

}


public class Ksiazka
{
    public int Id { get; set; }

    public int isbn { get; set; }
    public string tytul { get; set; } = string.Empty;

    public string autor { get; set; } = string.Empty;

    public string tag { get; set; } = string.Empty;

    public string stan { get; set; } = string.Empty;

    [Display(Name = "Spis Treści")]
    public string? SpisTresci { get; set; }

    [Display(Name = "Kategoria")]
    public int KategoriaId { get; set; }

    public Kategoria? Kategoria { get; set; }


}