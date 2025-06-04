using System.ComponentModel.DataAnnotations;
using CoffeeShopAPI.DTOs;
using CoffeeShopAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TablesController : ControllerBase
    {
        private readonly CoffeeShopContext _context;

        public TablesController(CoffeeShopContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [HttpGet]
        [Authorize(Roles = "Owner,Staff")]
        public async Task<IActionResult> GetTables([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            // Thiết lập truy vấn danh sách bàn
            if (page < 1 || pageSize < 1)
                return BadRequest(new { Errors = new[] { "Trang và kích thước trang phải lớn hơn 0" } });

            var validStatuses = new[] { "Trống", "Đã đặt", "Đang chờ gọi món", "Bảo trì" };

            if (!string.IsNullOrEmpty(status) && !validStatuses.Contains(status))
                return BadRequest(new { Errors = new[] { "Trạng thái không hợp lệ. Các trạng thái hợp lệ: Trống, Đã đặt, Bảo trì" } });

            var query = _context.Tables
                .AsNoTracking()
                .Where(t => t.IsActive); // Chỉ lấy bàn đang hoạt động

            if (!string.IsNullOrEmpty(status))
                query = query.Where(t => t.TrangThai == status);

            try
            {
                var total = await query.CountAsync();
                var tables = await query
                    .OrderBy(t => t.BanSo)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new TableDTO
                    {
                        MaBan = t.MaBan,
                        BanSo = t.BanSo,
                        TrangThai = t.TrangThai,
                        SucChua = t.SucChua,
                        ViTri = t.ViTri
                    })
                    .ToListAsync();

                return Ok(new { Total = total, Data = tables ?? new List<TableDTO>() });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải danh sách bàn" } });
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Owner,Staff")]
        public async Task<IActionResult> GetTableById(int id)
        {
            // Lấy thông tin bàn
            var table = await _context.Tables
                .AsNoTracking()
                .Where(t => t.MaBan == id && t.IsActive)
                .Select(t => new TableDTO
                {
                    MaBan = t.MaBan,
                    BanSo = t.BanSo,
                    TrangThai = t.TrangThai,
                    SucChua = t.SucChua,
                    ViTri = t.ViTri
                })
                .FirstOrDefaultAsync();

            if (table == null)
                return NotFound(new { Errors = new[] { "Không tìm thấy bàn" } });

            // Kiểm tra bàn có hóa đơn đang xử lý
            var hoaDonDangXuLy = await _context.HoaDons
                .Where(h => h.MaBan == id && h.IsActive && h.TrangThai == "Đang xử lý")
                .Select(h => new
                {
                    h.MaHD,
                    h.ThoiGianTao,
                    h.TongTien
                })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                table.MaBan,
                table.BanSo,
                table.TrangThai,
                table.SucChua,
                table.ViTri,
                DangSuDung = hoaDonDangXuLy != null, // Kiểm tra nếu có hóa đơn đang xử lý
                HoaDonDangXuLy = hoaDonDangXuLy
            });
        }



        [HttpPost]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> CreateTable([FromBody] TableDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            var validStatuses = new[] { "Trống", "Đã đặt", "Đang chờ gọi món", "Bảo trì" };

            if (!validStatuses.Contains(model.TrangThai))
                return BadRequest(new { Errors = new[] { "Trạng thái không hợp lệ. Các trạng thái hợp lệ: Trống, Đã đặt, Bảo trì" } });

            if (await _context.Tables.AsNoTracking().AnyAsync(t => t.BanSo == model.BanSo && t.IsActive))
                return BadRequest(new { Errors = new[] { "Số bàn đã tồn tại" } });

            try
            {
                var table = new Table
                {
                    BanSo = model.BanSo,
                    TrangThai = model.TrangThai,
                    SucChua = model.SucChua,
                    ViTri = model.ViTri,
                    IsActive = true
                };

                _context.Tables.Add(table);
                await _context.SaveChangesAsync();

                model.MaBan = table.MaBan;
                return CreatedAtAction(nameof(GetTables), new { id = table.MaBan }, model);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tạo bàn" } });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Staff")]
        public async Task<IActionResult> UpdateTable(int id, [FromBody] TableDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            var validStatuses = new[] { "Trống", "Đã đặt", "Đang chờ gọi món", "Bảo trì" };

            if (!validStatuses.Contains(model.TrangThai))
                return BadRequest(new { Errors = new[] { "Trạng thái không hợp lệ. Các trạng thái hợp lệ: Trống, Đã đặt, Bảo trì" } });

            var table = await _context.Tables.FindAsync(id);
            if (table == null || !table.IsActive)
                return NotFound(new { Errors = new[] { "Không tìm thấy bàn" } });

            if (model.BanSo != table.BanSo && await _context.Tables.AsNoTracking().AnyAsync(t => t.BanSo == model.BanSo && t.IsActive && t.MaBan != id))
                return BadRequest(new { Errors = new[] { "Số bàn đã tồn tại" } });

            if (model.TrangThai == "Trống" && table.TrangThai == "Đã đặt" &&
                await _context.HoaDons.AsNoTracking().AnyAsync(h => h.MaBan == id && h.TrangThai == "Đang xử lý"))
                return BadRequest(new { Errors = new[] { "Không thể đặt trạng thái Trống vì bàn đang có hóa đơn đang xử lý" } });

            try
            {
                table.BanSo = model.BanSo ?? table.BanSo;
                table.TrangThai = model.TrangThai;
                table.SucChua = model.SucChua ?? table.SucChua;
                table.ViTri = model.ViTri ?? table.ViTri;

                await _context.SaveChangesAsync();
                return Ok(new { Message = "Cập nhật bàn thành công" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể cập nhật bàn" } });
            }
        }

        [HttpPatch("{id}")]
        [Authorize(Roles = "Owner,Staff")]
        public async Task<IActionResult> UpdateTableStatus(int id, [FromBody] string status)
        {
            var validStatuses = new[] { "Trống", "Đã đặt", "Đang chờ gọi món", "Bảo trì" };

            if (!validStatuses.Contains(status))
                return BadRequest(new { Errors = new[] { "Trạng thái không hợp lệ" } });

            var table = await _context.Tables.FindAsync(id);
            if (table == null || !table.IsActive)
                return NotFound(new { Errors = new[] { "Không tìm thấy bàn" } });

            // Kiểm tra nếu bàn đang "Đã đặt" có hóa đơn đang xử lý
            if (status == "Trống" && table.TrangThai == "Đã đặt" &&
                await _context.HoaDons.AsNoTracking().AnyAsync(h => h.MaBan == id && h.TrangThai == "Đang xử lý"))
            {
                return BadRequest(new { Errors = new[] { "Không thể đổi trạng thái bàn về Trống vì có hóa đơn đang xử lý" } });
            }

            table.TrangThai = status;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Cập nhật trạng thái bàn thành công" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể cập nhật trạng thái bàn" } });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> DeleteTable(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null || !table.IsActive)
                return NotFound(new { Errors = new[] { "Không tìm thấy bàn" } });

            if (await _context.HoaDons.AsNoTracking().AnyAsync(h => h.MaBan == id && h.IsActive && h.TrangThai == "Đang xử lý"))
                return BadRequest(new { Errors = new[] { "Không thể xóa bàn vì đang được sử dụng trong hóa đơn đang xử lý" } });

            try
            {
                table.IsActive = false;
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Xóa bàn thành công" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể xóa bàn" } });
            }
        }
    }
}

namespace CoffeeShopAPI.DTOs
{
    public class TableDTO
    {
        public int MaBan { get; set; }
        [Required]
        public string BanSo { get; set; }
        [Required]
        public string TrangThai { get; set; }
        public int? SucChua { get; set; }
        public string? ViTri { get; set; }
    }
}