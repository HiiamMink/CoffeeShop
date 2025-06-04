using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.Models
{
    public class ChiTietHoaDon
    {
        [Key]
        public int MaCTHD { get; set; }
        [Required]
        public int MaHD { get; set; }
        [Required]
        public int MaSP { get; set; }
        [Required]
        public int SoLuong { get; set; }
        [Required]
        public decimal DonGia { get; set; }
        [Required]
        public decimal ThanhTien { get; set; }
        public string? GhiChu { get; set; }
        public string? Size { get; set; } //S M L
        public string? Topping { get; set; }

        // Navigation properties
        public HoaDon HoaDon { get; set; }
        public SanPham SanPham { get; set; }
    }
}