using CoffeeShopAPI.DTOs;
using CoffeeShopAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace CoffeeShopAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiemTichLuyController : ControllerBase
    {
        private readonly CoffeeShopContext _context;

        public DiemTichLuyController(CoffeeShopContext context)
        {
            _context = context;
        }

        [HttpGet("{id}/points")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetPoints(int id)
        {
            // Lấy user ID từ token
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null || userId != id.ToString())
                return Unauthorized(new { Errors = new[] { "Bạn chỉ có thể xem điểm của mình" } });

            // Truy vấn điểm tích lũy (kèm tên khách hàng nếu có)
            var diem = await _context.DiemTichLuys
                .Where(d => d.MaKH == id)
                .Select(d => new DiemTichLuyDTO
                {
                    MaKH = d.MaKH,
                    SoDiemTichLuy = d.SoDiemTichLuy,
                    HoTenKhachHang = d.KhachHang.HoTen
                })
                .FirstOrDefaultAsync();

            // Nếu chưa có bản ghi điểm, tạo kết quả mặc định
            if (diem == null)
            {
                var hoTen = await _context.KhachHangs
                    .Where(k => k.MaKH == id)
                    .Select(k => k.HoTen)
                    .FirstOrDefaultAsync();

                return Ok(new DiemTichLuyDTO
                {
                    MaKH = id,
                    SoDiemTichLuy = 0,
                    HoTenKhachHang = hoTen
                });
            }

            return Ok(diem);
        }

        [HttpPut("{id}/points")]
        [Authorize(Roles = "Owner,Staff")]
        public async Task<IActionResult> UpdatePoints(int id, [FromBody] UpdateDiemDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            // Optional: không cho nhập điểm âm
            if (model.SoDiemTichLuy < 0)
                return BadRequest(new { Errors = new[] { "Số điểm tích lũy không thể âm" } });

            var khachHang = await _context.KhachHangs
                .Include(kh => kh.DiemTichLuy)
                .FirstOrDefaultAsync(kh => kh.MaKH == id);

            if (khachHang == null)
                return BadRequest(new { Errors = new[] { "Khách hàng không tồn tại" } });

            if (khachHang.DiemTichLuy == null)
            {
                khachHang.DiemTichLuy = new DiemTichLuy
                {
                    MaKH = id,
                    SoDiemTichLuy = model.SoDiemTichLuy
                };
            }
            else
            {
                khachHang.DiemTichLuy.SoDiemTichLuy = model.SoDiemTichLuy;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Cập nhật điểm tích luỹ thành công" });
        }
    }

}
