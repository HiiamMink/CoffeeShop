using CoffeeShopAPI.DTOs;
using CoffeeShopAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CoffeeShopAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GioHangController : ControllerBase
    {
        private readonly CoffeeShopContext _context;

        public GioHangController(CoffeeShopContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [HttpGet]
        [Authorize(Roles = "Customer,Staff")]
        public async Task<IActionResult> GetGioHang()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int maKH))
                return Unauthorized(new { Errors = new[] { "Không tìm thấy thông tin người dùng" } });

            try
            {
                var gioHang = await _context.GioHangs
                    .AsNoTracking()
                    .Where(g => g.MaKH == maKH && g.IsActive)
                    .Include(g => g.GioHangItems)
                    .ThenInclude(i => i.SanPham)
                    .Select(g => new GioHangDTO
                    {
                        MaGioHang = g.MaGioHang,
                        MaKH = g.MaKH,
                        Items = g.GioHangItems.Select(i => new GioHangItemDTO
                        {
                            MaItem = i.MaItem,
                            MaSP = i.MaSP,
                            TenSP = i.SanPham.TenSP,
                            SoLuong = i.SoLuong,
                            DonGia = i.DonGia,
                            ThanhTien = i.ThanhTien,
                            Size = i.Size,
                            Topping = i.Topping,
                            GhiChu = i.GhiChu
                        }).ToList(),
                        TongTien = g.GioHangItems.Sum(i => i.ThanhTien)
                    })
                    .FirstOrDefaultAsync();

                if (gioHang == null)
                {
                    gioHang = new GioHangDTO { MaKH = maKH, Items = new List<GioHangItemDTO>(), TongTien = 0 };
                    return Ok(gioHang);
                }

                return Ok(gioHang);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải giỏ hàng", ex.Message } });
            }
        }

        [HttpPost("add")]
        [Authorize(Roles = "Customer,Staff")]
        public async Task<IActionResult> AddItem([FromBody] AddGioHangItemDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            if (model.SoLuong <= 0)
                return BadRequest(new { Errors = new[] { "Số lượng phải lớn hơn 0" } });

            var validSizes = new[] { "S", "M", "L" };
            var validToppings = new[] { "Trân châu", "Pudding", "Thạch trái cây", "" };

            if (!validSizes.Contains(model.Size ?? ""))
                return BadRequest(new { Errors = new[] { "Kích thước không hợp lệ (S, M, L)" } });

            if (!validToppings.Contains(model.Topping ?? ""))
                return BadRequest(new { Errors = new[] { "Topping không hợp lệ" } });

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int maKH))
                return Unauthorized(new { Errors = new[] { "Không tìm thấy thông tin người dùng" } });

            var sanPham = await _context.SanPhams
                .AsNoTracking()
                .FirstOrDefaultAsync(sp => sp.MaSP == model.MaSP && sp.IsActive);
            if (sanPham == null)
                return NotFound(new { Errors = new[] { "Sản phẩm không tồn tại hoặc không hoạt động" } });

            try
            {
                var gioHang = await _context.GioHangs
                    .Include(g => g.GioHangItems)
                    .FirstOrDefaultAsync(g => g.MaKH == maKH && g.IsActive);

                if (gioHang == null)
                {
                    gioHang = new GioHang { MaKH = maKH, IsActive = true };
                    _context.GioHangs.Add(gioHang);
                    await _context.SaveChangesAsync();
                }

                var existingItem = gioHang.GioHangItems
                    .FirstOrDefault(i => i.MaSP == model.MaSP && i.Size == model.Size && i.Topping == model.Topping && i.GhiChu == model.GhiChu);
                if (existingItem != null)
                {
                    existingItem.SoLuong += model.SoLuong;
                    existingItem.ThanhTien = existingItem.SoLuong * existingItem.DonGia;
                }
                else
                {
                    var newItem = new GioHangItem
                    {
                        MaGioHang = gioHang.MaGioHang,
                        MaSP = model.MaSP,
                        SoLuong = model.SoLuong,
                        TenSP = sanPham.TenSP,
                        DonGia = sanPham.GiaBan,
                        ThanhTien = model.SoLuong * sanPham.GiaBan,
                        Size = model.Size,
                        Topping = model.Topping,
                        GhiChu = model.GhiChu
                    };
                    gioHang.GioHangItems.Add(newItem);
                }

                await _context.SaveChangesAsync();

                var updatedGioHang = await _context.GioHangs
                    .AsNoTracking()
                    .Where(g => g.MaGioHang == gioHang.MaGioHang)
                    .Include(g => g.GioHangItems)
                    .ThenInclude(i => i.SanPham)
                    .Select(g => new GioHangDTO
                    {
                        MaGioHang = g.MaGioHang,
                        MaKH = g.MaKH,
                        Items = g.GioHangItems.Select(i => new GioHangItemDTO
                        {
                            MaItem = i.MaItem,
                            MaSP = i.MaSP,
                            TenSP = i.SanPham.TenSP,
                            SoLuong = i.SoLuong,
                            DonGia = i.DonGia,
                            ThanhTien = i.ThanhTien,
                            Size = i.Size,
                            Topping = i.Topping,
                            GhiChu = i.GhiChu
                        }).ToList(),
                        TongTien = g.GioHangItems.Sum(i => i.ThanhTien)
                    })
                    .FirstAsync();

                return Ok(new { Message = "Thêm sản phẩm vào giỏ hàng thành công", Data = updatedGioHang });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể thêm sản phẩm vào giỏ hàng", ex.Message } });
            }
        }

        [HttpPut("update")]
        [Authorize(Roles = "Customer,Staff")]
        public async Task<IActionResult> UpdateItem([FromBody] UpdateGioHangItemDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            if (model.SoLuong <= 0)
                return BadRequest(new { Errors = new[] { "Số lượng phải lớn hơn 0" } });

            var validSizes = new[] { "S", "M", "L" };
            var validToppings = new[] { "Trân châu", "Pudding", "Thạch trái cây", "" };

            if (model.Size != null && !validSizes.Contains(model.Size))
                return BadRequest(new { Errors = new[] { "Kích thước không hợp lệ (S, M, L)" } });

            if (model.Topping != null && !validToppings.Contains(model.Topping))
                return BadRequest(new { Errors = new[] { "Topping không hợp lệ" } });

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int maKH))
                return Unauthorized(new { Errors = new[] { "Không tìm thấy thông tin người dùng" } });

            var gioHang = await _context.GioHangs
                .Include(g => g.GioHangItems)
                .ThenInclude(i => i.SanPham)
                .FirstOrDefaultAsync(g => g.MaKH == maKH && g.IsActive);
            if (gioHang == null)
                return NotFound(new { Errors = new[] { "Giỏ hàng không tồn tại" } });

            var item = gioHang.GioHangItems.FirstOrDefault(i => i.MaItem == model.MaItem);
            if (item == null)
                return NotFound(new { Errors = new[] { "Mặt hàng không tồn tại trong giỏ hàng" } });

            try
            {
                item.SoLuong = model.SoLuong;
                item.GhiChu = model.GhiChu;
                if (model.Size != null) item.Size = model.Size;
                if (model.Topping != null) item.Topping = model.Topping;
                item.ThanhTien = item.SoLuong * item.DonGia;

                await _context.SaveChangesAsync();

                var updatedGioHang = await _context.GioHangs
                    .AsNoTracking()
                    .Where(g => g.MaGioHang == gioHang.MaGioHang)
                    .Include(g => g.GioHangItems)
                    .ThenInclude(i => i.SanPham)
                    .Select(g => new GioHangDTO
                    {
                        MaGioHang = g.MaGioHang,
                        MaKH = g.MaKH,
                        Items = g.GioHangItems.Select(i => new GioHangItemDTO
                        {
                            MaItem = i.MaItem,
                            MaSP = i.MaSP,
                            TenSP = i.SanPham.TenSP,
                            SoLuong = i.SoLuong,
                            DonGia = i.DonGia,
                            ThanhTien = i.ThanhTien,
                            Size = i.Size,
                            Topping = i.Topping,
                            GhiChu = i.GhiChu
                        }).ToList(),
                        TongTien = g.GioHangItems.Sum(i => i.ThanhTien)
                    })
                    .FirstAsync();

                return Ok(new { Message = "Cập nhật giỏ hàng thành công", Data = updatedGioHang });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể cập nhật giỏ hàng", ex.Message } });
            }
        }

        [HttpDelete("remove/{maItem}")]
        [Authorize(Roles = "Customer,Staff")]
        public async Task<IActionResult> RemoveItem(int maItem)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int maKH))
                return Unauthorized(new { Errors = new[] { "Không tìm thấy thông tin người dùng" } });

            var gioHang = await _context.GioHangs
                .Include(g => g.GioHangItems)
                .FirstOrDefaultAsync(g => g.MaKH == maKH && g.IsActive);
            if (gioHang == null)
                return NotFound(new { Errors = new[] { "Giỏ hàng không tồn tại" } });

            var item = gioHang.GioHangItems.FirstOrDefault(i => i.MaItem == maItem);
            if (item == null)
                return NotFound(new { Errors = new[] { "Mặt hàng không tồn tại trong giỏ hàng" } });

            try
            {
                _context.GioHangItems.Remove(item);
                await _context.SaveChangesAsync();

                var updatedGioHang = await _context.GioHangs
                    .AsNoTracking()
                    .Where(g => g.MaGioHang == gioHang.MaGioHang)
                    .Include(g => g.GioHangItems)
                    .ThenInclude(i => i.SanPham)
                    .Select(g => new GioHangDTO
                    {
                        MaGioHang = g.MaGioHang,
                        MaKH = g.MaKH,
                        Items = g.GioHangItems.Select(i => new GioHangItemDTO
                        {
                            MaItem = i.MaItem,
                            MaSP = i.MaSP,
                            TenSP = i.SanPham.TenSP,
                            SoLuong = i.SoLuong,
                            DonGia = i.DonGia,
                            ThanhTien = i.ThanhTien,
                            Size = i.Size,
                            Topping = i.Topping,
                            GhiChu = i.GhiChu
                        }).ToList(),
                        TongTien = g.GioHangItems.Sum(i => i.ThanhTien)
                    })
                    .FirstOrDefaultAsync();

                return Ok(new { Message = "Xóa sản phẩm khỏi giỏ hàng thành công", Data = updatedGioHang });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể xóa sản phẩm khỏi giỏ hàng", ex.Message } });
            }
        }

        [HttpDelete("clear")]
        [Authorize(Roles = "Customer,Staff")]
        public async Task<IActionResult> ClearCart()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int maKH))
                return Unauthorized(new { Errors = new[] { "Không tìm thấy thông tin người dùng" } });

            var gioHang = await _context.GioHangs
                .Include(g => g.GioHangItems)
                .FirstOrDefaultAsync(g => g.MaKH == maKH && g.IsActive);

            if (gioHang == null)
                return NotFound(new { Errors = new[] { "Giỏ hàng không tồn tại" } });

            try
            {
                _context.GioHangItems.RemoveRange(gioHang.GioHangItems);
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Đã xóa toàn bộ sản phẩm trong giỏ hàng" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể xóa giỏ hàng", ex.Message } });
            }
        }

        [HttpGet("validate")]
        [Authorize(Roles = "Customer,Staff")]
        public async Task<IActionResult> ValidateCart()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int maKH))
                return Unauthorized(new { Errors = new[] { "Không tìm thấy thông tin người dùng" } });

            var gioHang = await _context.GioHangs
                .Include(g => g.GioHangItems)
                .ThenInclude(i => i.SanPham)
                .FirstOrDefaultAsync(g => g.MaKH == maKH && g.IsActive);
            if (gioHang == null || !gioHang.GioHangItems.Any())
                return BadRequest(new { Errors = new[] { "Giỏ hàng trống" } });

            var invalidItems = gioHang.GioHangItems
                .Where(i => i.SanPham == null || !i.SanPham.IsActive)
                .Select(i => new { i.MaItem, i.MaSP, i.TenSP })
                .ToList();

            if (invalidItems.Any())
                return BadRequest(new { Errors = new[] { "Một số sản phẩm không hợp lệ" }, InvalidItems = invalidItems });

            return Ok(new { Message = "Giỏ hàng hợp lệ" });
        }

        [HttpPost("create")]
        [Authorize(Roles = "Customer,Staff")]
        public async Task<IActionResult> CreateCart()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int maKH))
                return Unauthorized(new { Errors = new[] { "Không tìm thấy thông tin người dùng" } });

            var existingCart = await _context.GioHangs
                .FirstOrDefaultAsync(g => g.MaKH == maKH && g.IsActive);

            if (existingCart != null)
                return BadRequest(new { Errors = new[] { "Giỏ hàng đã tồn tại" } });

            try
            {
                var newCart = new GioHang
                {
                    MaKH = maKH,
                    IsActive = true,
                    NgayTao = DateTime.Now
                };

                _context.GioHangs.Add(newCart);
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Tạo giỏ hàng thành công", Data = newCart });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tạo giỏ hàng", ex.Message } });
            }
        }
    }
}