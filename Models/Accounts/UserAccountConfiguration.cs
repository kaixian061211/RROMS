using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RROMS.Web.Models.Accounts;

public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("UserAccount", t =>
        {
            t.HasCheckConstraint("CK_UserAccounts_Role",
                "[Role] IN ('Admin','Staff','Customer')");
        });

        builder.HasKey(u => u.UserId);   // <-- the new line

        builder.Property(u => u.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

        // one account per email
        builder.HasIndex(u => u.Email).IsUnique();
    }
}