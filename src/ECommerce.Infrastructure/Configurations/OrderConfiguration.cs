using ECommerce.Core.Entities;
using ECommerce.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Infrastructure.Configurations
{
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            builder.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(e => e.ProductId)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(e => e.Quantity)
                .IsRequired();

            builder.Property(e => e.PaymentMethod)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue(OrderStatus.Pending);

            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            builder.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_orders_userid");

            builder.HasIndex(e => e.Status)
                .HasDatabaseName("idx_orders_status");

            builder.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_orders_created_at");
        }
    }
}
