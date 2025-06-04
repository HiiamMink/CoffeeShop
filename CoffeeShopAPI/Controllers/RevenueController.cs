using CoffeeShopAPI.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using CsvHelper;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;

namespace CoffeeShopAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Owner")]
    public class RevenueController : ControllerBase
    {
        private readonly CoffeeShopContext _context; private readonly ILogger _logger;

        public RevenueController(CoffeeShopContext context, ILogger<RevenueController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("dashboard")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> GetDashboard([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            startDate = startDate.HasValue
                ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Local).ToUniversalTime()
                : DateTime.UtcNow.AddDays(-30);

            endDate = endDate.HasValue
                ? DateTime.SpecifyKind(endDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime()
                : DateTime.UtcNow;


            _logger.LogInformation("Received dashboard request: startDate={StartDate}, endDate={EndDate}, QueryString={Query}",
                startDate, endDate, Request.QueryString);

            if (startDate > endDate)
            {
                _logger.LogWarning("Invalid date range: startDate {StartDate} > endDate {EndDate}", startDate, endDate);
                return BadRequest(new { Errors = new[] { "Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc" } });
            }

            try
            {
                var query = _context.HoaDons
                    .AsNoTracking()
                    .Where(h => h.ThoiGianTao >= startDate && h.ThoiGianTao <= endDate && h.TrangThai == "Hoàn thành");

                var dashboard = await query
                    .GroupBy(h => 1)
                    .Select(g => new DashboardDTO
                    {
                        TotalRevenue = g.Sum(h => h.TongTien),
                        TotalOrders = g.Count(),
                        TotalCustomers = g.Where(h => h.MaKH != null).Select(h => h.MaKH).Distinct().Count()
                    })
                    .FirstOrDefaultAsync();

                _logger.LogInformation("Dashboard data retrieved: Revenue={Revenue}, Orders={Orders}, Customers={Customers}",
                    dashboard?.TotalRevenue, dashboard?.TotalOrders, dashboard?.TotalCustomers);
                return Ok(dashboard ?? new DashboardDTO());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard data from {StartDate} to {EndDate}", startDate, endDate);
                return StatusCode(500, new { Errors = new[] { $"Không thể tạo dashboard: {ex.Message}" } });
            }
        }


        [HttpGet]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> GetRevenue([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string groupBy = "day", [FromQuery] bool comparePrevious = false)
        {
            startDate = startDate.HasValue
                ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Local).ToUniversalTime()
                : DateTime.UtcNow.AddDays(-30);

            endDate = endDate.HasValue
                ? DateTime.SpecifyKind(endDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime()
                : DateTime.UtcNow;

            _logger.LogInformation("Received revenue request: startDate={StartDate}, endDate={EndDate}, groupBy={GroupBy}, Query={Query}",
                startDate, endDate, groupBy, Request.QueryString);

            if (startDate > endDate)
            {
                _logger.LogWarning("Invalid date range: startDate {StartDate} > endDate {EndDate}", startDate, endDate);
                return BadRequest(new { Errors = new[] { "Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc" } });
            }

            var validGroupBy = new[] { "day", "month" };
            if (!validGroupBy.Contains(groupBy.ToLower()))
            {
                _logger.LogWarning("Invalid groupBy: {GroupBy}", groupBy);
                return BadRequest(new { Errors = new[] { "GroupBy phải là 'day' hoặc 'month'" } });
            }

            try
            {
                var query = _context.HoaDons
                    .AsNoTracking()
                    .Where(h => h.ThoiGianTao >= startDate && h.ThoiGianTao <= endDate && h.TrangThai == "Hoàn thành");

                List<RevenueDTO> revenue;
                decimal previousRevenue = 0;

                switch (groupBy.ToLower())
                {
                    case "month":
                        revenue = (await query
                            .GroupBy(h => new { Year = h.ThoiGianTao.Year, Month = h.ThoiGianTao.Month })
                            .Select(g => new
                            {
                                Year = g.Key.Year,
                                Month = g.Key.Month,
                                TotalRevenue = g.Sum(h => h.TongTien)
                            })
                            .ToListAsync())
                            .Select(g => new RevenueDTO
                            {
                                Date = new DateTime(g.Year, g.Month, 1),
                                TotalRevenue = g.TotalRevenue
                            })
                            .OrderBy(r => r.Date)
                            .ToList();
                        if (comparePrevious)
                        {
                            var numberOfMonths = ((endDate.Value.Year - startDate.Value.Year) * 12 + endDate.Value.Month - startDate.Value.Month) + 1;
                            var previousStart = startDate.Value.AddMonths(-numberOfMonths);
                            var previousEnd = startDate.Value.AddDays(-1);
                            previousRevenue = await _context.HoaDons
                                .Where(h => h.ThoiGianTao >= previousStart && h.ThoiGianTao <= previousEnd && h.TrangThai == "Hoàn thành")
                                .SumAsync(h => h.TongTien);
                        }
                        break;
                    default: // day
                        revenue = await query
                            .GroupBy(h => h.ThoiGianTao.Date)
                            .Select(g => new RevenueDTO
                            {
                                Date = g.Key,
                                TotalRevenue = g.Sum(h => h.TongTien)
                            })
                            .OrderBy(r => r.Date)
                            .ToListAsync();
                        if (comparePrevious)
                        {
                            var numberOfDays = (endDate.Value.Date - startDate.Value.Date).Days + 1;
                            var previousStart = startDate.Value.AddDays(-numberOfDays);
                            var previousEnd = startDate.Value.AddDays(-1);
                            previousRevenue = await _context.HoaDons
                                .Where(h => h.ThoiGianTao >= previousStart && h.ThoiGianTao <= previousEnd && h.TrangThai == "Hoàn thành")
                                .SumAsync(h => h.TongTien);
                        }
                        break;
                }

                var totalRevenue = revenue.Sum(r => r.TotalRevenue);
                var growthPercentage = previousRevenue > 0 ? (totalRevenue - previousRevenue) / previousRevenue * 100 : 0;

                if (!revenue.Any())
                {
                    _logger.LogInformation("No revenue data found for {StartDate} to {EndDate}", startDate, endDate);
                    return Ok(new { Message = "Không có dữ liệu doanh thu trong khoảng thời gian này", Data = revenue, GrowthPercentage = 0 });
                }

                _logger.LogInformation("Revenue data retrieved: Total={Total}, Growth=%{Growth}", totalRevenue, growthPercentage);
                return Ok(new { Data = revenue, TotalRevenue = totalRevenue, GrowthPercentage = growthPercentage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching revenue data from {StartDate} to {EndDate}", startDate, endDate);
                return StatusCode(500, new { Errors = new[] { $"Không thể tải dữ liệu doanh thu: {ex.Message}" } });
            }
        }

        [HttpGet("byProduct")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> GetRevenueByProduct([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            startDate = startDate.HasValue
                ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Local).ToUniversalTime()
                : DateTime.UtcNow.AddDays(-30);

            endDate = endDate.HasValue
                ? DateTime.SpecifyKind(endDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime()
                : DateTime.UtcNow;

            _logger.LogInformation("Received byProduct request: startDate={StartDate}, endDate={EndDate}, Query={Query}",
                startDate, endDate, Request.QueryString);

            if (startDate > endDate)
            {
                _logger.LogWarning("Invalid date range byProduct: startDate {StartDate} > endDate {EndDate}", startDate, endDate);
                return BadRequest(new { Errors = new[] { "Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc" } });
            }

            try
            {
                var totalRevenue = await _context.HoaDons
                    .Where(h => h.ThoiGianTao >= startDate && h.ThoiGianTao <= endDate && h.TrangThai == "Hoàn thành")
                    .SumAsync(h => h.TongTien);

                var products = await _context.ChiTietHoaDons
                    .AsNoTracking()
                    .Where(ct => ct.HoaDon.ThoiGianTao >= startDate && ct.HoaDon.ThoiGianTao <= endDate && ct.HoaDon.TrangThai == "Hoàn thành")
                    .Include(ct => ct.SanPham)
                    .GroupBy(ct => new { ct.MaSP, ct.SanPham.TenSP })
                    .Select(g => new ProductRevenueDTO
                    {
                        MaSP = g.Key.MaSP,
                        TenSP = g.Key.TenSP,
                        Quantity = g.Sum(ct => ct.SoLuong),
                        Revenue = g.Sum(ct => ct.ThanhTien),
                        Percentage = totalRevenue > 0 ? g.Sum(ct => ct.ThanhTien) / totalRevenue * 100 : 0
                    })
                    .OrderByDescending(p => p.Revenue)
                    .ToListAsync();

                if (!products.Any())
                {
                    _logger.LogInformation("No product revenue data found for {StartDate} to {EndDate}", startDate, endDate);
                    return Ok(new { Message = "Không có dữ liệu sản phẩm trong khoảng thời gian này", Data = products });
                }

                _logger.LogInformation("Product revenue retrieved: Total={Total}", totalRevenue);
                return Ok(new { TotalRevenue = totalRevenue, Data = products });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching product revenue from {StartDate} to {EndDate}", startDate, endDate);
                return StatusCode(500, new { Errors = new[] { $"Không thể tải dữ liệu doanh thu theo sản phẩm: {ex.Message}" } });
            }
        }

        [HttpGet("byCustomer")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> GetRevenueByCustomer([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            startDate = startDate.HasValue
                ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Local).ToUniversalTime()
                : DateTime.UtcNow.AddDays(-30);

            endDate = endDate.HasValue
                ? DateTime.SpecifyKind(endDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime()
                : DateTime.UtcNow;

            _logger.LogInformation("Received byCustomer request: startDate={StartDate}, endDate={EndDate}, Query={Query}",
                startDate, endDate, Request.QueryString);

            if (startDate > endDate)
            {
                _logger.LogWarning("Invalid date range byCustomer: startDate {StartDate} > endDate {EndDate}", startDate, endDate);
                return BadRequest(new { Errors = new[] { "Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc" } });
            }

            try
            {
                var totalRevenue = (await _context.HoaDons
                    .Where(h => h.ThoiGianTao >= startDate && h.ThoiGianTao <= endDate && h.TrangThai == "Hoàn thành")
                    .SumAsync(h => (decimal?)h.TongTien)).GetValueOrDefault();

                var customerRevenue = await _context.HoaDons
                    .AsNoTracking()
                    .Where(h => h.ThoiGianTao >= startDate && h.ThoiGianTao <= endDate && h.TrangThai == "Hoàn thành")
                    .GroupBy(h => h.MaKH.HasValue)
                    .Select(g => new
                    {
                        IsRegistered = g.Key,
                        Revenue = g.Sum(h => h.TongTien),
                        Percentage = totalRevenue > 0 ? g.Sum(h => h.TongTien) / totalRevenue * 100 : 0
                    })
                    .ToListAsync();

                var topCustomers = await _context.HoaDons
                    .AsNoTracking()
                    .Where(h => h.ThoiGianTao >= startDate && h.ThoiGianTao <= endDate && h.TrangThai == "Hoàn thành" && h.MaKH != null)
                    .Include(h => h.KhachHang)
                    .GroupBy(h => new { h.MaKH, h.KhachHang.HoTen })
                    .Select(g => new CustomerRevenueDTO
                    {
                        MaKH = g.Key.MaKH,
                        HoTen = g.Key.HoTen ?? "Unknown",
                        Revenue = g.Sum(h => h.TongTien),
                        Percentage = totalRevenue > 0 ? g.Sum(h => h.TongTien) / totalRevenue * 100 : 0
                    })
                    .OrderByDescending(c => c.Revenue)
                    .Take(5)
                    .ToListAsync();

                var result = new
                {
                    GuestRevenue = customerRevenue.FirstOrDefault(c => !c.IsRegistered)?.Revenue ?? 0,
                    GuestPercentage = customerRevenue.FirstOrDefault(c => !c.IsRegistered)?.Percentage ?? 0,
                    RegisteredRevenue = customerRevenue.FirstOrDefault(c => c.IsRegistered)?.Revenue ?? 0,
                    RegisteredPercentage = customerRevenue.FirstOrDefault(c => c.IsRegistered)?.Percentage ?? 0,
                    TopCustomers = topCustomers
                };

                _logger.LogInformation("Customer revenue: Guest={Guest}, Registered={Registered}", result.GuestRevenue, result.RegisteredRevenue);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching customer revenue from {StartDate} to {EndDate}", startDate, endDate);
                return StatusCode(500, new { Errors = new[] { $"Không thể tải dữ liệu doanh thu khách hàng: {ex.Message}" } });
            }
        }

        [HttpGet("reports/sales")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> GetSalesReport([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            startDate = startDate.HasValue
                ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Local).ToUniversalTime()
                : DateTime.UtcNow.AddDays(-30);

            endDate = endDate.HasValue
                ? DateTime.SpecifyKind(endDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime()
                : DateTime.UtcNow;

            _logger.LogInformation("Received sales report request: startDate={StartDate}, endDate={EndDate}, Query={Query}",
                startDate, endDate, Request.QueryString);

            if (startDate > endDate)
            {
                _logger.LogWarning("Invalid date range sales: startDate {StartDate} > endDate {EndDate}", startDate, endDate);
                return BadRequest(new { Errors = new[] { "Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc" } });
            }

            try
            {
                var report = await _context.HoaDons
                    .AsNoTracking()
                    .Where(h => h.ThoiGianTao >= startDate && h.ThoiGianTao <= endDate && h.TrangThai == "Hoàn thành")
                    .Include(h => h.ChiTietHoaDons)
                    .ThenInclude(ct => ct.SanPham)
                    .Include(h => h.NhanVien)
                    .Select(h => new SalesReportDTO
                    {
                        MaHD = h.MaHD,
                        ThoiGianTao = h.ThoiGianTao,
                        TongTien = h.TongTien,
                        NhanVien = h.NhanVien.HoTen,
                        ChiTiet = h.ChiTietHoaDons.Select(ct => new SalesReportChiTietDTO
                        {
                            TenSP = ct.SanPham.TenSP,
                            SoLuong = ct.SoLuong,
                            DonGia = ct.DonGia,
                            ThanhTien = ct.ThanhTien
                        }).ToList()
                    })
                    .ToListAsync();

                if (!report.Any())
                {
                    _logger.LogInformation("No sales data found for {StartDate} to {EndDate}", startDate, endDate);
                    return Ok(new { Message = "Không có dữ liệu báo cáo trong khoảng thời gian này", Data = report });
                }

                var csv = GenerateCsv(report);
                _logger.LogInformation("Sales report generated successfully");
                return File(csv, "text/csv", $"sales_report_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching sales report from {StartDate} to {EndDate}", startDate, endDate);
                return StatusCode(500, new { Errors = new[] { $"Không thể tạo báo cáo bán hàng: {ex.Message}" } });
            }
        }

        private byte[] GenerateCsv(List<SalesReportDTO> report)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                using var writer = new StreamWriter(memoryStream);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

                csv.WriteRecords(report.SelectMany(r => r.ChiTiet.Select(ct => new
                {
                    r.MaHD,
                    r.ThoiGianTao,
                    r.TongTien,
                    r.NhanVien,
                    ct.TenSP,
                    ct.SoLuong,
                    ct.DonGia,
                    ct.ThanhTien
                })));
                writer.Flush();
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating CSV for sales report");
                throw;
            }
        }
    }

}

namespace CoffeeShopAPI.DTOs
{
    public class RevenueDTO
    {
        public DateTime Date { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class SalesReportDTO
    {
        public int MaHD { get; set; }
        public DateTime ThoiGianTao { get; set; }
        public decimal TongTien { get; set; }
        public string NhanVien { get; set; }
        public List<SalesReportChiTietDTO> ChiTiet { get; set; }
    }

    public class SalesReportChiTietDTO
    {
        public string TenSP { get; set; }
        public int SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public decimal ThanhTien { get; set; }
    }

    public class DashboardDTO
    {
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int TotalCustomers { get; set; }
    }

    public class ProductRevenueDTO
    {
        public int MaSP { get; set; }
        public string TenSP { get; set; }
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
        public decimal Percentage { get; set; }
    }

    public class CustomerRevenueDTO
    {
        public int? MaKH { get; set; }
        public string HoTen { get; set; }
        public decimal Revenue { get; set; }
        public decimal Percentage { get; set; }
    }
}