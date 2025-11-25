using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Biblioteka.Models
{
    public class Kategoria
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nazwa kategorii jest wymagana.")]
        [Display(Name = "Nazwa Kategorii")]
        public string Nazwa { get; set; } = string.Empty;

        [Display(Name = "Kategoria nadrzędna")]
        public int? ParentId { get; set; }

        public Kategoria? Parent { get; set; }

        public ICollection<Kategoria> Children { get; set; } = new List<Kategoria>();

        public ICollection<Ksiazka> Ksiazki { get; set; } = new List<Ksiazka>();
    }
}