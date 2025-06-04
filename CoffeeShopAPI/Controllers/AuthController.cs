using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CoffeeShopAPI.DTOs;
using CoffeeShopAPI.Models;

namespace CoffeeShopAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly CoffeeShopContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(CoffeeShopContext context, IConfiguration configuration)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            // Kiểm tra cấu hình JWT để đảm bảo bảo mật
            if (string.IsNullOrEmpty(configuration["Jwt:Key"]) || configuration["Jwt:Key"].Length < 32)
                throw new InvalidOperationException("JWT Key không hợp lệ hoặc quá ngắn");
            if (string.IsNullOrEmpty(configuration["Jwt:Issuer"]) || string.IsNullOrEmpty(configuration["Jwt:Audience"]))
                throw new InvalidOperationException("JWT Issuer hoặc Audience không được cấu hình");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO model)
        {
            // Kiểm tra dữ liệu đầu vào
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            try
            {
                // Tìm người dùng trong bảng NhanVien hoặc KhachHang
                var nhanVien = await _context.NhanViens
                    .FirstOrDefaultAsync(n => n.Username == model.Username && n.IsActive);
                var khachHang = await _context.KhachHangs
                    .FirstOrDefaultAsync(k => k.Username == model.Username && k.IsActive);

                // Xác thực NhanVien
                if (nhanVien != null && model.MatKhau == nhanVien.MatKhau)
                {
                    var token = GenerateJwtToken(nhanVien.MaNV.ToString(), nhanVien.Username, nhanVien.Role);
                    return Ok(new LoginResponseDTO
                    {
                        Id = nhanVien.MaNV,
                        Username = nhanVien.Username,
                        Role = nhanVien.Role,
                        Token = token
                    });
                }
                // Xác thực KhachHang
                else if (khachHang != null && model.MatKhau == khachHang.MatKhau)
                {
                    var token = GenerateJwtToken(khachHang.MaKH.ToString(), khachHang.Username, "Customer");
                    return Ok(new LoginResponseDTO
                    {
                        Id = khachHang.MaKH,
                        Username = khachHang.Username,
                        Role = "Customer",
                        Token = token
                    });
                }

                return Unauthorized("Thông tin đăng nhập không đúng");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể xử lý đăng nhập", ex.Message } });
            }

        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            if (await _context.NhanViens.AsNoTracking().AnyAsync(n => n.Username == model.Username && n.IsActive) ||
                await _context.KhachHangs.AsNoTracking().AnyAsync(k => k.Username == model.Username && k.IsActive))
                return BadRequest(new { Errors = new[] { "Tên đăng nhập đã được sử dụng" } });

            if (await _context.KhachHangs.AsNoTracking().AnyAsync(k => k.Email == model.Email && k.IsActive))
                return BadRequest(new { Errors = new[] { "Email đã được sử dụng" } });

            if (model.Role != "Customer")
                return BadRequest(new { Errors = new[] { "Chỉ có thể đăng ký với vai trò Customer" } });

            try
            {
                var khachHang = new KhachHang
                {
                    Username = model.Username,
                    MatKhau = model.MatKhau,
                    HoTen = model.HoTen,
                    SoDienThoai = model.SoDienThoai,
                    Email = model.Email,
                    NgayTao = DateTime.Now,
                    IsActive = true
                };

                _context.KhachHangs.Add(khachHang);
                await _context.SaveChangesAsync();

                var diemTichLuy = new DiemTichLuy
                {
                    MaKH = khachHang.MaKH,
                    SoDiemTichLuy = 0
                };
                _context.DiemTichLuys.Add(diemTichLuy);
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Đăng ký thành công", Username = model.Username, Role = "Customer" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể đăng ký", ex.Message } });
            }
        }

        private string GenerateJwtToken(string id, string username, string role)
        {
            // Tạo claims cho JWT
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role)
            };

            // Tạo token với key, issuer, audience
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}