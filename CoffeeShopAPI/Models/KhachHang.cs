using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.Models
{
    public class KhachHang
    {
        [Key]
        public int MaKH { get; set; }
        [Required]
        public string Username { get; set; }
        [Required]
        public string MatKhau { get; set; }
        [Required]
        public string HoTen { get; set; }
        public string? Email { get; set; }
        public string? DiaChi { get; set; }
        public string? SoDienThoai { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime NgayTao { get; set; } = DateTime.Now;
        public decimal SoDu { get; set; } = 0;

        // Navigation property
        public List<HoaDon> HoaDons { get; set; }
        public DiemTichLuy DiemTichLuy { get; set; }
        public List<GioHang> GioHangs { get; set; }
    }
}