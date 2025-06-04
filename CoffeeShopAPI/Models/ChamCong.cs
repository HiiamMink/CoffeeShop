using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.Models
{
    public class ChamCong
    {
        [Key]
        public int MaChamCong { get; set; }
        [Required]
        public int MaNV { get; set; }
        [Required]
        public DateTime ThoiGianChamCong { get; set; } // Ghi giờ check-in/check-out
        [Required]
        public string LoaiChamCong { get; set; } // "Check-in", "Check-out"
        public string? TrangThai { get; set; } // "Đi làm", "Nghỉ phép", "Vắng"
        public string? GhiChu { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public NhanVien NhanVien { get; set; }
    }
}
