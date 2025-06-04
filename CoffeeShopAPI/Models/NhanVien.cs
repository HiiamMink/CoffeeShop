using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopAPI.Models
{
    public class NhanVien
    {
        [Key]
        public int MaNV { get; set; }
        [Required]
        public string Username { get; set; }
        [Required]
        public string MatKhau { get; set; }
        [Required]
        public string HoTen { get; set; }
        public string? Email { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string? GioiTinh { get; set; }
        public string? DiaChi { get; set; }
        public string? SoDienThoai { get; set; }
        public DateTime? NgayVaoLam { get; set; }
        [Required]
        public string Role { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation property
        public List<HoaDon> HoaDons { get; set; }
        public List<ChamCong> ChamCongs { get; set; }
    }
}