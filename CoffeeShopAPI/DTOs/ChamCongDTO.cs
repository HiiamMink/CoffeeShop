using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.DTOs
{
    public class ChamCongDTO
    {
        public int MaChamCong { get; set; }
        public int MaNV { get; set; }
        public string? HoTenNhanVien { get; set; }
        public DateTime ThoiGianChamCong { get; set; }
        public string LoaiChamCong { get; set; }
        public string? TrangThai { get; set; }
        public string? GhiChu { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateChamCongDTO
    {
        [Required]
        public int MaNV { get; set; }
        [Required]
        public DateTime ThoiGianChamCong { get; set; }
        [Required]
        public string LoaiChamCong { get; set; }
        public string? TrangThai { get; set; }
        public string? GhiChu { get; set; }
    }

    public class UpdateChamCongDTO
    {
        [Required]
        public int MaNV { get; set; }
        [Required]
        public DateTime ThoiGianChamCong { get; set; }
        [Required]
        public string LoaiChamCong { get; set; }
        public string? TrangThai { get; set; }
        public string? GhiChu { get; set; }
    }
}