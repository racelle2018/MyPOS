using Microsoft.EntityFrameworkCore;
using MyPos.Core.Data;
using MyPos.Core.Entities;
using MyPos.Core.Services;
using Xunit;

namespace MyPos.Tests;

public class HeldSaleServiceTests
{
    [Fact]
    public void Reading_a_held_cart_keeps_it_until_recall_is_completed()
    {
        var options = new DbContextOptionsBuilder<MyPosDbContext>()
            .UseSqlite("Data Source=:memory:").Options;
        using var db = new MyPosDbContext(options);
        db.Database.OpenConnection();
        DbInitializer.Initialize(db);

        var branchId = db.Branches.Single().Id;
        var userId = db.Users.Single().Id;
        var service = new HeldSaleService(db);
        var productId = Guid.NewGuid();
        var held = service.Hold(
            branchId,
            userId,
            new List<HeldCartLine> { new(productId, "Item", null, 112m, 50m, false, 2m, 5m) },
            "Customer",
            "OR-42",
            null,
            customerAddress: "Address",
            paymentMethod: PaymentMethod.Gcash,
            orderType: OrderType.Delivery,
            discountKind: DiscountKind.SeniorPwd,
            seniorIdNumber: "SC-123");

        var lines = service.ReadCart(held.Id);
        Assert.Single(lines);
        Assert.NotNull(db.HeldSales.Find(held.Id));
        Assert.Equal("Address", held.CustomerAddress);
        Assert.Equal(PaymentMethod.Gcash, held.PaymentMethod);
        Assert.Equal(DiscountKind.SeniorPwd, held.DiscountKind);

        held.HeldAt = DateTime.Now.AddDays(-2);
        db.SaveChanges();
        Assert.Contains(service.ListActive(branchId), draft => draft.Id == held.Id);

        service.CompleteRecall(held.Id, userId);
        Assert.Null(db.HeldSales.Find(held.Id));
        Assert.Contains(db.AuditLogs, entry => entry.Action == "HeldSaleRecall" && entry.UserId == userId);
    }
}
