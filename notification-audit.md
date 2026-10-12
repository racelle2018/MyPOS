# Action notification audit

Reviewed October 12, 2026. Scope: Desktop action feedback, not an accounting or production-data audit.

## Shared policy

`ActionDialog` routes normal UI messages to `NotificationDialog`. The dialog follows the selected appearance font/preset, has close-only window controls, wraps and scrolls long text, and keeps its buttons visible. Active visible windows own notifications; a hidden main window is not used during session lock. Confirmations preserve their result choices and default to No/Cancel. Startup or failed theme/dialog loading can fall back to a native message box.

Custom dialogs play the Windows sound scheme once on opening: Success/Information → Asterisk, Warning → Exclamation, Error → Hand, Confirmation → Question. Muting or disabling these events in Windows is respected; an audio failure never prevents the dialog. The native fallback supplies its own sound, avoiding an extra custom playback.

| Type | Use |
| --- | --- |
| Success | Completed save/export/approved action. Printing says sent to the printer, not physically printed. |
| Warning | Correctable input, permissions, stale state, unsupported printer paper or partial backup success. |
| Error | Failed file, backup, print or unexpected operation. |
| Information | No changes, empty results, preview/reset selection that has not been saved. |
| Confirmation | Destructive/discard/restore/close-shift decisions, retaining Yes/No/Cancel semantics. |

## Reviewed and updated areas

| Area | Feedback now provided |
| --- | --- |
| Appearance | Save success after persistence/application; explicit Preview/Reset information; validation warning. Selection remains local until Save. |
| Settings | Save success or no-change information; invalid low-stock threshold warning. |
| Drawer Access / Cash Counting | Save success with effective-scope wording; invalid choices/access warnings. |
| Backup & Restore | Preference save/no-change; verified backup success, partial-copy warning or backup error; destructive confirmation and restore-staged success, not a claim that restore already completed. |
| Products | Add/edit, receive-stock and activate/deactivate success; submit validation and selection/access warnings; activation confirmation. |
| Users / password change | Add/edit, activate/deactivate and initial-password-change success; input/access warnings and activation confirmation. |
| Shift / cash movements | Open/close/record/approve success; validation/stale-state failures; close-shift confirmation. Opening-float entry alone does not claim the shift has been saved. |
| POS / held sales | Shift opening, clear/hold/recall/discard results; blocked payment warnings; existing sale-completion dialog retained as the saved-sale result. Cart edits and scan feedback remain inline. |
| Reports | All report CSV export success/error paths; PDF save success/error; print submission success, unsupported-paper warning and print error. File-picker cancellation has no success message. |
| Receipt preview | Print-submission success or print error; saved-sale/retry context retained. Thermal payload and financial values unchanged. |
| Login / lock / navigation / permissions | Existing warning/confirmation messages routed through the shared dialog; authentication, access rules and session hiding unchanged. |
| Unexpected errors | Existing logged UI exception path uses Error/Warning; native fallback remains available if notification UI cannot load. |

## Deliberately retained inline

Unsaved changes, missing-font/printer configuration hints, backup status, report/filter status and empty states, row counts, current totals, denomination variance, typing/scan/cart-item feedback, and field-location validation markers. These are ongoing context rather than completed-action notifications. Errors raised during an explicit submit also show a dialog; keeping a nearby marker helps locate the field afterward. Avoid duplicate generic success dialogs after checkout because Sale Complete already communicates the saved invoice and receipt status.

## Verification and limits

Latest solution regression: 79 passing tests (60 Core, 19 Desktop). UI tests use an in-memory database and explicitly suppress live startup/backup work while modal tests pump the dispatcher.

Regression coverage checks notification severity for saves/validation, all dialog kinds, close-only styling, button results and safe confirmation defaults, plus existing screen, session-lock, permission, audit-log and output tests. Rendered notification samples are checked separately. File pickers and real printer/backup hardware workflows still need interactive acceptance; no production database, physical printer or external backup destination was exercised by this audit. Existing persisted business audit logs remain unchanged; showing a notification does not itself create a business audit record.
