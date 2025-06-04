using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.DTOs
{
    public class KhachHangDTO
    {
        public int MaKH { get; set; }
        public string HoTen { get; set; }
        public string? Username { get; set; }
        public string Email { get; set; }
        public string? MatKhau { get; set; }
        public string? SoDienThoai { get; set; }
        public string? DiaChi { get; set; }
        public decimal SoDu { get; set; }
        public bool IsActive { get; set; } = true;
    }
}