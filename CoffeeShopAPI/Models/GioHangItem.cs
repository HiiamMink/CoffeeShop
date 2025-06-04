using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.Models
{
    public class GioHangItem
    {
        [Key]
        public int MaItem { get; set; }
        public int MaGioHang { get; set; }
        public int MaSP { get; set; }
        public string TenSP { get; set; }
        [Range(1, int.MaxValue)]
        public int SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public decimal ThanhTien { get; set; }
        public string? Size { get; set; }
        public string? Topping { get; set; }
        public string GhiChu { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public GioHang GioHang { get; set; }
        public SanPham SanPham { get; set; }
    }
}