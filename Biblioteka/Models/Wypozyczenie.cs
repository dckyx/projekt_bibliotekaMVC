using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Biblioteka.Models
{
    public class Wypozyczenie
    {
        public int Id { get; set; }

        [Display(Name = "Użytkownik")]
        public int UserId { get; set; }

        [Display(Name = "Książka")]
        public int KsiazkaId { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Data Wypożyczenia")]
        public DateTime DataWypozyczenia { get; set; } = DateTime.Now;

        [DataType(DataType.Date)]
        [Display(Name = "Oczekiwana Data Zwrotu")]
        public DateTime OczekiwanaDataZwrotu { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Faktyczna Data Zwrotu")]
        public DateTime? FaktycznaDataZwrotu { get; set; } 

        [Display(Name = "Przedłużono")]
        public bool Przedluzono { get; set; } = false;

        public User? User { get; set; }
        public Ksiazka? Ksiazka { get; set; }

        public DateTime? DataZwrotu { get; set; }
    }
}