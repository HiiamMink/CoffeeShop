using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.DTOs
{
    public class GioHangDTO
    {
        public int MaGioHang { get; set; }
        public int MaKH { get; set; }
        public List<GioHangItemDTO> Items { get; set; }
        public decimal TongTien { get; set; }
    }

    public class GioHangItemDTO
    {
        public int MaItem { get; set; }
        public int MaSP { get; set; }
        public string TenSP { get; set; }
        public int SoLuong { get; set; }
        public string? Size { get; set; } // S, M, L
        public string? Topping { get; set; } // Trân châu, Pudding, etc.
        public decimal DonGia { get; set; }
        public decimal ThanhTien { get; set; }
        public string GhiChu { get; set; }
    }

    public class AddGioHangItemDTO
    {
        [Required]
        public int MaSP { get; set; }
        [Required]
        [Range(1, int.MaxValue)]
        public int SoLuong { get; set; }
        public string? Size { get; set; } // S, M, L
        public string? Topping { get; set; } // Trân châu, Pudding, etc.
        public string? GhiChu { get; set; }
    }

    public class UpdateGioHangItemDTO
    {
        [Required]
        public int MaItem { get; set; }
        [Required]
        [Range(1, int.MaxValue)]
        public int SoLuong { get; set; }
        public string? Size { get; set; } // S, M, L
        public string? Topping { get; set; } // Trân châu, Pudding, etc.
        public string GhiChu { get; set; }
    }
}