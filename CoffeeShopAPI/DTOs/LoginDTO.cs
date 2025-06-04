using System.ComponentModel.DataAnnotations;

namespace CoffeeShopAPI.DTOs
{
    public class LoginDTO
    {
        [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
        public string Username { get; set; } // HoTen
        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        public string MatKhau { get; set; }
    }

    public class LoginResponseDTO
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Role { get; set; }
        public string Token { get; set; }
    }
}