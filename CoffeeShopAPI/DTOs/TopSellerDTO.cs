namespace CoffeeShopAPI.DTOs
{
    public class TopSellerDTO
    {
        public int MaSP { get; set; }
        public string TenSP { get; set; } 
        public int? MaLoai { get; set; } 
        public string? TenLoai { get; set; } 
        public string? DonViTinh { get; set; } 
        public decimal GiaBan { get; set; } 
        public int TotalQuantity { get; set; } // Tổng số lượng bán
        public decimal TotalRevenue { get; set; } // Tổng doanh thu
    }
}