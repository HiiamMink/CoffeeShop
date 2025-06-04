using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.DTOs
{
    public class LoaiSanPhamDTO
    {
        public int MaLoai { get; set; }
        [Required]
        public string TenLoai { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateLoaiSanPhamDTO
    {
        [Required]
        public string TenLoai { get; set; }
    }

    public class UpdateLoaiSanPhamDTO
    {
        [Required]
        public string TenLoai { get; set; }
    }
}
