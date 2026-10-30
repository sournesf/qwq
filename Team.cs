using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Team
    {
        [Key]
        public int TeamId { get; set; }

        [Required]
        public string Name { get; set; } = "";

        [Required]
        public string Country { get; set; } = "";

        public int Founded { get; set; }

        public string? Image { get; set; } = "";

        // Пользователь, который создал команду
        public string? OwnerUserId { get; set; }

        public ApplicationUser? OwnerUser { get; set; }
    }
}