---
description: "Test conventions"
applyTo: "tests/**"
---

- xUnit + Shouldly + NSubstitute. One behaviour per test.
- Name tests after the rule they prove: `R6_numbers_are_gap_free_under_concurrency`, `R17_partial_allocation_sets_part_paid`.
- Domain and Application tests use no database: use fakes and `FakeTimeProvider`.
- Integration tests use Testcontainers SQL Server via a shared `DatabaseFixture`, plus `WebApplicationFactory<Program>` for controllers.
- PDF tests run Playwright Chromium and read output text with PdfPig. Mark them `[Trait("Category","Pdf")]`.
- Money assertions compare `decimal` exactly. Never use tolerance.
- No sleeps. Use `FakeTimeProvider` and Hangfire job methods called directly.
