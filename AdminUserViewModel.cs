namespace WebApplication2.Models
{
    public class AdminUserViewModel
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsAdmin { get; set; }
        public bool IsCaptain { get; set; }
    }
}