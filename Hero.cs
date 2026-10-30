using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Hero
    {
        [Key]
        public int HeroId { get; set; }

        public string Name { get; set; } = "";

        public string Role { get; set; } = "";

        public string Description { get; set; } = "";

        public string Image { get; set; } = "";
    }
}