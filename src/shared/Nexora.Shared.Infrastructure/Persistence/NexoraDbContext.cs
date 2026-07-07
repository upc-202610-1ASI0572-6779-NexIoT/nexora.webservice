using Microsoft.EntityFrameworkCore;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;

namespace Nexora.Infrastructure.Persistence
{
    public class NexoraDbContext : DbContext
    {
        public DbSet<Device> Devices => Set<Device>();
        public DbSet<TelemetryLog> TelemetryLogs => Set<TelemetryLog>();
        public DbSet<Alert> Alerts => Set<Alert>();
        public DbSet<MaintenanceTicket> MaintenanceTickets => Set<MaintenanceTicket>();
        public DbSet<Property> Properties => Set<Property>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Landlord> Landlords => Set<Landlord>();
        public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<SubscriptionEvent> SubscriptionEvents => Set<SubscriptionEvent>();
        public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<SavedCard> SavedCards => Set<SavedCard>();

        public NexoraDbContext(DbContextOptions<NexoraDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(u => u.Id);

                entity.Property(u => u.Id).HasColumnName("id");
                entity.Property(u => u.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
                entity.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
                entity.Property(u => u.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(u => u.FailedLoginAttempts).HasColumnName("failed_login_attempts").HasDefaultValue(0);
                entity.Property(u => u.LockedAt).HasColumnName("locked_at");
                entity.Property(u => u.UserableType).HasColumnName("userable_type").HasMaxLength(50);
                entity.Property(u => u.UserableId).HasColumnName("userable_id");
                entity.Property(u => u.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(u => u.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasIndex(u => new { u.UserableType, u.UserableId }).HasDatabaseName("IX_users_userable_type_userable_id");

                // Initial users will be seeded at runtime by DataSeeder to allow generated IDs and password hashing.
            });

            modelBuilder.Entity<Landlord>(entity =>
            {
                entity.ToTable("landlords");
                entity.HasKey(l => l.Id);

                entity.Property(l => l.Id).HasColumnName("id");
                entity.Property(l => l.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(l => l.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
                entity.Property(l => l.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
                entity.Property(l => l.Country).HasColumnName("country").HasMaxLength(100).IsRequired();
                entity.Property(l => l.City).HasColumnName("city").HasMaxLength(100).IsRequired();
                entity.Property(l => l.Address).HasColumnName("address").HasMaxLength(255).IsRequired();
                entity.Property(l => l.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
                entity.Property(l => l.StripeCustomerId).HasColumnName("stripe_customer_id").HasMaxLength(100);
                entity.Property(l => l.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(l => l.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(l => l.User)
                    .WithOne()
                    .HasForeignKey<Landlord>(l => l.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Initial landlords will be created by DataSeeder after users are created.
            });

            modelBuilder.Entity<Property>(entity =>
            {
                entity.ToTable("properties");
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Id).HasColumnName("id");
                entity.Property(p => p.PropertyCode)
                    .HasColumnName("property_code")
                    .HasMaxLength(8)
                    .IsRequired();
                entity.Property(p => p.LandlordId).HasColumnName("landlord_id").IsRequired();
                entity.Property(p => p.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
                entity.Property(p => p.Description).HasColumnName("description").HasMaxLength(500);

                entity.Property(p => p.PropertyType)
                    .HasColumnName("property_type")
                    .HasConversion<string>()
                    .HasMaxLength(30)
                    .IsRequired();

                entity.Property(p => p.Country).HasColumnName("country").HasMaxLength(100).IsRequired();
                entity.Property(p => p.City).HasColumnName("city").HasMaxLength(100).IsRequired();
                entity.Property(p => p.Address).HasColumnName("address").HasMaxLength(255).IsRequired();

                entity.Property(p => p.Status)
                    .HasColumnName("status")
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValue(PropertyStatus.ACTIVE)
                    .IsRequired();

                entity.Property(p => p.IsSecurityModeArmed).HasColumnName("is_security_mode_armed").HasDefaultValue(false);
                entity.Property(p => p.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(p => p.PropertyCode)
                    .IsUnique()
                    .HasDatabaseName("IX_properties_property_code");

                entity.HasOne(p => p.Landlord)
                    .WithMany(l => l.Properties)
                    .HasForeignKey(p => p.LandlordId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Initial properties will be created by DataSeeder via application commands to ensure PropertyCode generation.
            });

            modelBuilder.Entity<Device>(entity =>
            {
                entity.ToTable("devices");
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Id).HasMaxLength(100);

                entity.Property(d => d.MacAddress).HasMaxLength(100);
                entity.Property(d => d.Name).HasMaxLength(150);
                entity.Property(d => d.Rssi).HasColumnName("rssi");
                entity.Property(d => d.FirmwareVersion).HasColumnName("firmware_version").HasMaxLength(50);
                entity.HasIndex(d => d.MacAddress).IsUnique();

                entity.Property(d => d.ConnectionStatus)
                    .HasConversion<string>()
                    .HasColumnType("text")
                    .IsRequired();

                entity.HasOne(d => d.Property)
                    .WithMany()
                    .HasForeignKey(d => d.PropertyId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<TelemetryLog>(entity =>
            {
                entity.ToTable("telemetry_logs");
                entity.HasKey(t => t.Id);

                entity.HasOne(t => t.Device)
                    .WithMany()
                    .HasForeignKey(t => t.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(t => t.Timestamp).HasDatabaseName("IX_telemetry_logs_timestamp");
            });

            modelBuilder.Entity<Alert>(entity =>
            {
                entity.ToTable("alerts");
                entity.HasKey(a => a.Id);

                entity.Property(a => a.Severity).HasConversion<string>().HasColumnType("text").IsRequired();

                entity.HasOne(a => a.Device)
                    .WithMany()
                    .HasForeignKey(a => a.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<MaintenanceTicket>(entity =>
            {
                entity.ToTable("maintenance_tickets");
                entity.HasKey(m => m.Id);

                entity.Property(m => m.Status).HasConversion<string>().HasColumnType("text").IsRequired();

                entity.HasOne(m => m.Alert)
                    .WithOne()
                    .HasForeignKey<MaintenanceTicket>(m => m.AlertId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SubscriptionPlan>(entity =>
            {
                entity.ToTable("subscription_plans");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Id).HasColumnName("id");
                entity.Property(s => s.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
                entity.Property(s => s.MonthlyPrice).HasColumnName("monthly_price").HasColumnType("decimal(18,2)").IsRequired();
                entity.Property(s => s.MaxPropertiesLimit).HasColumnName("max_properties_limit").IsRequired();
                entity.Property(s => s.UnlimitedProperties).HasColumnName("unlimited_properties").HasDefaultValue(false);
                entity.Property(s => s.IsActive).HasColumnName("is_active").HasDefaultValue(true);

                // Subscription plans will be seeded by DataSeeder if absent.

            });

            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.ToTable("subscriptions");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Id).HasColumnName("id");

                entity.Property(s => s.LandlordId).HasColumnName("landlord_id").IsRequired();
                entity.Property(s => s.SubscriptionPlanId).HasColumnName("subscription_plan_id").IsRequired();

                entity.Property(s => s.Status)
                    .HasColumnName("status")
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(s => s.StartedAt).HasColumnName("started_at").IsRequired();
                entity.Property(s => s.CurrentPeriodStart).HasColumnName("current_period_start").IsRequired();
                entity.Property(s => s.CurrentPeriodEnd).HasColumnName("current_period_end").IsRequired();
                entity.Property(s => s.CancelAtPeriodEnd).HasColumnName("cancel_at_period_end").HasDefaultValue(false);
                entity.Property(s => s.CancelledAt).HasColumnName("cancelled_at");
                entity.Property(s => s.StripeSubscriptionId).HasColumnName("stripe_subscription_id").HasMaxLength(100);

                entity.HasOne(s => s.Landlord)
                    .WithOne()
                    .HasForeignKey<Subscription>(s => s.LandlordId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.Plan)
                    .WithMany()
                    .HasForeignKey(s => s.SubscriptionPlanId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.ToTable("invoices");
                entity.HasKey(i => i.Id);
                entity.Property(i => i.Id).HasColumnName("id");

                entity.Property(i => i.SubscriptionId).HasColumnName("subscription_id").IsRequired();
                entity.Property(i => i.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)").IsRequired();

                entity.Property(i => i.Status)
                    .HasColumnName("status")
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(i => i.DueDate).HasColumnName("due_date").IsRequired();
                entity.Property(i => i.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(i => i.Subscription)
                    .WithMany(s => s.Invoices)
                    .HasForeignKey(i => i.SubscriptionId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Invoices will be created by application logic or DataSeeder when needed.
            });

            modelBuilder.Entity<Payment>(entity =>
            {
                entity.ToTable("payments");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Id).HasColumnName("id");

                entity.Property(p => p.InvoiceId).HasColumnName("invoice_id").IsRequired();
                entity.Property(p => p.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)").IsRequired();

                entity.Property(p => p.Status)
                    .HasColumnName("status")
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(p => p.Provider).HasColumnName("provider").HasMaxLength(50).IsRequired();
                entity.Property(p => p.ProviderTransactionId).HasColumnName("provider_transaction_id").HasMaxLength(255).IsRequired();
                entity.Property(p => p.PaidAt).HasColumnName("paid_at").IsRequired();

                entity.HasOne(p => p.Invoice)
                    .WithMany(i => i.Payments)
                    .HasForeignKey(p => p.InvoiceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SubscriptionEvent>(entity =>
            {
                entity.ToTable("subscription_events");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.SubscriptionId).HasColumnName("subscription_id").IsRequired();
                entity.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
                entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(e => e.Subscription)
                    .WithMany(s => s.Events)
                    .HasForeignKey(e => e.SubscriptionId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Subscription events will be created by application logic or DataSeeder when needed.
            });

            modelBuilder.Entity<NotificationPreference>(entity =>
            {
                entity.ToTable("notification_preferences");
                entity.HasKey(n => n.Id);
                entity.Property(n => n.Id).HasColumnName("id");
                entity.Property(n => n.UserId).HasColumnName("user_id");
                entity.Property(n => n.ReceiveEmailAlerts).HasColumnName("receive_email_alerts");
                entity.Property(n => n.ReceiveSmsAlerts).HasColumnName("receive_sms_alerts");

                entity.HasOne(n => n.User)
                    .WithOne()
                    .HasForeignKey<NotificationPreference>(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.ToTable("tenants");
                entity.HasKey(t => t.Id);

                entity.Property(t => t.Id).HasColumnName("id");
                entity.Property(t => t.PropertyId).HasColumnName("property_id"); // nullable: tenant may not be linked yet
                entity.Property(t => t.UserId).HasColumnName("user_id");
                entity.Property(t => t.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
                entity.Property(t => t.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
                entity.Property(t => t.Country).HasColumnName("country").HasMaxLength(100).IsRequired();
                entity.Property(t => t.City).HasColumnName("city").HasMaxLength(100).IsRequired();
                entity.Property(t => t.Address).HasColumnName("address").HasMaxLength(255).IsRequired();
                entity.Property(t => t.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
                entity.Property(t => t.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(t => t.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(t => t.Property)
                    .WithMany(p => p.Tenants)
                    .HasForeignKey(t => t.PropertyId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired(false);

                entity.HasOne(t => t.User)
                    .WithOne()
                    .HasForeignKey<Tenant>(t => t.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<SavedCard>(entity =>
            {
                entity.ToTable("saved_cards");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Id).HasColumnName("id");
                entity.Property(c => c.LandlordId).HasColumnName("landlord_id").IsRequired();
                entity.Property(c => c.Brand).HasColumnName("brand").HasMaxLength(50).IsRequired();
                entity.Property(c => c.LastFour).HasColumnName("last_four").HasMaxLength(4).IsRequired();
                entity.Property(c => c.FullNumber).HasColumnName("full_number").HasMaxLength(19).IsRequired();
                entity.Property(c => c.ExpiryMonth).HasColumnName("expiry_month").HasMaxLength(2).IsRequired();
                entity.Property(c => c.ExpiryYear).HasColumnName("expiry_year").HasMaxLength(2).IsRequired();
                entity.Property(c => c.HolderName).HasColumnName("holder_name").HasMaxLength(100).IsRequired();
                entity.Property(c => c.Cvv).HasColumnName("cvv").HasMaxLength(4).IsRequired();
                entity.Property(c => c.IsDefault).HasColumnName("is_default").HasDefaultValue(false);
                entity.Property(c => c.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(c => c.Landlord)
                    .WithOne()
                    .HasForeignKey<SavedCard>(c => c.LandlordId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
