using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;

namespace RetailForma.Core.Services;

public enum StockReceiptKind { PaidPurchase = 0, OpeningBalance = 1, OwnerContribution = 2 }

public static class AccountingPolicy
{
    public const string Reserve = "1005", InventoryLoss = "5010", OpeningClearing = "3990", Drawings = "3100";
    public static void RequireAdmin(RetailFormaDbContext db, Guid userId)
    {
        if (!db.Users.AsNoTracking().Any(u => u.Id == userId && u.IsActive && UserAccess.AdministratorRoles.Contains(u.Role)))
            throw new InvalidOperationException("An active Owner or System Administrator must approve this posting.");
    }
    public static Account Account(RetailFormaDbContext db, string code, params AccountType[] types)
    {
        var account = db.Accounts.AsNoTracking().SingleOrDefault(a => a.Code == code)
            ?? throw new InvalidOperationException("Counterpart account not found.");
        if (!types.Contains(account.Type)) throw new InvalidOperationException("Counterpart account has an incompatible accounting type.");
        return account;
    }
    public static void RequireProprietor(RetailFormaDbContext db)
    {
        if (db.Settings.AsNoTracking().FirstOrDefault(s => s.Key == "CompanyLegalForm")?.Value != "SoleProprietor")
            throw new InvalidOperationException("Proprietor contributions/drawings require General Settings → legal form: Sole proprietor. Corporate payments need an approved specific counterpart.");
    }
    public static string CashCounterpart(RetailFormaDbContext db, Guid actorId, CashMovementCategory category,
        bool companyOwned, string? explicitAccount)
    {
        switch (category)
        {
            case CashMovementCategory.Expense:
                return "6000";
            case CashMovementCategory.BankDeposit:
                if (!companyOwned) throw new InvalidOperationException("Confirm that this is the company's bank account.");
                return "1010";
            case CashMovementCategory.Remittance:
            case CashMovementCategory.FloatTransfer:
                if (!companyOwned) throw new InvalidOperationException("Confirm a transfer between company-owned cash locations.");
                var cash = explicitAccount ?? Reserve;
                if (cash is not ("1005" or "1010")) throw new InvalidOperationException("Choose a supported company reserve/bank cash account.");
                Account(db, cash, AccountType.Asset);
                return cash;
            case CashMovementCategory.OwnerWithdrawal:
                RequireAdmin(db, actorId); RequireProprietor(db);
                return Drawings;
            case CashMovementCategory.Other:
                RequireAdmin(db, actorId);
                if (string.IsNullOrWhiteSpace(explicitAccount) || explicitAccount == "1000")
                    throw new InvalidOperationException("Other movements require an explicit approved counterpart, different from till cash.");
                if (explicitAccount == Drawings) RequireProprietor(db);
                Account(db, explicitAccount, AccountType.Liability, AccountType.Equity, AccountType.Expense);
                return explicitAccount;
            default:
                throw new InvalidOperationException("Choose a mapped classification. Unclassified movements cannot post to Owner's Equity by default.");
        }
    }
}
