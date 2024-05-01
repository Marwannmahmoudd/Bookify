using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Bookify.Web.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<Area> Areas { get; set; }
        public DbSet<Author> Authors { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<BookCategory> BookCategories { get; set; }

        public DbSet<BookCopy> BookCopies { get; set; }
        public DbSet<Category> Categories { get; set; }

        public DbSet<Governorate> Governorates { get; set; }
        public DbSet<Rental> Rentals { get; set; }
        public DbSet<RentalCopy> RentalCopies { get; set; }
        public DbSet<Subscriber> Subscribers { get; set; }
        public DbSet<Subscribtion> Subscribtions { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.HasSequence<int>("SerialNumber", schema: "shared")
                .StartsAt(1000001);
            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.Email)
                .IsUnique();
            builder.Entity<ApplicationUser>()
              .HasIndex(u => u.UserName)
              .IsUnique();
            builder.Entity<BookCopy>()
                .Property(e => e.SerialNumber)
                .HasDefaultValueSql("NEXT VALUE FOR shared.SerialNumber");

            builder.Entity<BookCategory>().HasKey(e => new { e.BookId, e.CategoryId });
            builder.Entity<RentalCopy>().HasKey(e => new { e.RentalId, e.BookCopyId });
            builder.Entity<Rental>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<RentalCopy>().HasQueryFilter(e => !e.Rental!.IsDeleted);

            var cascadFk = builder.Model.GetEntityTypes()
                .SelectMany(t => t.GetForeignKeys())
                .Where(fk => fk.DeleteBehavior == DeleteBehavior.Cascade && !fk.IsOwnership);
            foreach (var foreignKey in cascadFk)
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }
            base.OnModelCreating(builder);
        }
    }
}