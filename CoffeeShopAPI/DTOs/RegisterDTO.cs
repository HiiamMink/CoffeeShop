using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.DTOs
{
    public class RegisterDTO
    {
        [Required]
        public string Username { get; set; } // HoTen
        [Required]
        public string MatKhau { get; set; }
        [Required]
        public string HoTen { get; set; }
        public string? GioiTinh { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string? DiaChi { get; set; }
        public string? SoDienThoai { get; set; }
        public string? Email { get; set; } 
        public string? Role { get; set; } // Nullable cho khách hàng
        public int? DiemTichLuy { get; set; } = 0; // khách hàng, mặc định 0
        public DateTime? NgayVaoLam { get; set; } // nhân viên
    }
}