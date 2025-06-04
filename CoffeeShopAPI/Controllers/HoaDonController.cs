using CoffeeShopAPI.DTOs;
using CoffeeShopAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Transactions;

namespace CoffeeShopAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HoaDonController : ControllerBase
    {
        private readonly CoffeeShopContext _context;

        public HoaDonController(CoffeeShopContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [HttpPost("checkout")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Checkout([FromBody] HoaDonDTO model)
        {
            if (!ModelState.IsValid || model.ChiTietHoaDons == null)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Concat(new[] { "Chi tiết hóa đơn không được null" }) });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var table = await _context.Tables.FirstOrDefaultAsync(t => t.MaBan == model.MaBan && t.IsActive);
                if (table == null)
                    return BadRequest(new { Errors = new[] { "Số bàn không hợp lệ hoặc không tồn tại" } });

                if (table.TrangThai == "Đã đặt" || table.TrangThai == "Đang sử dụng")
                    return BadRequest(new { Errors = new[] { "Bàn đang được sử dụng, không thể tạo hóa đơn" } });

                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                var khachHang = await _context.KhachHangs.FirstOrDefaultAsync(kh => kh.Username == username && kh.IsActive);
                if (khachHang == null || (model.MaKH.HasValue && khachHang.MaKH != model.MaKH))
                    return Unauthorized(new { Errors = new[] { "Không có quyền thực hiện thanh toán" } });

                var gioHang = await _context.GioHangs
                    .Include(gh => gh.GioHangItems)
                    .ThenInclude(ghi => ghi.SanPham)
                    .FirstOrDefaultAsync(gh => gh.MaKH == khachHang.MaKH && gh.IsActive);

                if (gioHang == null || !gioHang.GioHangItems.Any(ghi => ghi.IsActive))
                    return BadRequest(new { Errors = new[] { "Giỏ hàng trống hoặc không tồn tại" } });

                var validPaymentMethods = new[] { "Tiền mặt", "Thẻ ngân hàng", "QR Code", "Ví điện tử" };
                if (!validPaymentMethods.Contains(model.HinhThucThanhToan))
                    return BadRequest(new { Errors = new[] { "Hình thức thanh toán không hợp lệ" } });

                var validSizes = new[] { "S", "M", "L", "" };
                var validToppings = new[] { "Trân châu", "Pudding", "Thạch trái cây", "" };
                if (gioHang.GioHangItems.Any(d => !validSizes.Contains(d.Size ?? "")))
                    return BadRequest(new { Errors = new[] { "Kích thước sản phẩm không hợp lệ (S, M, L)" } });
                if (gioHang.GioHangItems.Any(d => !validToppings.Contains(d.Topping ?? "")))
                    return BadRequest(new { Errors = new[] { "Topping không hợp lệ" } });
                if (gioHang.GioHangItems.Any(d => d.SoLuong <= 0))
                    return BadRequest(new { Errors = new[] { "Số lượng sản phẩm phải lớn hơn 0" } });

                decimal tongTien = gioHang.GioHangItems.Sum(i => i.ThanhTien);

                var hoaDon = new HoaDon
                {
                    MaKH = khachHang.MaKH,
                    MaNV = null,
                    MaBan = model.MaBan,
                    ThoiGianTao = DateTime.Now,
                    TongTien = tongTien,
                    TrangThai = "Chờ xác nhận",
                    HinhThucThanhToan = model.HinhThucThanhToan,
                    ThoiGianThanhToan = null,
                    SoTienDaTra = null,
                    IsActive = true,
                    ChiTietHoaDons = gioHang.GioHangItems.Select(i => new ChiTietHoaDon
                    {
                        MaSP = i.MaSP,
                        SoLuong = i.SoLuong,
                        DonGia = i.DonGia,
                        Size = i.Size,
                        Topping = i.Topping,
                        ThanhTien = i.ThanhTien,
                        GhiChu = i.GhiChu
                    }).ToList()
                };

                _context.GioHangItems.RemoveRange(gioHang.GioHangItems);
                _context.GioHangs.Remove(gioHang);
                _context.HoaDons.Add(hoaDon);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var hoaDonDTO = await _context.HoaDons
                    .Include(h => h.NhanVien)
                    .Include(h => h.KhachHang)
                    .Include(h => h.Table)
                    .Include(h => h.ChiTietHoaDons)
                    .ThenInclude(ct => ct.SanPham)
                    .Where(h => h.MaHD == hoaDon.MaHD)
                    .Select(h => new HoaDonDTO
                    {
                        MaHD = h.MaHD,
                        MaNV = h.MaNV,
                        HoTenNhanVien = h.NhanVien != null ? h.NhanVien.HoTen : null,
                        MaKH = h.MaKH,
                        HoTenKhachHang = h.KhachHang != null ? h.KhachHang.HoTen : null,
                        MaBan = h.MaBan,
                        BanSo = h.Table != null ? h.Table.BanSo : null,
                        TongTien = h.TongTien,
                        TrangThai = h.TrangThai,
                        ThoiGianTao = h.ThoiGianTao,
                        ThoiGianThanhToan = h.ThoiGianThanhToan,
                        HinhThucThanhToan = h.HinhThucThanhToan,
                        SoTienDaTra = h.SoTienDaTra,
                        ChiTietHoaDons = h.ChiTietHoaDons.Select(ct => new ChiTietHoaDonDTO
                        {
                            MaCTHD = ct.MaCTHD,
                            MaSP = ct.MaSP,
                            TenSP = ct.SanPham != null ? ct.SanPham.TenSP : null,
                            SoLuong = ct.SoLuong,
                            DonGia = ct.DonGia,
                            Size = ct.Size,
                            Topping = ct.Topping,
                            ThanhTien = ct.ThanhTien,
                            GhiChu = ct.GhiChu
                        }).ToList()
                    })
                    .FirstAsync();

                return Ok(new { Message = "Hóa đơn đã được tạo, chờ xác nhận", HoaDon = hoaDonDTO });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { Errors = new[] { "Không thể tạo hóa đơn", ex.Message } });
            }
        }

        [HttpPost("vanglai")]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> CreateHoaDonVangLai([FromBody] HoaDonCreateDTO model)
        {
            if (!ModelState.IsValid || model.ChiTietHoaDons == null || !model.ChiTietHoaDons.Any())
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Concat(new[] { "Danh sách chi tiết hóa đơn không được trống" }) });

            var validHinhThuc = new[] { "Tiền mặt", "Thẻ ngân hàng", "QR Code" };
            if (string.IsNullOrEmpty(model.HinhThucThanhToan) || !validHinhThuc.Contains(model.HinhThucThanhToan))
                return BadRequest(new { Errors = new[] { "Hình thức thanh toán không hợp lệ" } });

            var table = await _context.Tables.FirstOrDefaultAsync(t => t.MaBan == model.MaBan && t.IsActive);
            if (table == null)
                return BadRequest(new { Errors = new[] { "Bàn không tồn tại" } });

            if (table.TrangThai == "Đã đặt" || table.TrangThai == "Đang sử dụng")
                return BadRequest(new { Errors = new[] { "Bàn đang được sử dụng, không thể tạo hóa đơn" } });

            var validSizes = new[] { "S", "M", "L", "" };
            var validToppings = new[] { "Trân châu", "Pudding", "Thạch trái cây", "" };
            if (model.ChiTietHoaDons.Any(ct => !validSizes.Contains(ct.Size ?? "")))
                return BadRequest(new { Errors = new[] { "Kích thước sản phẩm không hợp lệ (S, M, L)" } });
            if (model.ChiTietHoaDons.Any(ct => !validToppings.Contains(ct.Topping ?? "")))
                return BadRequest(new { Errors = new[] { "Topping không hợp lệ" } });
            if (model.ChiTietHoaDons.Any(ct => ct.SoLuong <= 0))
                return BadRequest(new { Errors = new[] { "Số lượng sản phẩm phải lớn hơn 0" } });

            var sanPhams = await _context.SanPhams
                .Where(sp => model.ChiTietHoaDons.Select(ct => ct.MaSP).Contains(sp.MaSP) && sp.IsActive)
                .ToListAsync();

            if (sanPhams.Count != model.ChiTietHoaDons.Count)
                return BadRequest(new { Errors = new[] { "Một hoặc nhiều sản phẩm không tồn tại" } });

            decimal tongTien = model.ChiTietHoaDons.Sum(ct => ct.ThanhTien);
            if (model.HinhThucThanhToan == "Tiền mặt" && (!model.SoTienDaTra.HasValue || model.SoTienDaTra < tongTien))
                return BadRequest(new { Errors = new[] { "Số tiền đã trả không hợp lệ" } });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var hoaDon = new HoaDon
                {
                    MaNV = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("Không tìm thấy MaNV")),
                    MaKH = null,
                    MaBan = model.MaBan,
                    TongTien = tongTien,
                    TrangThai = "Hoàn thành",
                    ThoiGianTao = DateTime.Now,
                    ThoiGianThanhToan = DateTime.Now,
                    HinhThucThanhToan = model.HinhThucThanhToan,
                    SoTienDaTra = model.HinhThucThanhToan == "Tiền mặt" ? model.SoTienDaTra : null,
                    IsActive = true,
                    ChiTietHoaDons = model.ChiTietHoaDons.Select(ct =>
                {
                    var sanPham = sanPhams.FirstOrDefault(sp => sp.MaSP == ct.MaSP);
                    if (sanPham == null)
                        throw new InvalidOperationException($"Sản phẩm {ct.MaSP} không tồn tại");
                    if (ct.ThanhTien != ct.SoLuong * sanPham.GiaBan)
                        throw new InvalidOperationException($"Thành tiền sản phẩm {ct.MaSP} không hợp lệ");
                    return new ChiTietHoaDon
                    {
                        MaSP = ct.MaSP,
                        SoLuong = ct.SoLuong,
                        DonGia = sanPham.GiaBan,
                        Size = ct.Size,
                        Topping = ct.Topping,
                        ThanhTien = ct.ThanhTien,
                        GhiChu = ct.GhiChu
                    };
                }).ToList()
                };

                _context.HoaDons.Add(hoaDon);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = await _context.HoaDons
                    .Include(h => h.NhanVien)
                    .Include(h => h.Table)
                    .Include(h => h.ChiTietHoaDons)
                    .ThenInclude(ct => ct.SanPham)
                    .Where(h => h.MaHD == hoaDon.MaHD)
                    .Select(h => new HoaDonDTO
                    {
                        MaHD = h.MaHD,
                        MaNV = h.MaNV,
                        HoTenNhanVien = h.NhanVien.HoTen,
                        MaBan = h.MaBan,
                        BanSo = h.Table.BanSo,
                        TongTien = h.TongTien,
                        TrangThai = h.TrangThai,
                        ThoiGianTao = h.ThoiGianTao,
                        ThoiGianThanhToan = h.ThoiGianThanhToan,
                        HinhThucThanhToan = h.HinhThucThanhToan,
                        SoTienDaTra = h.SoTienDaTra,
                        ChiTietHoaDons = h.ChiTietHoaDons.Select(ct => new ChiTietHoaDonDTO
                        {
                            MaCTHD = ct.MaCTHD,
                            MaSP = ct.MaSP,
                            TenSP = ct.SanPham.TenSP,
                            SoLuong = ct.SoLuong,
                            DonGia = ct.DonGia,
                            Size = ct.Size,
                            Topping = ct.Topping,
                            ThanhTien = ct.ThanhTien,
                            GhiChu = ct.GhiChu
                        }).ToList()
                    }).FirstAsync();

                return CreatedAtAction(nameof(GetOrder), new { id = hoaDon.MaHD }, result);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { Errors = new[] { "Không thể tạo hóa đơn", ex.Message } });
            }
        }

        [HttpPost("{id}/payment")]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> ConfirmPayment(int id, [FromBody] PaymentDTO model)
        {
            var validHinhThuc = new[] { "Tiền mặt", "Thẻ ngân hàng", "QR Code", "Ví điện tử" };
            if (string.IsNullOrEmpty(model.HinhThucThanhToan) || !validHinhThuc.Contains(model.HinhThucThanhToan))
                return BadRequest(new { Errors = new[] { "Hình thức thanh toán không hợp lệ" } });

            var hoaDon = await _context.HoaDons
                .Include(h => h.KhachHang)
                .Include(h => h.NhanVien)
                .Include(h => h.Table)
                .Include(h => h.ChiTietHoaDons)
                .ThenInclude(ct => ct.SanPham)
                .FirstOrDefaultAsync(h => h.MaHD == id && h.IsActive);

            if (hoaDon == null)
                return NotFound(new { Errors = new[] { "Không tìm thấy hóa đơn" } });

            if (hoaDon.TrangThai != "Chờ xác nhận")
                return BadRequest(new { Errors = new[] { "Hóa đơn không ở trạng thái Chờ xác nhận" } });

            if (model.HinhThucThanhToan == "Tiền mặt" && (!model.SoTienDaTra.HasValue || model.SoTienDaTra < hoaDon.TongTien))
                return BadRequest(new { Errors = new[] { "Số tiền đã trả không đủ" } });

            if (model.HinhThucThanhToan == "Ví điện tử")
            {
                if (hoaDon.KhachHang == null)
                    return BadRequest(new { Errors = new[] { "Hóa đơn không có thông tin khách hàng" } });
                if (hoaDon.KhachHang.SoDu < hoaDon.TongTien)
                    return BadRequest(new { Errors = new[] { "Số dư khách hàng không đủ" } });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                hoaDon.TrangThai = "Hoàn thành";
                hoaDon.HinhThucThanhToan = model.HinhThucThanhToan;
                hoaDon.SoTienDaTra = model.HinhThucThanhToan == "Tiền mặt" ? model.SoTienDaTra : hoaDon.TongTien;
                hoaDon.ThoiGianThanhToan = DateTime.UtcNow;
                hoaDon.MaNV = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("Không tìm thấy MaNV"));

                if (model.HinhThucThanhToan == "Ví điện tử" && hoaDon.KhachHang != null)
                {
                    hoaDon.KhachHang.SoDu -= hoaDon.TongTien;
                    _context.KhachHangs.Update(hoaDon.KhachHang);
                }

                if (hoaDon.MaKH.HasValue)
                {
                    var diem = await _context.DiemTichLuys.FirstOrDefaultAsync(d => d.MaKH == hoaDon.MaKH);
                    int diemCong = (int)(hoaDon.TongTien / 1000);
                    if (diem == null)
                        _context.DiemTichLuys.Add(new DiemTichLuy { MaKH = hoaDon.MaKH.Value, SoDiemTichLuy = diemCong });
                    else
                    {
                        diem.SoDiemTichLuy += diemCong;
                        _context.DiemTichLuys.Update(diem);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = new HoaDonDTO
                {
                    MaHD = hoaDon.MaHD,
                    MaNV = hoaDon.MaNV,
                    HoTenNhanVien = hoaDon.NhanVien?.HoTen,
                    MaKH = hoaDon.MaKH,
                    HoTenKhachHang = hoaDon.KhachHang?.HoTen,
                    MaBan = hoaDon.MaBan,
                    BanSo = hoaDon.Table?.BanSo,
                    TongTien = hoaDon.TongTien,
                    TrangThai = hoaDon.TrangThai,
                    ThoiGianTao = hoaDon.ThoiGianTao,
                    ThoiGianThanhToan = hoaDon.ThoiGianThanhToan,
                    HinhThucThanhToan = hoaDon.HinhThucThanhToan,
                    SoTienDaTra = hoaDon.SoTienDaTra,
                    ChiTietHoaDons = hoaDon.ChiTietHoaDons.Select(ct => new ChiTietHoaDonDTO
                    {
                        MaCTHD = ct.MaCTHD,
                        MaSP = ct.MaSP,
                        TenSP = ct.SanPham?.TenSP,
                        SoLuong = ct.SoLuong,
                        DonGia = ct.DonGia,
                        Size = ct.Size,
                        Topping = ct.Topping,
                        ThanhTien = ct.ThanhTien,
                        GhiChu = ct.GhiChu
                    }).ToList()
                };

                return Ok(new { Message = "Thanh toán thành công", HoaDon = result });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { Errors = new[] { "Không thể xử lý thanh toán", ex.Message } });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Owner,Staff")]
        public async Task<IActionResult> GetOrders(
            [FromQuery] string? trangThai,
            [FromQuery] int? maBan,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? hoTenNhanVien,
            [FromQuery] string? hoTenKhachHang,
            [FromQuery] string? trangThaiBan,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            if (startDate.HasValue && endDate.HasValue && startDate > endDate)
                return BadRequest(new { Errors = new[] { "Thời gian bắt đầu không được lớn hơn thời gian kết thúc" } });

            var maNV = User.IsInRole("Staff") ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("Không tìm thấy MaNV")) : (int?)null;

            var query = _context.HoaDons
                .AsNoTracking()
                .Where(h => h.IsActive);

            // Chỉ lọc maNV nếu không phải Chờ xác nhận
            if (trangThai != "Chờ xác nhận" && maNV.HasValue)
            {
                query = query.Where(h => h.MaNV == maNV);
            }

            if (!string.IsNullOrEmpty(trangThai))
                query = query.Where(h => h.TrangThai == trangThai);
            if (maBan.HasValue)
                query = query.Where(h => h.MaBan == maBan);
            if (startDate.HasValue)
                query = query.Where(h => h.ThoiGianTao >= startDate);
            if (endDate.HasValue)
                query = query.Where(h => h.ThoiGianTao <= endDate);
            if (!string.IsNullOrEmpty(hoTenNhanVien))
                query = query.Where(h => h.NhanVien != null && h.NhanVien.HoTen.Contains(hoTenNhanVien));
            if (!string.IsNullOrEmpty(hoTenKhachHang))
                query = query.Where(h => h.KhachHang != null && h.KhachHang.HoTen.Contains(hoTenKhachHang));
            if (!string.IsNullOrEmpty(trangThaiBan))
                query = query.Where(h => h.Table != null && h.Table.TrangThai == trangThaiBan);

            try
            {
                var total = await query.CountAsync();
                var hoaDons = await query
                    .OrderByDescending(h => h.ThoiGianTao)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(h => new HoaDonDTO
                    {
                        MaHD = h.MaHD,
                        MaNV = h.MaNV,
                        HoTenNhanVien = h.NhanVien.HoTen,
                        MaKH = h.MaKH,
                        HoTenKhachHang = h.KhachHang.HoTen,
                        MaBan = h.MaBan,
                        BanSo = h.Table.BanSo,
                        ThoiGianTao = h.ThoiGianTao,
                        TongTien = h.TongTien,
                        TrangThai = h.TrangThai,
                        HinhThucThanhToan = h.HinhThucThanhToan,
                        ThoiGianThanhToan = h.ThoiGianThanhToan,
                        SoTienDaTra = h.SoTienDaTra,
                        ChiTietHoaDons = h.ChiTietHoaDons.Select(ct => new ChiTietHoaDonDTO
                        {
                            MaCTHD = ct.MaCTHD,
                            MaSP = ct.MaSP,
                            TenSP = ct.SanPham.TenSP,
                            SoLuong = ct.SoLuong,
                            DonGia = ct.DonGia,
                            ThanhTien = ct.ThanhTien,
                            GhiChu = ct.GhiChu,
                            Size = ct.Size,
                            Topping = ct.Topping
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Total = total,
                    Page = page,
                    PageSize = pageSize,
                    Data = hoaDons
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải danh sách hóa đơn", ex.Message } });
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Customer,Owner,Staff")]
        public async Task<IActionResult> GetOrder(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();
            var maNV = roles.Contains("Staff") ? int.Parse(userIdClaim ?? throw new InvalidOperationException("Không tìm thấy MaNV")) : (int?)null;

            var query = _context.HoaDons
                .AsNoTracking()
                .Where(h => h.MaHD == id && h.IsActive);

            if (roles.Contains("Customer") && !roles.Any(r => r == "Owner" || r == "Staff"))
            {
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var maKH))
                    return Unauthorized(new { Message = "Không thể xác định danh tính người dùng" });
                query = query.Where(h => h.MaKH == maKH);
            }
            else if (roles.Contains("Staff"))
            {
                query = query.Where(h => h.TrangThai == "Chờ xác nhận" || h.MaNV == maNV);
            }

            var hoaDon = await query
                .Select(h => new HoaDonDTO
                {
                    MaHD = h.MaHD,
                    MaNV = h.MaNV,
                    HoTenNhanVien = h.NhanVien.HoTen,
                    MaKH = h.MaKH,
                    HoTenKhachHang = h.KhachHang.HoTen,
                    MaBan = h.MaBan,
                    BanSo = h.Table.BanSo,
                    ThoiGianTao = h.ThoiGianTao,
                    TongTien = h.TongTien,
                    TrangThai = h.TrangThai,
                    HinhThucThanhToan = h.HinhThucThanhToan,
                    ThoiGianThanhToan = h.ThoiGianThanhToan,
                    SoTienDaTra = h.SoTienDaTra,
                    ChiTietHoaDons = h.ChiTietHoaDons.Select(ct => new ChiTietHoaDonDTO
                    {
                        MaCTHD = ct.MaCTHD,
                        MaSP = ct.MaSP,
                        TenSP = ct.SanPham.TenSP,
                        SoLuong = ct.SoLuong,
                        DonGia = ct.DonGia,
                        ThanhTien = ct.ThanhTien,
                        GhiChu = ct.GhiChu,
                        Size = ct.Size,
                        Topping = ct.Topping
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (hoaDon == null)
                return NotFound(new { Message = "Không tìm thấy hóa đơn hoặc bạn không có quyền xem" });

            return Ok(hoaDon);
        }

        [HttpGet("history")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetOrderHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var maKH))
                return Unauthorized(new { Message = "Không thể xác định danh tính người dùng" });

            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = _context.HoaDons
                .AsNoTracking()
                .Where(h => h.IsActive && h.MaKH == maKH);

            try
            {
                var total = await query.CountAsync();
                var hoaDons = await query
                    .OrderByDescending(h => h.ThoiGianTao)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(h => new HoaDonDTO
                    {
                        MaHD = h.MaHD,
                        MaNV = h.MaNV,
                        HoTenNhanVien = h.NhanVien.HoTen,
                        MaKH = h.MaKH,
                        HoTenKhachHang = h.KhachHang.HoTen,
                        MaBan = h.MaBan,
                        BanSo = h.Table.BanSo,
                        ThoiGianTao = h.ThoiGianTao,
                        TongTien = h.TongTien,
                        TrangThai = h.TrangThai,
                        HinhThucThanhToan = h.HinhThucThanhToan,
                        ThoiGianThanhToan = h.ThoiGianThanhToan,
                        SoTienDaTra = h.SoTienDaTra,
                        ChiTietHoaDons = h.ChiTietHoaDons.Select(ct => new ChiTietHoaDonDTO
                        {
                            MaCTHD = ct.MaCTHD,
                            MaSP = ct.MaSP,
                            TenSP = ct.SanPham.TenSP,
                            SoLuong = ct.SoLuong,
                            DonGia = ct.DonGia,
                            ThanhTien = ct.ThanhTien,
                            GhiChu = ct.GhiChu,
                            Size = ct.Size,
                            Topping = ct.Topping
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Total = total,
                    Page = page,
                    PageSize = pageSize,
                    Data = hoaDons
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải lịch sử hóa đơn", ex.Message } });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> UpdateOrder(int id, [FromBody] HoaDonDTO model)
        {
            if (!ModelState.IsValid || model.ChiTietHoaDons == null || !model.ChiTietHoaDons.Any())
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Concat(new[] { "Danh sách chi tiết hóa đơn không được trống" }) });

            var hoaDon = await _context.HoaDons
                .Include(h => h.ChiTietHoaDons)
                .FirstOrDefaultAsync(h => h.MaHD == id && h.IsActive);

            if (hoaDon == null)
                return NotFound(new { Message = "Không tìm thấy hóa đơn" });

            if (hoaDon.TrangThai == "Hoàn thành" || hoaDon.TrangThai == "Hủy")
                return BadRequest(new { Errors = new[] { "Không thể cập nhật hóa đơn đã hoàn thành hoặc đã hủy" } });

            if (!model.MaBan.HasValue || await _context.Tables.FirstOrDefaultAsync(t => t.MaBan == model.MaBan && t.IsActive) == null)
                return BadRequest(new { Errors = new[] { "Số bàn không hợp lệ hoặc không tồn tại" } });

            var productIds = model.ChiTietHoaDons.Select(d => d.MaSP).Distinct();
            var sanPhams = await _context.SanPhams
                .Where(sp => productIds.Contains(sp.MaSP) && sp.IsActive)
                .ToDictionaryAsync(sp => sp.MaSP);

            var validSizes = new[] { "S", "M", "L", "" };
            var validToppings = new[] { "Trân châu", "Pudding", "Thạch trái cây", "" };
            foreach (var detail in model.ChiTietHoaDons)
            {
                if (!sanPhams.ContainsKey(detail.MaSP))
                    return BadRequest(new { Errors = new[] { $"Sản phẩm {detail.MaSP} không hợp lệ hoặc không còn hoạt động" } });
                if (!validSizes.Contains(detail.Size ?? ""))
                    return BadRequest(new { Errors = new[] { $"Size của sản phẩm {detail.MaSP} không hợp lệ" } });
                if (!validToppings.Contains(detail.Topping ?? ""))
                    return BadRequest(new { Errors = new[] { $"Topping của sản phẩm {detail.MaSP} không hợp lệ" } });
                if (detail.SoLuong <= 0)
                    return BadRequest(new { Errors = new[] { $"Số lượng sản phẩm {detail.MaSP} phải lớn hơn 0" } });

                var giaChuan = sanPhams[detail.MaSP].GiaBan;
                if (detail.DonGia != giaChuan)
                    return BadRequest(new { Errors = new[] { $"Đơn giá của sản phẩm {detail.MaSP} không đúng" } });
                if (detail.ThanhTien != detail.SoLuong * detail.DonGia)
                    return BadRequest(new { Errors = new[] { $"Thành tiền sản phẩm {detail.MaSP} không hợp lệ" } });
            }

            var validTrangThai = new[] { "Chờ xác nhận", "Hoàn thành", "Hủy", "Draft" };
            if (!validTrangThai.Contains(model.TrangThai))
                return BadRequest(new { Errors = new[] { "Trạng thái hóa đơn không hợp lệ" } });

            var validHinhThuc = new[] { "Tiền mặt", "Thẻ ngân hàng", "QR Code" };
            if (!string.IsNullOrEmpty(model.HinhThucThanhToan) && !validHinhThuc.Contains(model.HinhThucThanhToan))
                return BadRequest(new { Errors = new[] { "Hình thức thanh toán không hợp lệ" } });

            decimal? change = null;
            if (model.HinhThucThanhToan == "Tiền mặt" && model.SoTienDaTra.HasValue)
            {
                if (model.SoTienDaTra < model.ChiTietHoaDons.Sum(d => d.ThanhTien))
                    return BadRequest(new { Errors = new[] { "Số tiền thanh toán không đủ" } });
                change = model.SoTienDaTra.Value - model.ChiTietHoaDons.Sum(d => d.ThanhTien);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                hoaDon.MaNV = model.MaNV;
                hoaDon.MaKH = model.MaKH;
                hoaDon.MaBan = model.MaBan;
                hoaDon.TrangThai = model.TrangThai;
                hoaDon.TongTien = model.ChiTietHoaDons.Sum(d => d.ThanhTien);
                hoaDon.HinhThucThanhToan = model.HinhThucThanhToan;
                hoaDon.ThoiGianThanhToan = model.TrangThai == "Hoàn thành" ? DateTime.Now : null;
                hoaDon.SoTienDaTra = model.HinhThucThanhToan == "Tiền mặt" ? model.SoTienDaTra : null;

                _context.ChiTietHoaDons.RemoveRange(hoaDon.ChiTietHoaDons);
                hoaDon.ChiTietHoaDons = model.ChiTietHoaDons.Select(d => new ChiTietHoaDon
                {
                    MaHD = hoaDon.MaHD,
                    MaSP = d.MaSP,
                    SoLuong = d.SoLuong,
                    DonGia = d.DonGia,
                    Size = d.Size,
                    Topping = d.Topping,
                    ThanhTien = d.ThanhTien,
                    GhiChu = d.GhiChu
                }).ToList();

                if (model.TrangThai == "Hoàn thành" && model.MaKH.HasValue)
                {
                    var diem = await _context.DiemTichLuys.FirstOrDefaultAsync(d => d.MaKH == model.MaKH);
                    int diemCong = (int)(hoaDon.TongTien / 1000);
                    if (diem == null)
                    {
                        _context.DiemTichLuys.Add(new DiemTichLuy { MaKH = model.MaKH.Value, SoDiemTichLuy = diemCong });
                    }
                    else
                    {
                        diem.SoDiemTichLuy += diemCong;
                        _context.DiemTichLuys.Update(diem);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var hoaDonDTO = await _context.HoaDons
                    .Include(h => h.KhachHang)
                    .Include(h => h.NhanVien)
                    .Include(h => h.ChiTietHoaDons)
                    .ThenInclude(ct => ct.SanPham)
                    .Where(h => h.MaHD == hoaDon.MaHD)
                    .Select(h => new HoaDonDTO
                    {
                        MaHD = h.MaHD,
                        MaNV = h.MaNV,
                        HoTenNhanVien = h.NhanVien.HoTen,
                        MaKH = h.MaKH,
                        HoTenKhachHang = h.KhachHang.HoTen,
                        MaBan = h.MaBan,
                        BanSo = h.Table.BanSo,
                        ThoiGianTao = h.ThoiGianTao,
                        TongTien = h.TongTien,
                        TrangThai = h.TrangThai,
                        HinhThucThanhToan = h.HinhThucThanhToan,
                        ThoiGianThanhToan = h.ThoiGianThanhToan,
                        SoTienDaTra = h.SoTienDaTra,
                        ChiTietHoaDons = h.ChiTietHoaDons.Select(ct => new ChiTietHoaDonDTO
                        {
                            MaCTHD = ct.MaCTHD,
                            MaSP = ct.MaSP,
                            TenSP = ct.SanPham.TenSP,
                            SoLuong = ct.SoLuong,
                            DonGia = ct.DonGia,
                            Size = ct.Size,
                            Topping = ct.Topping,
                            ThanhTien = ct.ThanhTien,
                            GhiChu = ct.GhiChu
                        }).ToList()
                    })
                    .FirstAsync();

                return Ok(new { Message = "Cập nhật hóa đơn thành công", HoaDon = hoaDonDTO, Change = change });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { Errors = new[] { "Không thể cập nhật hóa đơn", ex.Message } });
            }
        }

        // PUT: api/HoaDon/{id}/cancel
        [HttpPut("{id}/cancel")]
        [Authorize(Roles = "Staff,Owner")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var hoaDon = await _context.HoaDons
                .FirstOrDefaultAsync(h => h.MaHD == id && h.IsActive);

            if (hoaDon == null)
                return NotFound(new { Message = "Không tìm thấy hóa đơn" });

            if (hoaDon.TrangThai != "Draft" && hoaDon.TrangThai != "Chờ xác nhận")
                return BadRequest(new { Errors = new[] { "Chỉ có thể hủy hóa đơn ở trạng thái Draft hoặc Chờ xác nhận" } });

            if (hoaDon.TrangThai == "Hủy")
                return BadRequest(new { Errors = new[] { "Hóa đơn đã được hủy trước đó" } });

            var maNV = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("Không tìm thấy MaNV"));
            if (User.IsInRole("Staff") && hoaDon.MaNV != maNV)
                return Forbid("Bạn chỉ có thể hủy hóa đơn do mình tạo");

            try
            {
                hoaDon.TrangThai = "Hủy";
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Hóa đơn đã được hủy thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể hủy hóa đơn", ex.Message } });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var hoaDon = await _context.HoaDons
                .Include(h => h.ChiTietHoaDons)
                .FirstOrDefaultAsync(h => h.MaHD == id && h.IsActive);

            if (hoaDon == null)
                return NotFound("Không tìm thấy hóa đơn");

            if (hoaDon.TrangThai != "Hủy")
                return BadRequest(new { Errors = new[] { "Chỉ có thể xóa hóa đơn ở trạng thái Hủy" } });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                hoaDon.IsActive = false;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { Message = "Đã xóa hóa đơn thành công" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { Errors = new[] { "Không thể xóa hóa đơn", ex.Message } });
            }
        }

        // GET: api/HoaDon/pending
        [HttpGet("pending")]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> GetPendingHoaDon([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1 || pageSize < 1)
                return BadRequest(new { Errors = new[] { "Trang và kích thước trang phải lớn hơn 0" } });

            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = _context.HoaDons
                .AsNoTracking()
                .Where(h => h.IsActive && h.TrangThai == "Chờ xác nhận");

            try
            {
                var total = await query.CountAsync();
                var hoaDons = await query
                    .OrderByDescending(h => h.ThoiGianTao)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(h => new HoaDonDTO
                    {
                        MaHD = h.MaHD,
                        MaNV = h.MaNV,
                        HoTenNhanVien = h.NhanVien != null ? h.NhanVien.HoTen : null,
                        MaKH = h.MaKH,
                        HoTenKhachHang = h.KhachHang != null ? h.KhachHang.HoTen : null,
                        MaBan = h.MaBan,
                        BanSo = h.Table != null ? h.Table.BanSo : null,
                        TongTien = h.TongTien,
                        TrangThai = h.TrangThai,
                        ThoiGianTao = h.ThoiGianTao,
                        ChiTietHoaDons = h.ChiTietHoaDons.Select(ct => new ChiTietHoaDonDTO
                        {
                            MaCTHD = ct.MaCTHD,
                            MaSP = ct.MaSP,
                            TenSP = ct.SanPham != null ? ct.SanPham.TenSP : null,
                            SoLuong = ct.SoLuong,
                            DonGia = ct.DonGia,
                            Size = ct.Size,
                            Topping = ct.Topping,
                            ThanhTien = ct.ThanhTien
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new { Total = total, Page = page, PageSize = pageSize, Data = hoaDons });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tải danh sách hóa đơn" } });
            }
        }

        // POST: api/HoaDon/staff-create
        [HttpPost("staff-create")]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> CreateHoaDonForCustomer([FromBody] HoaDonCreateDTO model)
        {
            if (!ModelState.IsValid || model.ChiTietHoaDons == null || !model.ChiTietHoaDons.Any())
                return BadRequest(new { Errors = new[] { "Danh sách chi tiết hóa đơn không được trống" } });

            var validHinhThuc = new[] { "Tiền mặt", "Thẻ ngân hàng", "QR Code" };
            if (string.IsNullOrEmpty(model.HinhThucThanhToan) || !validHinhThuc.Contains(model.HinhThucThanhToan))
                return BadRequest(new { Errors = new[] { "Hình thức thanh toán không hợp lệ" } });

            var table = await _context.Tables.FirstOrDefaultAsync(t => t.MaBan == model.MaBan && t.IsActive);
            if (table == null || table.TrangThai == "Đã đặt" || table.TrangThai == "Đang sử dụng")
                return BadRequest(new { Errors = new[] { "Bàn không hợp lệ hoặc đang sử dụng" } });

            if (!model.MaKH.HasValue)
                return BadRequest(new { Errors = new[] { "Yêu cầu MaKH cho khách hàng đăng ký" } });

            var khachHang = await _context.KhachHangs.FirstOrDefaultAsync(kh => kh.MaKH == model.MaKH && kh.IsActive);
            if (khachHang == null)
                return BadRequest(new { Errors = new[] { "Khách hàng không tồn tại" } });

            var validSizes = new[] { "S", "M", "L", "" };
            var validToppings = new[] { "Trân châu", "Pudding", "Thạch trái cây", "" };
            if (model.ChiTietHoaDons.Any(ct => !validSizes.Contains(ct.Size ?? "")))
                return BadRequest(new { Errors = new[] { "Kích thước sản phẩm không hợp lệ (S, M, L)" } });
            if (model.ChiTietHoaDons.Any(ct => !validToppings.Contains(ct.Topping ?? "")))
                return BadRequest(new { Errors = new[] { "Topping không hợp lệ" } });
            if (model.ChiTietHoaDons.Any(ct => ct.SoLuong <= 0))
                return BadRequest(new { Errors = new[] { "Số lượng sản phẩm phải lớn hơn 0" } });

            var sanPhams = await _context.SanPhams
                .Where(sp => model.ChiTietHoaDons.Select(ct => ct.MaSP).Contains(sp.MaSP) && sp.IsActive)
                .ToListAsync();

            if (sanPhams.Count != model.ChiTietHoaDons.Count)
                return BadRequest(new { Errors = new[] { "Một hoặc nhiều sản phẩm không tồn tại" } });

            decimal tongTien = model.ChiTietHoaDons.Sum(ct => ct.ThanhTien);
            if (model.HinhThucThanhToan == "Tiền mặt" && (!model.SoTienDaTra.HasValue || model.SoTienDaTra < tongTien))
                return BadRequest(new { Errors = new[] { "Số tiền đã trả không đủ" } });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var hoaDon = new HoaDon
                {
                    MaNV = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("Không tìm thấy MaNV")),
                    MaKH = model.MaKH,
                    MaBan = model.MaBan,
                    TongTien = tongTien,
                    TrangThai = "Hoàn thành",
                    ThoiGianTao = DateTime.Now,
                    ThoiGianThanhToan = DateTime.Now,
                    HinhThucThanhToan = model.HinhThucThanhToan,
                    SoTienDaTra = model.HinhThucThanhToan == "Tiền mặt" ? model.SoTienDaTra : null,
                    IsActive = true,
                    ChiTietHoaDons = model.ChiTietHoaDons.Select(ct =>
                    {
                        var sanPham = sanPhams.First(sp => sp.MaSP == ct.MaSP);
                        if (ct.ThanhTien != ct.SoLuong * sanPham.GiaBan)
                            throw new InvalidOperationException($"Thành tiền sản phẩm {ct.MaSP} không hợp lệ");
                        return new ChiTietHoaDon
                        {
                            MaSP = ct.MaSP,
                            SoLuong = ct.SoLuong,
                            DonGia = sanPham.GiaBan,
                            Size = ct.Size,
                            Topping = ct.Topping,
                            ThanhTien = ct.ThanhTien,
                            GhiChu = ct.GhiChu
                        };
                    }).ToList()
                };

                // Cộng điểm tích lũy cho khách hàng
                int diemCong = (int)(tongTien / 1000);
                var diem = await _context.DiemTichLuys.FirstOrDefaultAsync(d => d.MaKH == model.MaKH);
                if (diem == null)
                    _context.DiemTichLuys.Add(new DiemTichLuy { MaKH = model.MaKH.Value, SoDiemTichLuy = diemCong });
                else
                {
                    diem.SoDiemTichLuy += diemCong;
                    _context.DiemTichLuys.Update(diem);
                }

                _context.HoaDons.Add(hoaDon);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = await _context.HoaDons
                    .Include(h => h.NhanVien)
                    .Include(h => h.KhachHang)
                    .Include(h => h.Table)
                    .Include(h => h.ChiTietHoaDons)
                    .ThenInclude(ct => ct.SanPham)
                    .Where(h => h.MaHD == hoaDon.MaHD)
                    .Select(h => new HoaDonDTO
                    {
                        MaHD = h.MaHD,
                        MaNV = h.MaNV,
                        HoTenNhanVien = h.NhanVien != null ? h.NhanVien.HoTen : null,
                        MaKH = h.MaKH,
                        HoTenKhachHang = h.KhachHang != null ? h.KhachHang.HoTen : null,
                        MaBan = h.MaBan,
                        BanSo = h.Table != null ? h.Table.BanSo : null,
                        TongTien = h.TongTien,
                        TrangThai = h.TrangThai,
                        ThoiGianTao = h.ThoiGianTao,
                        ThoiGianThanhToan = h.ThoiGianThanhToan,
                        HinhThucThanhToan = h.HinhThucThanhToan,
                        SoTienDaTra = h.SoTienDaTra,
                        ChiTietHoaDons = h.ChiTietHoaDons.Select(ct => new ChiTietHoaDonDTO
                        {
                            MaCTHD = ct.MaCTHD,
                            MaSP = ct.MaSP,
                            TenSP = ct.SanPham != null ? ct.SanPham.TenSP : null,
                            SoLuong = ct.SoLuong,
                            DonGia = ct.DonGia,
                            Size = ct.Size,
                            Topping = ct.Topping,
                            ThanhTien = ct.ThanhTien,
                            GhiChu = ct.GhiChu
                        }).ToList()
                    })
                    .FirstAsync();

                return CreatedAtAction(nameof(GetOrder), new { id = hoaDon.MaHD }, result);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { Errors = new[] { "Không thể tạo hóa đơn", ex.Message } });
            }
        }

        // POST: api/HoaDon/draft
        [HttpPost("draft")]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> CreateDraftHoaDon([FromBody] HoaDonCreateDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            var table = await _context.Tables.FirstOrDefaultAsync(t => t.MaBan == model.MaBan && t.IsActive);
            if (table == null)
                return BadRequest(new { Errors = new[] { "Bàn không tồn tại" } });

            var validSizes = new[] { "S", "M", "L", "" };
            var validToppings = new[] { "Trân châu", "Pudding", "Thạch trái cây", "" };
            if (model.ChiTietHoaDons == null || !model.ChiTietHoaDons.Any())
                return BadRequest(new { Errors = new[] { "Danh sách chi tiết hóa đơn không được trống" } });
            if (model.ChiTietHoaDons.Any(ct => !validSizes.Contains(ct.Size ?? "")))
                return BadRequest(new { Errors = new[] { "Kích thước sản phẩm không hợp lệ (S, M, L)" } });
            if (model.ChiTietHoaDons.Any(ct => !validToppings.Contains(ct.Topping ?? "")))
                return BadRequest(new { Errors = new[] { "Topping không hợp lệ" } });
            if (model.ChiTietHoaDons.Any(ct => ct.SoLuong <= 0))
                return BadRequest(new { Errors = new[] { "Số lượng sản phẩm phải lớn hơn 0" } });

            var sanPhams = await _context.SanPhams
                .Where(sp => model.ChiTietHoaDons.Select(ct => ct.MaSP).Contains(sp.MaSP) && sp.IsActive)
                .ToListAsync();

            if (sanPhams.Count != model.ChiTietHoaDons.Count)
                return BadRequest(new { Errors = new[] { "Một hoặc nhiều sản phẩm không tồn tại" } });

            try
            {
                var hoaDon = new HoaDon
                {
                    MaNV = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("Không tìm thấy MaNV")),
                    MaKH = model.MaKH,
                    MaBan = model.MaBan,
                    TongTien = model.ChiTietHoaDons.Sum(ct => ct.ThanhTien),
                    TrangThai = "Draft",
                    ThoiGianTao = DateTime.Now,
                    IsActive = true,
                    ChiTietHoaDons = model.ChiTietHoaDons.Select(ct =>
                    {
                        var sanPham = sanPhams.First(sp => sp.MaSP == ct.MaSP);
                        return new ChiTietHoaDon
                        {
                            MaSP = ct.MaSP,
                            SoLuong = ct.SoLuong,
                            DonGia = sanPham.GiaBan,
                            Size = ct.Size,
                            Topping = ct.Topping,
                            ThanhTien = ct.ThanhTien,
                            GhiChu = ct.GhiChu
                        };
                    }).ToList()
                };

                _context.HoaDons.Add(hoaDon);
                await _context.SaveChangesAsync();

                var result = await _context.HoaDons
                    .Include(h => h.NhanVien)
                    .Include(h => h.KhachHang)
                    .Include(h => h.Table)
                    .Include(h => h.ChiTietHoaDons)
                    .ThenInclude(ct => ct.SanPham)
                    .Where(h => h.MaHD == hoaDon.MaHD)
                    .Select(h => new HoaDonDTO
                    {
                        MaHD = h.MaHD,
                        MaNV = h.MaNV,
                        HoTenNhanVien = h.NhanVien != null ? h.NhanVien.HoTen : null,
                        MaKH = h.MaKH,
                        HoTenKhachHang = h.KhachHang != null ? h.KhachHang.HoTen : null,
                        MaBan = h.MaBan,
                        BanSo = h.Table != null ? h.Table.BanSo : null,
                        TongTien = h.TongTien,
                        TrangThai = h.TrangThai,
                        ThoiGianTao = h.ThoiGianTao,
                        ChiTietHoaDons = h.ChiTietHoaDons.Select(ct => new ChiTietHoaDonDTO
                        {
                            MaCTHD = ct.MaCTHD,
                            MaSP = ct.MaSP,
                            TenSP = ct.SanPham != null ? ct.SanPham.TenSP : null,
                            SoLuong = ct.SoLuong,
                            DonGia = ct.DonGia,
                            Size = ct.Size,
                            Topping = ct.Topping,
                            ThanhTien = ct.ThanhTien,
                            GhiChu = ct.GhiChu
                        }).ToList()
                    })
                    .FirstAsync();

                return CreatedAtAction(nameof(GetOrder), new { id = hoaDon.MaHD }, result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể tạo hóa đơn tạm", ex.Message } });
            }
        }

        // DELETE: api/HoaDon/draft/{id}
        [HttpDelete("draft/{id}")]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> DeleteDraftHoaDon(int id)
        {
            var hoaDon = await _context.HoaDons
                .Include(h => h.ChiTietHoaDons)
                .FirstOrDefaultAsync(h => h.MaHD == id && h.TrangThai == "Draft" && h.IsActive);

            if (hoaDon == null)
                return NotFound(new { Errors = new[] { "Không tìm thấy hóa đơn tạm" } });

            try
            {
                _context.ChiTietHoaDons.RemoveRange(hoaDon.ChiTietHoaDons);
                _context.HoaDons.Remove(hoaDon);
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Xóa hóa đơn tạm thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Errors = new[] { "Không thể xóa hóa đơn tạm", ex.Message } });
            }
        }
    }

    public class PaymentDTO
    {
        public string HinhThucThanhToan { get; set; }
        public decimal? SoTienDaTra { get; set; }
    }
}