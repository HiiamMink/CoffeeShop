using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.Models
{
    public class HoaDon
    {
        [Key]
        public int MaHD { get; set; }
        public int? MaNV { get; set; } // Bỏ [Required]
        public int? MaKH { get; set; }
        public int? MaBan { get; set; } // Sửa int? thành int để khớp schema
        [Required]
        public DateTime ThoiGianTao { get; set; }
        [Required]
        public decimal TongTien { get; set; }
        public decimal? SoTienDaTra { get; set; }
        [Required]
        public string TrangThai { get; set; }
        public bool IsActive { get; set; } = true;
        public string? HinhThucThanhToan { get; set; }
        public DateTime? ThoiGianThanhToan { get; set; }

        // Navigation properties
        public NhanVien NhanVien { get; set; }
        public KhachHang KhachHang { get; set; }
        public Table Table { get; set; }
        public List<ChiTietHoaDon> ChiTietHoaDons { get; set; }
    }
}