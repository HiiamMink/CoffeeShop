using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.DTOs
{
    public class NhanVienDTO
    {
        public int MaNV { get; set; }
        [Required]
        public string HoTen { get; set; } // Họ tên nhân viên
        [Required]
        public string Username { get; set; } // Tên đăng nhập
        public string? MatKhau { get; set; }
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
        [Required]
        public string Role { get; set; } // Vai trò (Owner, Staff)
        public bool IsActive { get; set; }
    }

    public class NhanVienUpdateDTO
    {
        public string? HoTen { get; set; }
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
    }
}