using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.Models
{
    public class Table
    {
        [Key]
        public int MaBan { get; set; }
        [Required]
        public string BanSo { get; set; }
        [Required]
        public string TrangThai { get; set; }
        public int? SucChua { get; set; }
        public string? ViTri { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation property
        public List<HoaDon> HoaDons { get; set; }
    }
}