using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.Models
{
    public class GioHang
    {
        [Key]
        public int MaGioHang { get; set; }
        [Required]
        public int MaKH { get; set; }
        public DateTime NgayTao { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public KhachHang KhachHang { get; set; }
        public List<GioHangItem> GioHangItems { get; set; }
    }
}