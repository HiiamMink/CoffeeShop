using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.DTOs
{
    public class HoaDonDTO
    {
        public int MaHD { get; set; }
        public int? MaNV { get; set; } // Bỏ [Required]
        public string? HoTenNhanVien { get; set; }
        public int? MaKH { get; set; }
        public string? HoTenKhachHang { get; set; }
        public int? MaBan { get; set; }
        public string? BanSo { get; set; }
        public DateTime ThoiGianTao { get; set; }
        public decimal TongTien { get; set; }
        public decimal? SoTienDaTra { get; set; }
        public string? TrangThai { get; set; }
        public string? HinhThucThanhToan { get; set; }
        public DateTime? ThoiGianThanhToan { get; set; }
        public bool IsActive { get; set; } = true;
        public List<ChiTietHoaDonDTO>? ChiTietHoaDons { get; set; }
    }

    public class ChiTietHoaDonDTO
    {
        public int MaCTHD { get; set; }
        public int MaHD { get; set; }
        public int MaSP { get; set; }
        public string? TenSP { get; set; }
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
        public int SoLuong { get; set; }
        [Required]
        public decimal DonGia { get; set; }
        public decimal ThanhTien { get; set; }
        public string? GhiChu { get; set; }
        public string? Size { get; set; }
        public string? Topping { get; set; }
    }

    public class HoaDonCreateDTO
    {
        public int? MaKH { get; set; }
        public int MaBan { get; set; }
        public string? HinhThucThanhToan { get; set; }
        public decimal? SoTienDaTra { get; set; }
        public List<ChiTietHoaDonDTO> ChiTietHoaDons { get; set; }
    }
}
