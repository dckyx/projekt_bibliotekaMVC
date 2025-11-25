using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Biblioteka.Models
{
    public class Rezerwacja
    {
        public int Id { get; set; }

        [Display(Name = "Użytkownik")]
        public int UserId { get; set; }

        [Display(Name = "Książka")]
        public int KsiazkaId { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Data Rezerwacji")]
        public DateTime DataRezerwacji { get; set; } = DateTime.Now;

        [DataType(DataType.Date)]
        [Display(Name = "Wygasa")]
        public DateTime DataWygasniecia { get; set; }

        [Display(Name = "Aktywna")]
        public bool IsActive { get; set; } = true; 

        public User? User { get; set; }
        public Ksiazka? Ksiazka { get; set; }
    }
}