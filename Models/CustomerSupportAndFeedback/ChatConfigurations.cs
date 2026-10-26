using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RROMS.Web.Models.CustomerSupportAndFeedback;

public class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> builder)
    {
        builder.ToTable("ChatSession");

        builder.Property(s => s.StartedAt).HasDefaultValueSql("SYSDATETIME()");
        builder.HasIndex(s => s.CustomerUserId);

        // a conversation's messages are deleted with it
        builder.HasMany(s => s.Messages).WithOne(m => m.ChatSession)
               .HasForeignKey(m => m.ChatSessionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessage", t =>
        {
            t.HasCheckConstraint("CK_ChatMessage_Sender", "[Sender] IN ('Customer','Bot')");
        });

        builder.Property(m => m.SentAt).HasDefaultValueSql("SYSDATETIME()");
    }
}
