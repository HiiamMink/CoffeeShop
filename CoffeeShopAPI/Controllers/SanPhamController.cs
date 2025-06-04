using CoffeeShopAPI.DTOs;
using CoffeeShopAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SanPhamController : ControllerBase
    {
        private readonly CoffeeShopContext _context;

        public SanPhamController(CoffeeShopContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [HttpGet]
        [Authorize(Roles = "Owner,Staff,Customer")]
        public async Task<IActionResult> GetSanPham([FromQuery] int? maLoai, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            // Thiết lập truy vấn sản phẩm
            if (page < 1 || pageSize < 1)
                return BadRequest(new { Errors = new[] { "Trang và kích thước trang phải lớn hơn 0" } });

            var query = _context.SanPhams
                .AsNoTracking()
                .Where(sp => sp.IsActive);

            if (maLoai.HasValue)
                query = query.Where(sp => sp.MaLoai == maLoai);
            if (!string.IsNullOrEmpty(search))
                query = query.Where(sp => sp.TenSP.Contains(search));

            query = query.Include(sp => sp.LoaiSanPham);

            try
            {
                var total = await query.CountAsync();
                var sanPhams = await query
                    .OrderBy(sp => sp.TenSP)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(sp => new SanPhamDTO
                    {
                        MaSP = sp.MaSP,
                        TenSP = sp.TenSP,
                        MaLoai = sp.MaLoai,
                        TenLoai = sp.LoaiSanPham != null ? sp.LoaiSanPham.TenLoai : null,
                        DonViTinh = sp.DonViTinh,
                        GiaBan = sp.GiaBan,
                        MoTa = sp.MoTa,
                        HinhAnh = sp.HinhAnh,
                        IsActive = sp.IsActive
                    })
                    .ToListAsync();

                return Ok(new { Total = total, Data = sanPhams ?? new List<SanPhamDTO>() });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải danh sách sản phẩm" } });
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Owner,Staff,Customer")]
        public async Task<IActionResult> GetSanPhamById(int id)
        {
            try
            {
                var sanPham = await _context.SanPhams
                    .AsNoTracking()
                    .Where(sp => sp.MaSP == id && sp.IsActive)
                    .Include(sp => sp.LoaiSanPham)
                    .Select(sp => new SanPhamDTO
                    {
                        MaSP = sp.MaSP,
                        TenSP = sp.TenSP,
                        MaLoai = sp.MaLoai,
                        TenLoai = sp.LoaiSanPham != null ? sp.LoaiSanPham.TenLoai : null,
                        DonViTinh = sp.DonViTinh,
                        GiaBan = sp.GiaBan,
                        MoTa = sp.MoTa,
                        HinhAnh = sp.HinhAnh,
                        IsActive = sp.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (sanPham == null)
                    return NotFound(new { Errors = new[] { "Không tìm thấy sản phẩm" } });

                return Ok(sanPham);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải thông tin sản phẩm" } });
            }
        }

        [HttpGet("top-sellers")]
        [Authorize(Roles = "Owner,Staff,Customer")]
        public async Task<IActionResult> GetTopSellers([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] int top = 5)
        {
            // Thiết lập giá trị mặc định và kiểm tra hợp lệ
            startDate ??= DateTime.Now.AddDays(-30);
            endDate ??= DateTime.Now;

            if (startDate > endDate)
                return BadRequest(new { Errors = new[] { "Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc" } });
            if (startDate < DateTime.Now.AddYears(-1) || endDate > DateTime.Now)
                return BadRequest(new { Errors = new[] { "Khoảng thời gian không hợp lệ (tối đa 1 năm trước)" } });
            if (top < 1 || top > 20)
                return BadRequest(new { Errors = new[] { "Giới hạn phải từ 1 đến 20" } });

            try
            {
                // Truy vấn sản phẩm bán chạy
                var topSellers = await _context.ChiTietHoaDons
                    .AsNoTracking()
                    .Include(ct => ct.SanPham)
                    .ThenInclude(sp => sp.LoaiSanPham)
                    .Include(ct => ct.HoaDon)
                    .Where(ct => ct.HoaDon.IsActive && ct.HoaDon.TrangThai == "Hoàn thành" &&
                                 ct.HoaDon.ThoiGianTao >= startDate && ct.HoaDon.ThoiGianTao <= endDate)
                    .GroupBy(ct => new
                    {
                        ct.MaSP,
                        ct.SanPham.TenSP,
                        ct.SanPham.MaLoai,
                        TenLoai = ct.SanPham.LoaiSanPham != null ? ct.SanPham.LoaiSanPham.TenLoai : null,
                        ct.SanPham.DonViTinh,
                        ct.SanPham.GiaBan
                    })
                    .Select(g => new TopSellerDTO
                    {
                        MaSP = g.Key.MaSP,
                        TenSP = g.Key.TenSP,
                        MaLoai = g.Key.MaLoai,
                        TenLoai = g.Key.TenLoai,
                        DonViTinh = g.Key.DonViTinh,
                        GiaBan = g.Key.GiaBan,
                        TotalQuantity = g.Sum(ct => ct.SoLuong),
                        TotalRevenue = g.Sum(ct => ct.ThanhTien)
                    })
                    .OrderByDescending(ts => ts.TotalQuantity)
                    .Take(top)
                    .ToListAsync();

                if (!topSellers.Any())
                    return Ok(new { Message = "Không có dữ liệu sản phẩm bán chạy trong khoảng thời gian này", Data = topSellers });

                return Ok(topSellers);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải dữ liệu sản phẩm bán chạy" } });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> CreateSanPham([FromBody] SanPhamDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            if (model.MaLoai.HasValue && !await _context.LoaiSanPhams.AnyAsync(l => l.MaLoai == model.MaLoai && l.IsActive))
                return BadRequest(new { Errors = new[] { "Danh mục không tồn tại hoặc không hoạt động" } });

            try
            {
                var sanPham = new SanPham
                {
                    TenSP = model.TenSP,
                    MaLoai = model.MaLoai,
                    DonViTinh = model.DonViTinh,
                    GiaBan = model.GiaBan,
                    MoTa = model.MoTa,
                    HinhAnh = model.HinhAnh,
                    IsActive = true
                };

                _context.SanPhams.Add(sanPham);
                await _context.SaveChangesAsync();

                model.MaSP = sanPham.MaSP;
                return CreatedAtAction(nameof(GetSanPhamById), new { id = sanPham.MaSP }, new { Message = "Sản phẩm đã được tạo thành công.", model });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tạo sản phẩm" } });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> UpdateSanPham(int id, [FromBody] SanPhamDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            var sanPham = await _context.SanPhams.FindAsync(id);
            if (sanPham == null)
                return NotFound(new { Errors = new[] { "Không tìm thấy sản phẩm" } });

            if (model.MaLoai.HasValue && !await _context.LoaiSanPhams.AnyAsync(l => l.MaLoai == model.MaLoai && l.IsActive))
                return BadRequest(new { Errors = new[] { "Danh mục không tồn tại hoặc không hoạt động" } });

            try
            {
                sanPham.TenSP = model.TenSP;
                sanPham.MaLoai = model.MaLoai;
                sanPham.DonViTinh = model.DonViTinh;
                sanPham.GiaBan = model.GiaBan;
                sanPham.MoTa = model.MoTa;
                sanPham.HinhAnh = model.HinhAnh;
                sanPham.IsActive = model.IsActive;

                await _context.SaveChangesAsync();
                return Ok(new { Message = "Cập nhật sản phẩm thành công!" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể cập nhật sản phẩm" } });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> DeleteSanPham(int id)
        {
            var sanPham = await _context.SanPhams.FindAsync(id);
            if (sanPham == null || !sanPham.IsActive)
                return NotFound(new { Errors = new[] { "Không tìm thấy sản phẩm" } });

            if (await _context.ChiTietHoaDons.AnyAsync(ct => ct.MaSP == id && ct.HoaDon.IsActive && (ct.HoaDon.TrangThai == "Đang xử lý")))
                return BadRequest(new { Errors = new[] { "Không thể xóa sản phẩm vì đang được sử dụng trong hóa đơn đang xử lý" } });

            try
            {
                sanPham.IsActive = false;
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Xoá sản phẩm thành công!" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể xóa sản phẩm" } });
            }
        }
    }
}