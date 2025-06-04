using CoffeeShopAPI.DTOs;
using CoffeeShopAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoaiSanPhamController : ControllerBase
    {
        private readonly CoffeeShopContext _context;

        public LoaiSanPhamController(CoffeeShopContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Owner,Staff, Customer")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _context.LoaiSanPhams
                    .AsNoTracking()
                    .Where(l => l.IsActive)
                    .Select(l => new LoaiSanPhamDTO
                    {
                        MaLoai = l.MaLoai,
                        TenLoai = l.TenLoai,
                        IsActive = l.IsActive
                    })
                    .ToListAsync();

                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải danh sách loại sản phẩm" } });
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Owner,Staff")]
        public async Task<IActionResult> GetById(int id)
        {
            var loai = await _context.LoaiSanPhams
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.MaLoai == id && l.IsActive);

            if (loai == null)
                return NotFound(new { Errors = new[] { "Không tìm thấy loại sản phẩm" } });

            return Ok(new LoaiSanPhamDTO
            {
                MaLoai = loai.MaLoai,
                TenLoai = loai.TenLoai,
                IsActive = loai.IsActive
            });
        }

        [HttpPost]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> Create([FromBody] CreateLoaiSanPhamDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TenLoai))
                return BadRequest(new { Errors = new[] { "Tên loại không được để trống" } });

            // Kiểm tra trùng tên loại (không phân biệt hoa thường)
            bool isDuplicate = await _context.LoaiSanPhams
                .AnyAsync(l => l.IsActive && l.TenLoai.ToLower() == dto.TenLoai.Trim().ToLower());

            if (isDuplicate)
                return BadRequest(new { Errors = new[] { "Tên loại sản phẩm đã tồn tại" } });

            try
            {
                var loai = new LoaiSanPham
                {
                    TenLoai = dto.TenLoai.Trim(),
                    IsActive = true
                };

                _context.LoaiSanPhams.Add(loai);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetById), new { id = loai.MaLoai }, new { Message = "Tạo loại sản phẩm thành công", Data = loai });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tạo loại sản phẩm" } });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateLoaiSanPhamDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TenLoai))
                return BadRequest(new { Errors = new[] { "Tên loại không được để trống" } });

            var loai = await _context.LoaiSanPhams.FindAsync(id);
            if (loai == null || !loai.IsActive)
                return NotFound(new { Errors = new[] { "Không tìm thấy loại sản phẩm" } });

            // Kiểm tra trùng tên với loại khác
            bool isDuplicate = await _context.LoaiSanPhams
                .AnyAsync(l => l.IsActive && l.TenLoai.ToLower() == dto.TenLoai.Trim().ToLower() && l.MaLoai != id);

            if (isDuplicate)
                return BadRequest(new { Errors = new[] { "Tên loại sản phẩm đã tồn tại" } });

            try
            {
                loai.TenLoai = dto.TenLoai.Trim();
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Cập nhật loại sản phẩm thành công" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể cập nhật loại sản phẩm" } });
            }
        }


        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> Delete(int id)
        {
            var loai = await _context.LoaiSanPhams.FindAsync(id);
            if (loai == null || !loai.IsActive)
                return NotFound(new { Errors = new[] { "Không tìm thấy loại sản phẩm" } });

            // Kiểm tra xem còn sản phẩm nào thuộc loại này không
            if (await _context.SanPhams.AnyAsync(sp => sp.MaLoai == id && sp.IsActive))
                return BadRequest(new { Errors = new[] { "Không thể xóa vì còn sản phẩm đang sử dụng loại này" } });

            try
            {
                loai.IsActive = false;
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Xóa loại sản phẩm thành công" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể xóa loại sản phẩm" } });
            }
        }
    }
}
