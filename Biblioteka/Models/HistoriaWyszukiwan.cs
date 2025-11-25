using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Biblioteka.Models
{
    public class HistoriaWyszukiwan
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Nazwa { get; set; }

        [Required]
        public string Zapytanie { get; set; }

        public DateTime DataZapisu { get; set; }

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
    }
}
