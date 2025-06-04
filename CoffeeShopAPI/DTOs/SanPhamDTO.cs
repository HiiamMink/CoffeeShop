using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.DTOs
{
    public class SanPhamDTO
    {
        public int MaSP { get; set; }
        [Required]
        public string TenSP { get; set; }
        public int? MaLoai { get; set; }
        public string? TenLoai { get; set; } 
        public string? DonViTinh { get; set; }
        [Required]
        public decimal GiaBan { get; set; }
        public string? MoTa { get; set; } 
        public string? HinhAnh { get; set; } 
        public bool IsActive { get; set; } 
    }
}