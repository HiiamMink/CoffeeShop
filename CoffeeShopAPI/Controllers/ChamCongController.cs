using CoffeeShopAPI.DTOs;
using CoffeeShopAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace CoffeeShopAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChamCongController : ControllerBase
    {
        private readonly CoffeeShopContext _context;

        public ChamCongController(CoffeeShopContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Staff")]
        public async Task<IActionResult> CreateChamCong([FromBody] CreateChamCongDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            try
            {
                var nhanVien = await _context.NhanViens
                    .AsNoTracking()
                    .FirstOrDefaultAsync(nv => nv.MaNV == model.MaNV && nv.IsActive);
                if (nhanVien == null)
                    return BadRequest(new { Errors = new[] { "Nhân viên không tồn tại hoặc không hoạt động" } });

                var validLoaiChamCong = new[] { "Check-in", "Check-out" };
                if (!validLoaiChamCong.Contains(model.LoaiChamCong))
                    return BadRequest(new { Errors = new[] { "Loại chấm công không hợp lệ (Check-in, Check-out)" } });

                var validTrangThai = new[] { "Đi làm", "Nghỉ phép", "Vắng", null };
                if (!validTrangThai.Contains(model.TrangThai))
                    return BadRequest(new { Errors = new[] { "Trạng thái chấm công không hợp lệ (Đi làm, Nghỉ phép, Vắng)" } });

                if (model.ThoiGianChamCong > DateTime.Now)
                    return BadRequest(new { Errors = new[] { "Thời gian chấm công không thể trong tương lai" } });

                if (await _context.ChamCongs.AnyAsync(cc =>
                    cc.MaNV == model.MaNV &&
                    cc.ThoiGianChamCong.Date == model.ThoiGianChamCong.Date &&
                    cc.LoaiChamCong == model.LoaiChamCong &&
                    cc.IsActive))
                {
                    return BadRequest(new { Errors = new[] { $"Đã có bản ghi {model.LoaiChamCong} cho nhân viên này trong ngày" } });
                }

                var chamCong = new ChamCong
                {
                    MaNV = model.MaNV,
                    ThoiGianChamCong = model.ThoiGianChamCong,
                    LoaiChamCong = model.LoaiChamCong,
                    TrangThai = model.TrangThai,
                    GhiChu = model.GhiChu,
                    IsActive = true
                };

                _context.ChamCongs.Add(chamCong);
                await _context.SaveChangesAsync();

                var chamCongDTO = await _context.ChamCongs
                    .AsNoTracking()
                    .Include(cc => cc.NhanVien)
                    .Where(cc => cc.MaChamCong == chamCong.MaChamCong)
                    .Select(cc => new ChamCongDTO
                    {
                        MaChamCong = cc.MaChamCong,
                        MaNV = cc.MaNV,
                        HoTenNhanVien = cc.NhanVien.HoTen,
                        ThoiGianChamCong = cc.ThoiGianChamCong,
                        LoaiChamCong = cc.LoaiChamCong,
                        TrangThai = cc.TrangThai,
                        GhiChu = cc.GhiChu,
                        IsActive = cc.IsActive
                    })
                    .FirstAsync();

                return CreatedAtAction(nameof(GetChamCong), new { id = chamCong.MaChamCong }, new { Message = "Tạo bản ghi chấm công thành công", Data = chamCongDTO });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tạo bản ghi chấm công", ex.Message } });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Owner,Staff")]
        public async Task<IActionResult> GetChamCongs(
            [FromQuery] int? maNV,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? loaiChamCong,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1 || pageSize < 1)
                return BadRequest(new { Errors = new[] { "Trang và kích thước trang phải lớn hơn 0" } });

            try
            {
                var query = _context.ChamCongs
                    .AsNoTracking()
                    .Where(cc => cc.IsActive);

                if (maNV.HasValue)
                {
                    var nhanVien = await _context.NhanViens
                        .AsNoTracking()
                        .FirstOrDefaultAsync(nv => nv.MaNV == maNV.Value && nv.IsActive);
                    if (nhanVien == null)
                        return BadRequest(new { Errors = new[] { "Nhân viên không tồn tại hoặc không hoạt động" } });
                    query = query.Where(cc => cc.MaNV == maNV);
                }
                if (startDate.HasValue)
                    query = query.Where(cc => cc.ThoiGianChamCong.Date >= startDate.Value.Date);
                if (endDate.HasValue)
                    query = query.Where(cc => cc.ThoiGianChamCong.Date <= endDate.Value.Date);
                if (!string.IsNullOrEmpty(loaiChamCong))
                {
                    var validLoaiChamCong = new[] { "Check-in", "Check-out" };
                    if (!validLoaiChamCong.Contains(loaiChamCong))
                        return BadRequest(new { Errors = new[] { "Loại chấm công không hợp lệ (Check-in, Check-out)" } });
                    query = query.Where(cc => cc.LoaiChamCong == loaiChamCong);
                }

                var total = await query.CountAsync();
                var chamCongs = await query
                    .Include(cc => cc.NhanVien)
                    .OrderByDescending(cc => cc.ThoiGianChamCong)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(cc => new ChamCongDTO
                    {
                        MaChamCong = cc.MaChamCong,
                        MaNV = cc.MaNV,
                        HoTenNhanVien = cc.NhanVien.HoTen,
                        ThoiGianChamCong = cc.ThoiGianChamCong,
                        LoaiChamCong = cc.LoaiChamCong,
                        TrangThai = cc.TrangThai,
                        GhiChu = cc.GhiChu,
                        IsActive = cc.IsActive
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Total = total,
                    Page = page,
                    PageSize = pageSize,
                    Data = chamCongs
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải danh sách chấm công", ex.Message } });
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Owner,Staff")]
        public async Task<IActionResult> GetChamCong(int id)
        {
            try
            {
                var chamCong = await _context.ChamCongs
                    .AsNoTracking()
                    .Include(cc => cc.NhanVien)
                    .Where(cc => cc.MaChamCong == id && cc.IsActive)
                    .Select(cc => new ChamCongDTO
                    {
                        MaChamCong = cc.MaChamCong,
                        MaNV = cc.MaNV,
                        HoTenNhanVien = cc.NhanVien.HoTen,
                        ThoiGianChamCong = cc.ThoiGianChamCong,
                        LoaiChamCong = cc.LoaiChamCong,
                        TrangThai = cc.TrangThai,
                        GhiChu = cc.GhiChu,
                        IsActive = cc.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (chamCong == null)
                    return NotFound(new { Errors = new[] { "Không tìm thấy bản ghi chấm công" } });

                return Ok(chamCong);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải bản ghi chấm công", ex.Message } });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> UpdateChamCong(int id, [FromBody] UpdateChamCongDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            try
            {
                var chamCong = await _context.ChamCongs.FirstOrDefaultAsync(cc => cc.MaChamCong == id && cc.IsActive);
                if (chamCong == null)
                    return NotFound(new { Errors = new[] { "Không tìm thấy bản ghi chấm công" } });

                var nhanVien = await _context.NhanViens
                    .AsNoTracking()
                    .FirstOrDefaultAsync(nv => nv.MaNV == model.MaNV && nv.IsActive);
                if (nhanVien == null)
                    return BadRequest(new { Errors = new[] { "Nhân viên không tồn tại hoặc không hoạt động" } });

                var validLoaiChamCong = new[] { "Check-in", "Check-out" };
                if (!validLoaiChamCong.Contains(model.LoaiChamCong))
                    return BadRequest(new { Errors = new[] { "Loại chấm công không hợp lệ (Check-in, Check-out)" } });

                var validTrangThai = new[] { "Đi làm", "Nghỉ phép", "Vắng", null };
                if (!validTrangThai.Contains(model.TrangThai))
                    return BadRequest(new { Errors = new[] { "Trạng thái chấm công không hợp lệ (Đi làm, Nghỉ phép, Vắng)" } });

                if (model.ThoiGianChamCong > DateTime.Now)
                    return BadRequest(new { Errors = new[] { "Thời gian chấm công không thể trong tương lai" } });

                if (await _context.ChamCongs.AnyAsync(cc =>
                    cc.MaChamCong != id &&
                    cc.MaNV == model.MaNV &&
                    cc.ThoiGianChamCong.Date == model.ThoiGianChamCong.Date &&
                    cc.LoaiChamCong == model.LoaiChamCong &&
                    cc.IsActive))
                {
                    return BadRequest(new { Errors = new[] { $"Đã có bản ghi {model.LoaiChamCong} cho nhân viên này trong ngày" } });
                }

                chamCong.MaNV = model.MaNV;
                chamCong.ThoiGianChamCong = model.ThoiGianChamCong;
                chamCong.LoaiChamCong = model.LoaiChamCong;
                chamCong.TrangThai = model.TrangThai;
                chamCong.GhiChu = model.GhiChu;

                await _context.SaveChangesAsync();
                return Ok(new { Message = "Cập nhật bản ghi chấm công thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể cập nhật bản ghi chấm công", ex.Message } });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> DeleteChamCong(int id)
        {
            try
            {
                var chamCong = await _context.ChamCongs.FirstOrDefaultAsync(cc => cc.MaChamCong == id && cc.IsActive);
                if (chamCong == null)
                    return NotFound(new { Errors = new[] { "Không tìm thấy bản ghi chấm công" } });

                chamCong.IsActive = false;
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Xóa bản ghi chấm công thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể xóa bản ghi chấm công", ex.Message } });
            }
        }
    }
}