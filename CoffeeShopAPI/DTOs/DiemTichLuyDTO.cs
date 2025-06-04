using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.DTOs
{
    public class DiemTichLuyDTO
    {
        public int MaKH { get; set; }
        public int SoDiemTichLuy { get; set; }
        public string? HoTenKhachHang { get; set; }
    }

    public class UpdateDiemDTO
    {
        [Required]
        public int SoDiemTichLuy { get; set; }
    }
}