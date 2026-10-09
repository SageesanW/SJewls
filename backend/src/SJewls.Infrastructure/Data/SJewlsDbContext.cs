using Microsoft.EntityFrameworkCore;
using SJewls.Domain.Entities;

namespace SJewls.Infrastructure.Data;

public class SJewlsDbContext : DbContext
{
    public SJewlsDbContext(DbContextOptions<SJewlsDbContext> options) : base(options)
    {
    }

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<RegistrationSession> RegistrationSessions => Set<RegistrationSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Staff> StaffMembers => Set<Staff>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<StaffRole> StaffRoles => Set<StaffRole>();
    public DbSet<StaffBranch> StaffBranches => Set<StaffBranch>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

    public DbSet<ChituPlan> ChituPlans => Set<ChituPlan>();
    public DbSet<ChituSlot> ChituSlots => Set<ChituSlot>();
    public DbSet<ChituInstalment> ChituInstalments => Set<ChituInstalment>();
    public DbSet<ChituDraw> ChituDraws => Set<ChituDraw>();
    public DbSet<WinnerBenefit> WinnerBenefits => Set<WinnerBenefit>();

    public DbSet<GoldRate> GoldRates => Set<GoldRate>();
    public DbSet<PaymentQuote> PaymentQuotes => Set<PaymentQuote>();

    public DbSet<JewelleryPlanCategory> JewelleryCategories => Set<JewelleryPlanCategory>();
    public DbSet<JewelleryPlan> JewelleryPlans => Set<JewelleryPlan>();
    public DbSet<JewelleryEnrolment> JewelleryEnrolments => Set<JewelleryEnrolment>();
    public DbSet<JewelleryContribution> JewelleryContributions => Set<JewelleryContribution>();

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<Refund> Refunds => Set<Refund>();

    public DbSet<PhysicalClaim> PhysicalClaims => Set<PhysicalClaim>();
    public DbSet<ExtensionRequest> ExtensionRequests => Set<ExtensionRequest>();
    public DbSet<ClosureRequest> ClosureRequests => Set<ClosureRequest>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Precision configurations for Decimals (Money & Gold grams)
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetColumnType("decimal(18, 4)");
        }

        // Branch configuration
        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // Customer configuration
        modelBuilder.Entity<Customer>(entity =>
        {
            // Mandatory unique NIC constraint when NIC is provided
            entity.HasIndex(e => e.Nic)
                  .IsUnique()
                  .HasFilter("\"Nic\" IS NOT NULL");

            entity.HasIndex(e => e.PhoneNumber)
                  .IsUnique()
                  .HasFilter("\"PhoneNumber\" IS NOT NULL");

            entity.HasIndex(e => e.Email)
                  .IsUnique()
                  .HasFilter("\"Email\" IS NOT NULL");

            entity.HasOne(c => c.DeactivatedByStaff)
                  .WithMany()
                  .HasForeignKey(c => c.DeactivatedByStaffId)
                  .OnDelete(DeleteBehavior.SetNull);
        });


        // Registration sessions
        modelBuilder.Entity<RegistrationSession>(entity =>
        {
            entity.HasIndex(e => e.RegistrationToken).IsUnique();
        });

        // Refresh tokens
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasOne(r => r.Customer)
                  .WithMany(c => c.RefreshTokens)
                  .HasForeignKey(r => r.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // OTP challenges
        modelBuilder.Entity<OtpChallenge>(entity =>
        {
            entity.HasIndex(e => new { e.ContactValue, e.Purpose, e.IsConsumed });
        });

        // Staff
        modelBuilder.Entity<Staff>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username)
                  .IsUnique()
                  .HasFilter("\"Username\" IS NOT NULL");
        });

        // Role
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Staff Branch uniqueness (prevents duplicate branch assignments for any user)
        modelBuilder.Entity<StaffBranch>(entity =>
        {
            entity.HasIndex(e => new { e.StaffId, e.BranchId }).IsUnique();
        });

        // Staff Role uniqueness (prevents duplicate role assignments for any user)
        modelBuilder.Entity<StaffRole>(entity =>
        {
            entity.HasIndex(e => new { e.StaffId, e.RoleId }).IsUnique();
        });

        // Chitu Slot uniqueness & concurrency
        modelBuilder.Entity<ChituSlot>(entity =>
        {
            entity.HasIndex(e => new { e.ChituPlanId, e.SlotNumber }).IsUnique();
            entity.HasOne(e => e.ChituPlan)
                  .WithMany(p => p.Slots)
                  .HasForeignKey(e => e.ChituPlanId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Chitu Instalments
        modelBuilder.Entity<ChituInstalment>(entity =>
        {
            entity.HasIndex(e => new { e.ChituSlotId, e.MonthIndex }).IsUnique();
            entity.HasOne(e => e.ChituSlot)
                  .WithMany(s => s.Instalments)
                  .HasForeignKey(e => e.ChituSlotId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Chitu Draw & Winner
        modelBuilder.Entity<ChituDraw>(entity =>
        {
            entity.HasOne(d => d.WinningSlot)
                  .WithMany()
                  .HasForeignKey(d => d.WinningSlotId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WinnerBenefit>(entity =>
        {
            entity.HasOne(w => w.ChituDraw)
                  .WithOne(d => d.Benefit)
                  .HasForeignKey<WinnerBenefit>(w => w.ChituDrawId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(w => w.ChituSlot)
                  .WithOne(s => s.WinnerBenefit)
                  .HasForeignKey<WinnerBenefit>(w => w.ChituSlotId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Jewellery Plan Category
        modelBuilder.Entity<JewelleryPlanCategory>(entity =>
        {
            entity.HasIndex(e => new { e.BranchId, e.NormalizedName }).IsUnique();
            entity.HasOne(e => e.Branch)
                  .WithMany(b => b.JewelleryCategories)
                  .HasForeignKey(e => e.BranchId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Jewellery Plan & Enrolment
        modelBuilder.Entity<JewelleryPlan>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => new { e.BranchId, e.IsActive, e.StartDate });
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.JewelleryPlans)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DeactivatedByStaff)
                  .WithMany()
                  .HasForeignKey(e => e.DeactivatedByStaffId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ReopenedByStaff)
                  .WithMany()
                  .HasForeignKey(e => e.ReopenedByStaffId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<JewelleryEnrolment>(entity =>
        {
            entity.HasOne(e => e.JewelleryPlan)
                  .WithMany(p => p.Enrolments)
                  .HasForeignKey(e => e.JewelleryPlanId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<JewelleryContribution>(entity =>
        {
            entity.HasOne(c => c.JewelleryEnrolment)
                  .WithMany(e => e.Contributions)
                  .HasForeignKey(c => c.JewelleryEnrolmentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Payment & Allocation
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(e => e.ProviderReference);
            entity.HasIndex(e => e.IdempotencyKey);
        });

        modelBuilder.Entity<PaymentAllocation>(entity =>
        {
            entity.HasOne(a => a.Payment)
                  .WithMany(p => p.Allocations)
                  .HasForeignKey(a => a.PaymentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
