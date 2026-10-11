using System.Text;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MyPos.Core.Data;
using MyPos.Core.Entities;

namespace MyPos.Core.Services;

public sealed record CashMovementReportRow(Guid Id, Guid ShiftId, DateTime ShiftOpenedAt,
    DateTime Date, CashMovementType Type, string Reason, Guid UserId, string User, decimal Amount,
    CashMovementCategory Category, string ExpenseCategory, string Destination, string Reference, Guid? ApprovedById, DateTime? ApprovedAt, string Approver);

public sealed record CashMovementReport(Guid BranchId, string Branch, DateOnly From, DateOnly To,
    CashMovementType? Type, List<CashMovementReportRow> Rows, CashMovementCategory? Category = null, string Search = "", Guid? ShiftId = null)
{
    public decimal CashIn => Rows.Where(r => r.Type == CashMovementType.CashIn).Sum(r => r.Amount);
    public decimal CashOut => Rows.Where(r => r.Type == CashMovementType.CashOut).Sum(r => r.Amount);
    public decimal NetMovement => CashIn - CashOut;
    public string TypeLabel => Type == CashMovementType.CashIn ? "Cash in" :
        Type == CashMovementType.CashOut ? "Cash out" : "All movements";
    public string FilterLabel => $"{TypeLabel} | Category: {Category?.ToString() ?? "All"} | Search: {(Search.Length == 0 ? "None" : Search)} | Shift: {ShiftId?.ToString() ?? "All accessible"}";
    public string DescribePresentation(bool multipleBranches) => ReportPresentationScope.Format(From, To,
        new (string, string?)[]
        {
            ("Branch", multipleBranches ? Branch : null),
            ("Type", Type.HasValue ? TypeLabel : null),
            ("Classification", Category.HasValue ? ClassificationLabel(Category.Value) : null),
            ("Search", Search),
            ("Shift", ShiftId?.ToString())
        });
    public string CategorySummary => string.Join(" | ", Enum.GetValues<CashMovementCategory>().Select(c =>
        $"{c}: PHP {ReportCsv.Money(Rows.Where(r => r.Category == c && r.Type == CashMovementType.CashOut).Sum(r => r.Amount))}"));

    public static string ClassificationLabel(CashMovementCategory category) => category switch
    {
        CashMovementCategory.BankDeposit => "Bank deposit",
        CashMovementCategory.OwnerWithdrawal => "Owner withdrawal",
        CashMovementCategory.FloatTransfer => "Float transfer",
        _ => category.ToString()
    };

    public string CashOutBreakdown
    {
        get
        {
            var groups = Rows.Where(r => r.Type == CashMovementType.CashOut).GroupBy(r => r.Category)
                .OrderBy(g => g.Key).Select(g => $"{ClassificationLabel(g.Key)}: ₱{g.Sum(r => r.Amount).ToString("N2", CultureInfo.InvariantCulture)}").ToList();
            return groups.Count > 0 ? "Cash-out breakdown: " + string.Join(" | ", groups) : "";
        }
    }

    public string ToCsv()
    {
        var csv = new StringBuilder("Branch,From,To,Filter,MovementId,ShiftId,ShiftOpened,DateTime,Type,Reason,UserId,User,Amount,Category,ExpenseCategory,Destination,Reference,ApproverId,ApprovedAt,Approver\r\n");
        var invariant = CultureInfo.InvariantCulture;
        foreach (var row in Rows)
            csv.AppendLine(string.Join(",", ReportCsv.Text(Branch), From.ToString("yyyy-MM-dd", invariant),
                To.ToString("yyyy-MM-dd", invariant), ReportCsv.Text(FilterLabel), row.Id, row.ShiftId,
                row.ShiftOpenedAt.ToString("yyyy-MM-dd HH:mm:ss", invariant), row.Date.ToString("yyyy-MM-dd HH:mm:ss", invariant),
                ReportCsv.Text(row.Type == CashMovementType.CashIn ? "Cash in" : "Cash out"),
                ReportCsv.Text(row.Reason), row.UserId, ReportCsv.Text(row.User), ReportCsv.Money(row.Amount),
                ReportCsv.Text(row.Category.ToString()), ReportCsv.Text(row.ExpenseCategory), ReportCsv.Text(row.Destination),
                ReportCsv.Text(row.Reference), ReportCsv.Text(row.ApprovedById?.ToString()),
                ReportCsv.Text(row.ApprovedAt?.ToString("yyyy-MM-dd HH:mm:ss", invariant)), ReportCsv.Text(row.Approver)));
        return csv.ToString();
    }
}

public sealed class CashMovementReportService(MyPosDbContext db)
{
    public CashMovementReport GetReport(Guid branchId, Guid userId, DateOnly from, DateOnly to,
        CashMovementType? type = null, CashMovementCategory? category = null, string search = "", Guid? shiftId = null)
    {
        var user = db.Users.AsNoTracking().FirstOrDefault(u => u.Id == userId && u.IsActive)
            ?? throw new InvalidOperationException("An active user is required to view cash movements.");
        if (from > to) throw new InvalidOperationException("From date must not be after To date.");
        if (to > DateOnly.FromDateTime(DateTime.Today))
            throw new InvalidOperationException("Future report dates are not allowed.");
        if (type.HasValue && !Enum.IsDefined(type.Value))
            throw new InvalidOperationException("Invalid cash movement type.");
        if (category.HasValue && !Enum.IsDefined(category.Value)) throw new InvalidOperationException("Invalid classification.");
        var branch = db.Branches.AsNoTracking().FirstOrDefault(b => b.Id == branchId)
            ?? throw new InvalidOperationException("Branch was not found.");
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        // Reuse drawer visibility: admins see all; other users see shared drawers and their assigned drawers.
        // Filter before loading rows, so inaccessible entries and amounts never enter the result.
        var shifts = db.CashShifts.AsNoTracking().Where(s => s.BranchId == branchId);
        if (user.Role != UserRole.Admin)
            shifts = shifts.Where(s => s.DrawerMode == DrawerMode.Shared || s.AssignedCashierId == userId);
        var query = from movement in db.CashMovements.AsNoTracking()
                    join shift in shifts on movement.ShiftId equals shift.Id
                    where movement.MovementDate >= start && movement.MovementDate < end
                    select new { Movement = movement, ShiftOpenedAt = shift.OpenedAt };
        if (type.HasValue) query = query.Where(r => r.Movement.Type == type.Value);
        if (category.HasValue) query = query.Where(r => r.Movement.Category == category.Value);
        if (shiftId.HasValue) query = query.Where(r => r.Movement.ShiftId == shiftId.Value);
        var entries = query.OrderBy(r => r.Movement.MovementDate).ThenBy(r => r.Movement.Id).ToList();
        if (!string.IsNullOrWhiteSpace(search)) entries = entries.Where(e => e.Movement.Reason.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            (e.Movement.Reference ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) || (e.Movement.Destination ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        var userIds = entries.SelectMany(e => new[] { e.Movement.UserId, e.Movement.ApprovedById ?? Guid.Empty }).Distinct().ToList();
        var names = db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id, u => u.FullName);
        var rows = entries.Select(e => new CashMovementReportRow(e.Movement.Id, e.Movement.ShiftId,
            e.ShiftOpenedAt, e.Movement.MovementDate, e.Movement.Type, e.Movement.Reason,
            e.Movement.UserId, names.GetValueOrDefault(e.Movement.UserId, "Unknown user"), e.Movement.Amount,
            e.Movement.Category, e.Movement.ExpenseCategory ?? "", e.Movement.Destination ?? "", e.Movement.Reference ?? "",
            e.Movement.ApprovedById, e.Movement.ApprovedAt, names.GetValueOrDefault(e.Movement.ApprovedById ?? Guid.Empty, "Unapproved"))).ToList();
        return new(branchId, branch.Name, from, to, type, rows, category, search, shiftId);
    }
}
