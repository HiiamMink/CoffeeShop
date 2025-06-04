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
    public class NhanVienController : ControllerBase
    {
        private readonly CoffeeShopContext _context;

        public NhanVienController(CoffeeShopContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [HttpGet]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> GetNhanVien([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1 || pageSize < 1)
                return BadRequest(new { Errors = new[] { "Trang và kích thước trang phải lớn hơn 0" } });

            var query = _context.NhanViens
                .AsNoTracking()
                .Where(nv => nv.IsActive);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(nv => nv.HoTen.Contains(search) || nv.Username.Contains(search));

            try
            {
                var total = await query.CountAsync();
                var nhanViens = await query
                    .OrderBy(nv => nv.HoTen)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(nv => new NhanVienDTO
                    {
                        MaNV = nv.MaNV,
                        HoTen = nv.HoTen,
                        Username = nv.Username,
                        Email = nv.Email,
                        SoDienThoai = nv.SoDienThoai,
                        Role = nv.Role,
                        IsActive = nv.IsActive
                    })
                    .ToListAsync();

                return Ok(new { Total = total, Data = nhanViens });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải danh sách nhân viên" } });
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> GetNhanVienById(int id)
        {
            try
            {
                var nhanVien = await _context.NhanViens
                    .AsNoTracking()
                    .Where(nv => nv.MaNV == id && nv.IsActive)
                    .Select(nv => new NhanVienDTO
                    {
                        MaNV = nv.MaNV,
                        HoTen = nv.HoTen,
                        Username = nv.Username,
                        Email = nv.Email,
                        SoDienThoai = nv.SoDienThoai,
                        Role = nv.Role,
                        IsActive = nv.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (nhanVien == null)
                    return NotFound(new { Errors = new[] { "Không tìm thấy nhân viên" } });

                return Ok(nhanVien);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải thông tin nhân viên" } });
            }
        }

        [HttpGet("profile")]
        [Authorize(Roles = "Owner,Staff")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(username))
                    return Unauthorized(new { Errors = new[] { "Không tìm thấy thông tin người dùng" } });

                var nhanVien = await _context.NhanViens
                    .AsNoTracking()
                    .Where(nv => nv.Username == username && nv.IsActive)
                    .Select(nv => new NhanVienDTO
                    {
                        MaNV = nv.MaNV,
                        HoTen = nv.HoTen,
                        Username = nv.Username,
                        Email = nv.Email,
                        SoDienThoai = nv.SoDienThoai,
                        Role = nv.Role,
                        IsActive = nv.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (nhanVien == null)
                    return NotFound(new { Errors = new[] { "Không tìm thấy thông tin nhân viên" } });

                return Ok(nhanVien);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải thông tin cá nhân" } });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> CreateNhanVien([FromBody] NhanVienDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            var validRoles = new[] { "Owner", "Staff" };
            if (!validRoles.Contains(model.Role))
                return BadRequest(new { Errors = new[] { "Vai trò không hợp lệ. Các vai trò hợp lệ: Owner, Staff" } });

            if (string.IsNullOrEmpty(model.MatKhau))
                return BadRequest(new { Errors = new[] { "Mật khẩu là bắt buộc khi tạo nhân viên" } });

            if (await _context.NhanViens.AsNoTracking().AnyAsync(nv => nv.Username == model.Username && nv.IsActive))
                return BadRequest(new { Errors = new[] { "Tên đăng nhập đã tồn tại" } });

            if (!string.IsNullOrEmpty(model.Email) && await _context.NhanViens.AsNoTracking().AnyAsync(nv => nv.Email == model.Email && nv.IsActive))
                return BadRequest(new { Errors = new[] { "Email đã tồn tại" } });

            try
            {
                var nhanVien = new NhanVien
                {
                    HoTen = model.HoTen,
                    Username = model.Username,
                    MatKhau = model.MatKhau,
                    Email = model.Email,
                    SoDienThoai = model.SoDienThoai,
                    Role = model.Role,
                    NgayVaoLam = DateTime.Now,
                    IsActive = true
                };

                _context.NhanViens.Add(nhanVien);
                await _context.SaveChangesAsync();

                model.MaNV = nhanVien.MaNV;
                model.MatKhau = null; // Không trả về mật khẩu
                return CreatedAtAction(nameof(GetNhanVienById), new { id = nhanVien.MaNV }, new { Message = "Tạo nhân viên thành công!", model });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tạo nhân viên" } });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> UpdateNhanVien(int id, [FromBody] NhanVienDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            var validRoles = new[] { "Owner", "Staff" };
            if (!validRoles.Contains(model.Role))
                return BadRequest(new { Errors = new[] { "Vai trò không hợp lệ. Các vai trò hợp lệ: Owner, Staff" } });

            var nhanVien = await _context.NhanViens.FindAsync(id);
            if (nhanVien == null)
                return NotFound(new { Errors = new[] { "Không tìm thấy nhân viên" } });

            // Kiểm tra trùng username với nhân viên khác (chỉ những nhân viên còn hoạt động hoặc không phải chính mình)
            if (model.Username != nhanVien.Username &&
                await _context.NhanViens.AsNoTracking().AnyAsync(nv => nv.Username == model.Username && nv.MaNV != id))
            {
                return BadRequest(new { Errors = new[] { "Tên đăng nhập đã tồn tại" } });
            }

            // Kiểm tra trùng email nếu có cập nhật
            if (!string.IsNullOrEmpty(model.Email) &&
                model.Email != nhanVien.Email &&
                await _context.NhanViens.AsNoTracking().AnyAsync(nv => nv.Email == model.Email && nv.MaNV != id))
            {
                return BadRequest(new { Errors = new[] { "Email đã tồn tại" } });
            }

            try
            {
                nhanVien.HoTen = model.HoTen;
                nhanVien.Username = model.Username;

                if (!string.IsNullOrEmpty(model.MatKhau))
                    nhanVien.MatKhau = model.MatKhau;

                nhanVien.Email = model.Email;
                nhanVien.SoDienThoai = model.SoDienThoai;
                nhanVien.Role = model.Role;
                nhanVien.IsActive = model.IsActive;

                await _context.SaveChangesAsync();
                return Ok(new { Message = "Cập nhật nhân viên thành công!" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể cập nhật nhân viên" } });
            }
        }


        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> DeleteNhanVien(int id)
        {
            var nhanVien = await _context.NhanViens.FindAsync(id);
            if (nhanVien == null || !nhanVien.IsActive)
                return NotFound(new { Errors = new[] { "Không tìm thấy nhân viên" } });

            if (await _context.HoaDons.AsNoTracking().AnyAsync(h => h.MaNV == id && h.IsActive && h.TrangThai == "Đang xử lý"))
                return BadRequest(new { Errors = new[] { "Không thể xóa nhân viên vì đang xử lý hóa đơn" } });

            try
            {
                nhanVien.IsActive = false;
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Xoá nhân viên thành công!" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể xóa nhân viên" } });
            }
        }

        // PUT: api/NhanVien/profile
        [HttpPut("profile")]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> UpdateProfile([FromBody] NhanVienUpdateDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList() });

            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            var nhanVien = await _context.NhanViens.FirstOrDefaultAsync(nv => nv.Username == username && nv.IsActive);

            if (nhanVien == null)
                return NotFound(new { Errors = new[] { "Không tìm thấy nhân viên" } });

            if (!string.IsNullOrEmpty(model.Email) && model.Email != nhanVien.Email &&
                await _context.NhanViens.AnyAsync(nv => nv.Email == model.Email && nv.IsActive && nv.MaNV != nhanVien.MaNV))
            {
                return BadRequest(new { Errors = new[] { "Email đã tồn tại" } });
            }

            try
            {
                nhanVien.HoTen = model.HoTen ?? nhanVien.HoTen;
                nhanVien.Email = model.Email ?? nhanVien.Email;
                nhanVien.SoDienThoai = model.SoDienThoai ?? nhanVien.SoDienThoai;

                await _context.SaveChangesAsync();

                var result = new NhanVienDTO
                {
                    MaNV = nhanVien.MaNV,
                    HoTen = nhanVien.HoTen,
                    Username = nhanVien.Username,
                    Email = nhanVien.Email,
                    SoDienThoai = nhanVien.SoDienThoai,
                    Role = nhanVien.Role,
                    IsActive = nhanVien.IsActive
                };

                return Ok(new { Message = "Cập nhật thông tin thành công!", Data = result });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể cập nhật thông tin" } });
            }
        }

        // PUT: api/NhanVien/password
        [HttpPut("password")]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> UpdatePassword([FromBody] PasswordUpdateDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            var nhanVien = await _context.NhanViens.FirstOrDefaultAsync(nv => nv.Username == username && nv.IsActive);

            if (nhanVien == null)
                return NotFound(new { Errors = new[] { "Không tìm thấy nhân viên" } });

            if (string.IsNullOrEmpty(model.MatKhau))
                return BadRequest(new { Errors = new[] { "Mật khẩu không được để trống" } });

            try
            {
                nhanVien.MatKhau = model.MatKhau;
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Đổi mật khẩu thành công!" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể đổi mật khẩu" } });
            }
        }
    }
}