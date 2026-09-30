namespace MyPos.Core.Entities;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";          // stable key: "1000", "4000"...
    public string Name { get; set; } = "";
    public AccountType Type { get; set; }
    public bool IsSystem { get; set; }              // seeded accounts can't be deleted
}

public enum AccountType { Asset = 1, Liability = 2, Equity = 3, Revenue = 4, Expense = 5 }