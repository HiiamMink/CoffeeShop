using CoffeeShopAPI.DTOs;
using CoffeeShopAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CoffeeShopAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class KhachHangController : ControllerBase
    {
        private readonly CoffeeShopContext _context;

        public KhachHangController(CoffeeShopContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [HttpGet]
        [Authorize(Roles = "Owner,Staff")]
        public async Task<IActionResult> GetKhachHang([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1 || pageSize < 1)
                return BadRequest(new { Errors = new[] { "Trang và kích thước trang phải lớn hơn 0" } });

            var query = _context.KhachHangs
                .AsNoTracking()
                .Where(kh => kh.IsActive);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(kh => kh.HoTen.Contains(search) || kh.Email.Contains(search));

            try
            {
                var total = await query.CountAsync();
                var khachHangs = await query
                    .OrderBy(kh => kh.HoTen)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(kh => new KhachHangDTO
                    {
                        MaKH = kh.MaKH,
                        HoTen = kh.HoTen,
                        Email = kh.Email,
                        SoDienThoai = kh.SoDienThoai,
                        DiaChi = kh.DiaChi,
                        IsActive = kh.IsActive
                    })
                    .ToListAsync();

                return Ok(new { Total = total, Data = khachHangs ?? new List<KhachHangDTO>(), Page = page, PageSize = pageSize });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải danh sách khách hàng" } });
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Owner,Staff")]
        public async Task<IActionResult> GetKhachHangById(int id)
        {
            try
            {
                var khachHang = await _context.KhachHangs
                    .AsNoTracking()
                    .Where(kh => kh.MaKH == id && kh.IsActive)
                    .Select(kh => new KhachHangDTO
                    {
                        MaKH = kh.MaKH,
                        HoTen = kh.HoTen,
                        Email = kh.Email,
                        SoDienThoai = kh.SoDienThoai,
                        DiaChi = kh.DiaChi,
                        IsActive = kh.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (khachHang == null)
                    return NotFound(new { Errors = new[] { "Không tìm thấy khách hàng" } });

                return Ok(khachHang);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải thông tin khách hàng" } });
            }
        }

        [HttpGet("profile")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(username))
                    return Unauthorized(new { Errors = new[] { "Không tìm thấy thông tin người dùng" } });

                var khachHang = await _context.KhachHangs
                    .AsNoTracking()
                    .Where(kh => kh.Username == username && kh.IsActive)
                    .Select(kh => new KhachHangDTO
                    {
                        MaKH = kh.MaKH,
                        HoTen = kh.HoTen,
                        Username = kh.Username,
                        Email = kh.Email,
                        SoDienThoai = kh.SoDienThoai,
                        DiaChi = kh.DiaChi,
                        SoDu = kh.SoDu,
                        IsActive = kh.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (khachHang == null)
                    return NotFound(new { Errors = new[] { "Không tìm thấy thông tin khách hàng" } });

                return Ok(khachHang);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải thông tin cá nhân" } });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> CreateKhachHang([FromBody] KhachHangDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            if (await _context.KhachHangs.AsNoTracking().AnyAsync(kh => kh.Email == model.Email && kh.IsActive))
                return BadRequest(new { Errors = new[] { "Email đã tồn tại" } });

            if (await _context.KhachHangs.AsNoTracking().AnyAsync(kh => kh.Username == model.Username && kh.IsActive))
                return BadRequest(new { Errors = new[] { "Tài khoản (Username) đã tồn tại" } });

            try
            {
                var khachHang = new KhachHang
                {
                    HoTen = model.HoTen,
                    Email = model.Email,
                    Username = model.Username,
                    MatKhau = model.MatKhau,
                    SoDienThoai = model.SoDienThoai,
                    DiaChi = model.DiaChi,
                    IsActive = model.IsActive,
                    NgayTao = DateTime.Now
                };

                _context.KhachHangs.Add(khachHang);
                await _context.SaveChangesAsync();

                model.MaKH = khachHang.MaKH;
                model.MatKhau = null;

                return CreatedAtAction(nameof(GetKhachHangById), new { id = khachHang.MaKH }, new { Message = "Tạo khách hàng thành công!", model });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tạo khách hàng", ex.Message } });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Customer")]
        public async Task<IActionResult> UpdateKhachHang(int id, [FromBody] KhachHangDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new
                {
                    Errors = ModelState.Values.SelectMany(v => v.Errors)
                                               .Select(e => e.ErrorMessage)
                });

            var khachHang = await _context.KhachHangs.FindAsync(id);
            if (khachHang == null)
                return NotFound(new { Errors = new[] { "Không tìm thấy khách hàng" } });

            if (User.IsInRole("Customer"))
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (khachHang.Username != username)
                    return Forbid();
            }

            if (model.Email != khachHang.Email &&
                await _context.KhachHangs.AsNoTracking()
                    .AnyAsync(kh => kh.Email == model.Email && kh.IsActive && kh.MaKH != id))
            {
                return BadRequest(new { Errors = new[] { "Email đã tồn tại" } });
            }

            try
            {
                khachHang.HoTen = model.HoTen ?? khachHang.HoTen;
                khachHang.Email = model.Email ?? khachHang.Email;
                khachHang.SoDienThoai = model.SoDienThoai ?? khachHang.SoDienThoai;
                khachHang.DiaChi = model.DiaChi ?? khachHang.DiaChi;

                if (!string.IsNullOrEmpty(model.MatKhau))
                    khachHang.MatKhau = model.MatKhau;

                if (User.IsInRole("Owner"))
                {
                    khachHang.IsActive = model.IsActive;
                }

                await _context.SaveChangesAsync();

                return Ok(new { Message = "Cập nhật khách hàng thành công!" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể cập nhật khách hàng" } });
            }
        }

        [HttpPut("{id}/password")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> UpdatePassword(int id, [FromBody] PasswordUpdateDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new
                {
                    Errors = ModelState.Values.SelectMany(v => v.Errors)
                                               .Select(e => e.ErrorMessage)
                });

            var khachHang = await _context.KhachHangs.FindAsync(id);
            if (khachHang == null)
                return NotFound(new { Errors = new[] { "Không tìm thấy khách hàng" } });

            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (khachHang.Username != username)
                return Forbid();

            if (string.IsNullOrEmpty(model.MatKhau))
                return BadRequest(new { Errors = new[] { "Mật khẩu không được để trống" } });

            try
            {
                khachHang.MatKhau = model.MatKhau;
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Đổi mật khẩu thành công!" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể đổi mật khẩu" } });
            }
        }

        [HttpPost("{id}/recharge")]
        [Authorize(Roles = "Owner,Staff,Customer")]
        public async Task<IActionResult> RechargeBalance(int id, [FromBody] RechargeDTO model)
        {
            if (model.SoTien <= 0)
                return BadRequest(new { Errors = new[] { "Số tiền phải lớn hơn 0" } });

            if (model.SoTien > 10000000)
                return BadRequest(new { Errors = new[] { "Số tiền tối đa là 10,000,000 VNĐ" } });

            var khachHang = await _context.KhachHangs.FindAsync(id);
            if (khachHang == null)
                return NotFound(new { Errors = new[] { "Khách hàng không tồn tại" } });

            if (User.IsInRole("Customer"))
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (khachHang.Username != username)
                    return Forbid();
            }

            try
            {
                khachHang.SoDu += model.SoTien;
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Nạp tiền thành công", SoDu = khachHang.SoDu });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể nạp tiền" } });
            }
        }

        [HttpGet("{id}/balance")]
        [Authorize(Roles = "Owner,Staff,Customer")]
        public async Task<IActionResult> GetBalance(int id)
        {
            var khachHang = await _context.KhachHangs.FindAsync(id);
            if (khachHang == null)
                return NotFound(new { Errors = new[] { "Khách hàng không tồn tại" } });

            if (User.IsInRole("Customer"))
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (khachHang.Username != username)
                    return Forbid();
            }

            return Ok(new { SoDu = khachHang.SoDu });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> DeleteKhachHang(int id)
        {
            var khachHang = await _context.KhachHangs.FindAsync(id);
            if (khachHang == null || !khachHang.IsActive)
                return NotFound(new { Errors = new[] { "Không tìm thấy khách hàng" } });

            if (await _context.HoaDons.AsNoTracking().AnyAsync(h => h.MaKH == id && h.IsActive && h.TrangThai == "Đang xử lý"))
                return BadRequest(new { Errors = new[] { "Không thể xóa khách hàng vì đang có hóa đơn đang xử lý" } });

            try
            {
                khachHang.IsActive = false;
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Xoá khách hàng thành công!" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể xóa khách hàng" } });
            }
        }
    }
}

namespace CoffeeShopAPI.DTOs
{
    public class PasswordUpdateDTO
    {
        public string MatKhau { get; set; }
    }
}

namespace CoffeeShopAPI.DTOs
{
    public class RechargeDTO
    {
        public decimal SoTien { get; set; }
    }
}