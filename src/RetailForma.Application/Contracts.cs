using System.Text.Json;
using System.Text.Json.Serialization;
using RetailForma.Core.Entities;
using RetailForma.Core.Services;

namespace RetailForma.ServiceApplication;

public enum Operation
{
    PublicIdentity, Login, Logout, Lock, Unlock, ChangePassword, CurrentUser,
    Products, Customers, Branches, Staff, Accounts, Settings, Shifts, AuditLog, HistoricalCategories, AuditChoices,
    SaveProduct, ActivateProduct, SaveUser, ActivateUser, SaveSettings, SaveAppearance,
    OpenShift, OpenDrawer, CanUseDrawer, ShiftTotals, CloseShift, ConfigureDrawer, CountingMinimum, CountingDenominations,
    ConfigureCounting, RecordCashMovement, ApproveCashMovement, PostSale, VoidSale, ReceiveStock, AdjustStock,
    HeldSales, HoldSale, ReadHeldCart, CompleteRecall, DiscardHeld, InvoiceExists,
    DailySales, ValidateScope, DescribeScope, ProductSales, SalesSummary, VoidEvents, ShiftReport, CashMovementReport,
    RecordClientEvent, ServiceStatus, ProvisionNewCompany, ProvisionTechnicalAccount,
    IssueAccountRecovery, CompleteAccountRecovery, LicenseRequest, ImportLicense, StartTrial, ValidateLicense,
    BackupPhraseState, VerifyBackupPhrase, ChangeBackupPhrase, CreateBackup, ReadBackupChunk, FinishBackupDownload,
    BeginRestoreUpload, WriteRestoreChunk, PrepareRestore, CommitRestore, CancelRestore,
    BeginCompanyImport, WriteCompanyImportChunk, PreviewCompanyImport, CommitCompanyImport, CancelCompanyImport
}

public sealed record WireRequest(Operation Operation, JsonElement Payload);
public sealed record WireResponse(bool Success, JsonElement? Value = null, string? Error = null, string? Code = null);
public static class WireJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        MaxDepth = 32, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false
    };
    public static JsonElement Element<T>(T value) => JsonSerializer.SerializeToElement(value, Options);
    public static T Read<T>(JsonElement value) => value.Deserialize<T>(Options) ?? throw new ArgumentException("Missing request data.");
}

// Never return the persisted User/PasswordHash through IPC.
public sealed record ClientUser(Guid Id, string Username, string FullName, UserRole Role, bool IsActive,
    DateTime? LastLoginAt, bool MustChangePassword);
public sealed record LoginInput(string Username, string Password);
public sealed record PasswordInput(string Password);
public sealed record ChangePasswordInput(string Current, string Next, string Confirmation);
public sealed record IdInput(Guid Id);
public sealed record ToggleInput(Guid Id, bool Active);
public sealed record ProductInput(Guid? Id, string Name, string? Barcode, string Category, string Unit, decimal Price, bool IsVatExempt, decimal? CostPrice = null);
public sealed record UserInput(Guid? Id, string Username, string FullName, UserRole Role, string? Password, bool Active = true);
public sealed record SettingsInput(Dictionary<string, string> Values);
public sealed record OpenShiftInput(Guid BranchId, decimal OpeningFloat, string? Counterpart);
public sealed record CloseShiftInput(decimal Counted, string? Notes, Dictionary<decimal, int>? Denominations,
    Guid? ShiftId, ShiftService.ShiftTotals? ReviewedTotals);
public sealed record DrawerInput(DrawerMode Mode, Guid? AssignedCashierId);
public sealed record DecimalInput(decimal Value);
public sealed record MovementInput(CashMovementType Type, decimal Amount, string Reason, CashMovementCategory Category,
    string? Reference, string? Destination, string? ExpenseCategory, bool Approve, bool CompanyOwnedDestination, string? Counterpart);
public sealed record PostSaleInput(Guid BranchId, List<CartLine> Lines, decimal Discount, decimal Tendered, PaymentMethod Method,
    string? Reference, ReceiptType ReceiptType, string? ManualReceiptNumber, string? CustomerName, string? CustomerAddress,
    OrderType OrderType, DiscountKind DiscountKind, string? SeniorIdNumber, decimal? ExpectedTotal);
public sealed record ReasonInput(Guid Id, string Reason);
public sealed record ReceiveInput(Guid ProductId, decimal Quantity, decimal UnitCost, string Notes, string? PaidFromAccount,
    StockReceiptKind ReceiptKind, bool ZeroCostConfirmed);
public sealed record AdjustInput(Guid ProductId, decimal Delta, string Reason, string? Counterpart, bool ZeroCostConfirmed = false);
public sealed record HoldInput(Guid BranchId, List<HeldCartLine> Cart, string? CustomerName, string? Invoice, string? Notes,
    string? CustomerAddress, PaymentMethod PaymentMethod, OrderType OrderType, DiscountKind DiscountKind, decimal DiscountAmount, string? SeniorIdNumber);
public sealed record InvoiceInput(Guid BranchId, string Invoice);
public sealed record DailyInput(DateTime Date, Guid BranchId, Guid? CashierId, PaymentMethod? Payment, string Search);
public sealed record ScopeInput(ReportScope Scope, bool Summary = false, bool AdminOnly = false);
public sealed record CashReportInput(Guid BranchId, DateOnly From, DateOnly To, CashMovementType? Type,
    CashMovementCategory? Category, string Search, Guid? ShiftId);
public sealed record AuditInput(DateTime From, DateTime To);
public sealed record AuditChoices(string[] Actions, string[] Usernames);
public sealed record ClientEventInput(string Event, string Detail);
public sealed record TechnicalProvisionInput(string Username, string FullName, string Password, string Confirmation, string Company = "");
public sealed record ServiceStatusInfo(bool Configured, string DatabasePath, string Company, bool TechnicalAccountExists, Guid InstallationId, string RecoveryKeyId);
public sealed record AccountRecoveryRequest(int Format, Guid InstallationId, Guid AccountId, string Username, string Company, string KeyId, string Challenge);
public sealed record AccountRecoveryResponse(int Format, string Challenge, string Signature);
public sealed record AccountRecoveryInput(AccountRecoveryResponse Authorization, string TemporaryPassword, string Confirmation);
public sealed record UsernameInput(string Username);
public sealed record TrialStartInput(Guid LicenseId, string ActivationKey);
public sealed record BackupPhraseState(bool Configured, string Version);
public sealed record VerifyBackupPhraseInput(string Current, string Version);
public sealed record ChangeBackupPhraseInput(string Current, string Next, string Confirmation, string Version);
public sealed record CreateBackupInput(bool Automatic = false);
public sealed record BackupDownload(Guid TransferId, long Length, string Sha256, DateTimeOffset CreatedUtc);
public sealed record BackupChunkInput(Guid TransferId, long Offset);
public sealed record BackupChunk(long Offset, byte[] Bytes);
public sealed record RestoreUploadInput(long Length, string Sha256);
public sealed record RestoreChunkInput(Guid TransferId, long Offset, byte[] Bytes);
public sealed record PrepareRestoreInput(Guid TransferId, string Passphrase);
public sealed record RestorePreview(Guid TransferId, string Company, int Users, int Sales, string SourceSha256);
