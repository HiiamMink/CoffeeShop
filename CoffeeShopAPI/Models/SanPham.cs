using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.Models
{
    public class SanPham
    {
        [Key]
        public int MaSP { get; set; }
        [Required]
        public string TenSP { get; set; }
        public int? MaLoai { get; set; }
        public string? DonViTinh { get; set; }
        [Required]
        public decimal GiaBan { get; set; }
        public string? MoTa { get; set; }
        public string? HinhAnh { get; set; }
        public bool IsActive { get; set; }

        // Navigation properties
        public List<ChiTietHoaDon> ChiTietHoaDons { get; set; }
        public LoaiSanPham? LoaiSanPham { get; set; }
        public List<GioHangItem> GioHangItems { get; set; }
    }
}