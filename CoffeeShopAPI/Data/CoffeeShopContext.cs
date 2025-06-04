using Microsoft.EntityFrameworkCore;
using CoffeeShopAPI.Models;

namespace CoffeeShopAPI
{
    public class CoffeeShopContext : DbContext
    {
        public CoffeeShopContext(DbContextOptions<CoffeeShopContext> options) : base(options) { }

        public DbSet<NhanVien> NhanViens { get; set; }
        public DbSet<KhachHang> KhachHangs { get; set; }
        public DbSet<SanPham> SanPhams { get; set; }
        public DbSet<Table> Tables { get; set; }
        public DbSet<HoaDon> HoaDons { get; set; }
        public DbSet<ChiTietHoaDon> ChiTietHoaDons { get; set; }
        public DbSet<LoaiSanPham> LoaiSanPhams { get; set; }
        public DbSet<DiemTichLuy> DiemTichLuys { get; set; }
        public DbSet<ChamCong> ChamCongs { get; set; }
        public DbSet<GioHang> GioHangs { get; set; }
        public DbSet<GioHangItem> GioHangItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // NhanVien
            modelBuilder.Entity<NhanVien>()
                .HasKey(nv => nv.MaNV);
            modelBuilder.Entity<NhanVien>()
                .Property(nv => nv.Username).HasMaxLength(50);
            modelBuilder.Entity<NhanVien>()
                .Property(nv => nv.MatKhau).HasMaxLength(100);
            modelBuilder.Entity<NhanVien>()
                .Property(nv => nv.HoTen).HasMaxLength(100);
            modelBuilder.Entity<NhanVien>()
                .Property(nv => nv.Email).HasMaxLength(100);
            modelBuilder.Entity<NhanVien>()
                .Property(nv => nv.DiaChi).HasMaxLength(200);
            modelBuilder.Entity<NhanVien>()
                .Property(nv => nv.SoDienThoai).HasMaxLength(20);
            modelBuilder.Entity<NhanVien>()
                .Property(nv => nv.GioiTinh).HasMaxLength(10);
            modelBuilder.Entity<NhanVien>()
                .Property(nv => nv.Role).HasMaxLength(50);
            modelBuilder.Entity<NhanVien>()
                .Property(nv => nv.IsActive).HasDefaultValue(true);

            // KhachHang
            modelBuilder.Entity<KhachHang>()
                .HasKey(kh => kh.MaKH);
            modelBuilder.Entity<KhachHang>()
                .Property(kh => kh.Username).HasMaxLength(50);
            modelBuilder.Entity<KhachHang>()
                .Property(kh => kh.MatKhau).HasMaxLength(100);
            modelBuilder.Entity<KhachHang>()
                .Property(kh => kh.HoTen).HasMaxLength(100);
            modelBuilder.Entity<KhachHang>()
                .Property(kh => kh.Email).HasMaxLength(100);
            modelBuilder.Entity<KhachHang>()
                .Property(kh => kh.DiaChi).HasMaxLength(200);
            modelBuilder.Entity<KhachHang>()
                .Property(kh => kh.SoDienThoai).HasMaxLength(20);
            modelBuilder.Entity<KhachHang>()
                .Property(kh => kh.IsActive).HasDefaultValue(true);

            // SanPham
            modelBuilder.Entity<SanPham>()
                .HasKey(sp => sp.MaSP);
            modelBuilder.Entity<SanPham>()
                .Property(sp => sp.TenSP).HasMaxLength(100);
            modelBuilder.Entity<SanPham>()
                .Property(sp => sp.DonViTinh).HasMaxLength(50);
            modelBuilder.Entity<SanPham>()
                .Property(sp => sp.MoTa).HasMaxLength(500);
            modelBuilder.Entity<SanPham>()
                .Property(sp => sp.HinhAnh).HasMaxLength(200);
            modelBuilder.Entity<SanPham>()
                .Property(sp => sp.IsActive).HasDefaultValue(true);
            modelBuilder.Entity<SanPham>()
                .HasOne(sp => sp.LoaiSanPham)
                .WithMany(lsp => lsp.SanPhams)
                .HasForeignKey(sp => sp.MaLoai)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_SanPham_LoaiSanPham_MaLoai");

            // Table
            modelBuilder.Entity<Table>()
                .HasKey(t => t.MaBan);
            modelBuilder.Entity<Table>()
                .Property(t => t.BanSo).HasMaxLength(50);
            modelBuilder.Entity<Table>()
                .Property(t => t.TrangThai).HasMaxLength(50);
            modelBuilder.Entity<Table>()
                .Property(t => t.ViTri).HasMaxLength(100);
            modelBuilder.Entity<Table>()
                .Property(t => t.IsActive).HasDefaultValue(true);

            // HoaDon
            modelBuilder.Entity<HoaDon>()
                .HasKey(h => h.MaHD);
            modelBuilder.Entity<HoaDon>()
                .Property(h => h.TrangThai).HasMaxLength(50);
            modelBuilder.Entity<HoaDon>()
                .Property(h => h.HinhThucThanhToan).HasMaxLength(50);
            modelBuilder.Entity<HoaDon>()
                .Property(h => h.IsActive).HasDefaultValue(true);
            modelBuilder.Entity<HoaDon>()
                .HasOne(h => h.NhanVien)
                .WithMany(nv => nv.HoaDons)
                .HasForeignKey(h => h.MaNV)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_HoaDon_NhanVien_MaNV");
            modelBuilder.Entity<HoaDon>()
                .HasOne(h => h.KhachHang)
                .WithMany(kh => kh.HoaDons)
                .HasForeignKey(h => h.MaKH)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_HoaDon_KhachHang_MaKH");
            modelBuilder.Entity<HoaDon>()
                .HasOne(h => h.Table)
                .WithMany(t => t.HoaDons)
                .HasForeignKey(h => h.MaBan)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_HoaDon_Tables_MaBan");

            // ChiTietHoaDon
            modelBuilder.Entity<ChiTietHoaDon>()
                .HasKey(ct => ct.MaCTHD);
            modelBuilder.Entity<ChiTietHoaDon>()
                .Property(ct => ct.GhiChu).HasMaxLength(200);
            modelBuilder.Entity<ChiTietHoaDon>()
                .Property(ct => ct.Size).HasMaxLength(10);
            modelBuilder.Entity<ChiTietHoaDon>()
                .Property(ct => ct.Topping).HasMaxLength(100);
            modelBuilder.Entity<ChiTietHoaDon>()
                .HasOne(ct => ct.HoaDon)
                .WithMany(h => h.ChiTietHoaDons)
                .HasForeignKey(ct => ct.MaHD)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ChiTietHoaDon_HoaDon_MaHD");
            modelBuilder.Entity<ChiTietHoaDon>()
                .HasOne(ct => ct.SanPham)
                .WithMany(sp => sp.ChiTietHoaDons)
                .HasForeignKey(ct => ct.MaSP)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ChiTietHoaDon_SanPham_MaSP");

            // LoaiSanPham
            modelBuilder.Entity<LoaiSanPham>()
                .HasKey(lsp => lsp.MaLoai);
            modelBuilder.Entity<LoaiSanPham>()
                .Property(lsp => lsp.TenLoai).HasMaxLength(100);
            modelBuilder.Entity<LoaiSanPham>()
                .Property(lsp => lsp.IsActive).HasDefaultValue(true);

            // DiemTichLuy
            modelBuilder.Entity<DiemTichLuy>()
                .HasKey(dt => dt.MaKH);
            modelBuilder.Entity<DiemTichLuy>()
                .HasOne(dt => dt.KhachHang)
                .WithOne(kh => kh.DiemTichLuy)
                .HasForeignKey<DiemTichLuy>(dt => dt.MaKH)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_DiemTichLuy_KhachHang_MaKH");

            // ChamCong
            modelBuilder.Entity<ChamCong>()
                .HasKey(cc => cc.MaChamCong);
            modelBuilder.Entity<ChamCong>()
                .Property(cc => cc.LoaiChamCong).HasMaxLength(50);
            modelBuilder.Entity<ChamCong>()
                .Property(cc => cc.TrangThai).HasMaxLength(50);
            modelBuilder.Entity<ChamCong>()
                .Property(cc => cc.GhiChu).HasMaxLength(200);
            modelBuilder.Entity<ChamCong>()
                .Property(cc => cc.IsActive).HasDefaultValue(true);
            modelBuilder.Entity<ChamCong>()
                .HasOne(cc => cc.NhanVien)
                .WithMany(nv => nv.ChamCongs)
                .HasForeignKey(cc => cc.MaNV)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ChamCong_NhanVien_MaNV");

            // GioHang
            modelBuilder.Entity<GioHang>()
                .HasKey(gh => gh.MaGioHang);
            modelBuilder.Entity<GioHang>()
                .Property(gh => gh.IsActive).HasDefaultValue(true);
            modelBuilder.Entity<GioHang>()
                .HasOne(gh => gh.KhachHang)
                .WithMany(kh => kh.GioHangs)
                .HasForeignKey(gh => gh.MaKH)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_GioHang_KhachHang_MaKH");

            // GioHangItem
            modelBuilder.Entity<GioHangItem>()
                .HasKey(ghi => ghi.MaItem);
            modelBuilder.Entity<GioHangItem>()
                .Property(ghi => ghi.Size).HasMaxLength(10);
            modelBuilder.Entity<GioHangItem>()
                .Property(ghi => ghi.Topping).HasMaxLength(100);
            modelBuilder.Entity<GioHangItem>()
                .Property(ghi => ghi.IsActive).HasDefaultValue(true);
            modelBuilder.Entity<GioHangItem>()
                .HasOne(ghi => ghi.GioHang)
                .WithMany(gh => gh.GioHangItems)
                .HasForeignKey(ghi => ghi.MaGioHang)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_GioHangItem_GioHang_MaGioHang");
            modelBuilder.Entity<GioHangItem>()
                .HasOne(ghi => ghi.SanPham)
                .WithMany(sp => sp.GioHangItems)
                .HasForeignKey(ghi => ghi.MaSP)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_GioHangItem_SanPham_MaSP");
        }
    }
}