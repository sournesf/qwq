using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Player
    {
        [Key]
        public int PlayerId { get; set; }

        public string Nickname { get; set; } = "";

        public string Position { get; set; } = "";

        public int TeamId { get; set; }

        public Team? Team { get; set; }

        public string Image { get; set; } = "";
    }
}