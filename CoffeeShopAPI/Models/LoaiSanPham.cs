using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.Models
{
    public class LoaiSanPham
    {
        [Key]
        public int MaLoai { get; set; }
        [Required]
        public string TenLoai { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation property
        public List<SanPham> SanPhams { get; set; }
    }
}