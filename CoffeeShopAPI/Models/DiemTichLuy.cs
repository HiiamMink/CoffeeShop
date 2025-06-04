using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeShopAPI.Models{
    public class DiemTichLuy
    {
        [Key]
        [ForeignKey("KhachHang")]
        public int MaKH { get; set; } // Mã khách hàng
        [Required]
        public int SoDiemTichLuy { get; set; } = 0; // Số điểm tích lũy

        // Navigation properties
        public KhachHang KhachHang { get; set; }
    }
}