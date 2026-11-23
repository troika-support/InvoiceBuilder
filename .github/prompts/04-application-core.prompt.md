---
description: "MediatR pipeline, validation, clock, audit log, formatting helpers, options."
---

# 04 · Application core

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Cross-cutting Application plumbing every feature uses.

## Do
1. MediatR registration plus pipeline behaviours: `ValidationBehaviour` (FluentValidation → `Result` failure with field errors), `LoggingBehaviour`, `UnitOfWorkBehaviour` (commits for commands marked `ITransactionalCommand`).
2. `ICurrentUser`, `ICurrentTenant`, `ICurrentBusiness` interfaces; `IActivityLog.AddAsync(entityType, entityId, action, summary)` writing `ActivityEntry` (R33), plus a domain-event dispatcher that logs Issued, Viewed, Accepted and PaymentConfirmed.
3. `IDisplayFormatter` implementing design-system §5.2 exactly (dates in Africa/Harare, `$2,400.00`, `ZiG 18,200.00` for currency code `ZWG`, "N days late"), with at least `Date(DateTimeOffset)`, `ShortDate(DateTimeOffset)`, `Time(DateTimeOffset)`, `Money(decimal amount, string currency, bool showCurrency = true)` and `DaysLate(int days)` (the theme kit's `MoneyText` calls `Money`).
4. Options: `AppOptions` (PublicBaseUrl, TimeZone = Africa/Harare), `StorageOptions` (FilesRoot), `PdfOptions` (PoolSize = 2, TimeoutSeconds = 20) with `ValidateOnStart`.
5. `IFileStore` (Save/Open/Delete by key) and a local-disk implementation under `StorageOptions.FilesRoot`.
6. `IFiscalisationService` (`QueueAsync(Guid documentId, CancellationToken)`) with `NoOpFiscalisationService` (R32). Phase 2 (prompt 29) replaces the implementation; keep the interface as is.

## Tests first
Formatter tests for every row of design-system §5.2; validation behaviour returns field errors without calling the handler.

## Done when
Tests pass; app starts with options validated (missing PublicBaseUrl fails fast in Production).
