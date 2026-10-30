using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Match
    {
        [Key]
        public int MatchId { get; set; }

        public int TournamentId { get; set; }

        public Tournament? Tournament { get; set; }

        public int Team1Id { get; set; }

        public Team? Team1 { get; set; }

        public int Team2Id { get; set; }

        public Team? Team2 { get; set; }

        public int WinnerId { get; set; }

        public Team? Winner { get; set; }

        public DateTime MatchDate { get; set; }
    }
}