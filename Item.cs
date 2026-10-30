using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Item
    {
        [Key]
        public int ItemId { get; set; }

        public string Name { get; set; } = "";

        public string Type { get; set; } = "";

        public string Description { get; set; } = "";

        public string Image { get; set; } = "";
    }
}


