---
description: "DbContext, configurations, unique indexes, repositories, unit of work, migration and seed data."
---

# 03 · Persistence

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
SQL Server persistence for the spec §6 model with tenant/business filters.

## Do
1. `Infrastructure/Persistence/AppDbContext` (inherits `IdentityDbContext<AppUser>`), one `IEntityTypeConfiguration` per entity; decimal precisions per R10; JSON columns for snapshots, TemplateDefinition and Theme tokens.
2. Every unique index from spec §6, including the filtered ones (Document number where not null; Payment ProviderReference where not null).
3. Global query filters on `TenantId` and (where present) `BusinessId` via `ICurrentTenant` / `ICurrentBusiness` (interfaces in Application, implemented in Web later; use a fixed dev tenant for now).
4. `RowVersion` concurrency tokens; `CreatedAt`/`UpdatedAt` set by a `SaveChanges` interceptor using `TimeProvider`.
5. Repository interfaces in Application (`IDocumentRepository`, `IClientRepository`, `IPaymentRepository`, `ITemplateRepository`, `IBusinessRepository`, `ICatalogueRepository`, `IRecurringRepository`) and `IUnitOfWork` (`SaveChangesAsync`, `BeginTransactionAsync`). Implementations in Infrastructure.
6. `NumberSequenceRepository.AllocateAsync(businessId, type)` uses `UPDLOCK, ROWLOCK` inside the caller's transaction (R6).
7. Migration `Initial`, exported to `db/migrations/0001_initial.sql` (idempotent).
8. `Instalment` configuration (unique DocumentId + Position, amount decimal(18,2)) per spec §6.
9. Dev seed (Development only): one tenant, business "Troika Design Workshop" (USD, Net 14, no tax), number sequences per spec R6 defaults, 5 clients and 6 catalogue items from the canvas sample data, Troika Amber theme (design-system §2.3), and one issued invoice INV-0150 for $2,400.00 with a 3 × $800.00 monthly plan, first instalment paid.

## Tests first
Integration test (Testcontainers): 20 parallel `AllocateAsync` calls produce 1–20 with no gaps or duplicates (R6). Query filter test: business A can't read business B's clients.

## Done when
Migration applies to a fresh container, tests pass, and seed data is visible in SQL.
