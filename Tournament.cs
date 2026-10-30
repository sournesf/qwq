using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Tournament
    {
        [Key]
        public int TournamentId { get; set; }

        public string Name { get; set; } = "";

        public string PrizePool { get; set; } = "";

        public int Year { get; set; }

        public string Image { get; set; } = "";
    }
}