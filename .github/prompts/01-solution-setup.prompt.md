---
description: "Create the solution, projects, central package management and DI entry points."
---

# 01 · Solution setup

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
An empty, building Clean Architecture solution (copilot-instructions "Stack").

## Do
1. Create `InvoiceBuilder.sln` with `src/InvoiceBuilder.Domain`, `src/InvoiceBuilder.Application`, `src/InvoiceBuilder.Infrastructure`, `src/InvoiceBuilder.Web` (Blazor Server, interactive server render mode only, plus MVC controllers), and `tests/InvoiceBuilder.Domain.Tests`, `tests/InvoiceBuilder.Application.Tests`, `tests/InvoiceBuilder.Integration.Tests`.
2. Project references: Application → Domain; Infrastructure → Application; Web → Application + Infrastructure; tests reference what they test.
3. `Directory.Build.props`: `net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`, `AnalysisLevel latest-recommended`.
4. `Directory.Packages.props` (central package management) with exactly: MediatR, FluentValidation, FluentValidation.DependencyInjectionExtensions, Microsoft.EntityFrameworkCore.SqlServer, Microsoft.EntityFrameworkCore.Design, Microsoft.AspNetCore.Identity.EntityFrameworkCore, MudBlazor, Hangfire.Core, Hangfire.SqlServer, Hangfire.AspNetCore, Serilog.AspNetCore, Serilog.Sinks.File, Microsoft.Playwright, QRCoder, Microsoft.Extensions.Http.Resilience, xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, Shouldly, NSubstitute, Testcontainers.MsSql, Microsoft.AspNetCore.Mvc.Testing, Microsoft.Extensions.TimeProvider.Testing, UglyToad.PdfPig. Use current stable versions. For MediatR, stop and ask which version (spec §8).
5. `.editorconfig` (file-scoped namespaces, `var` when obvious, 4 spaces, sorted usings) and `.gitignore` for .NET.
6. DI entry points: `Application/DependencyInjection.cs` (`AddApplication()`), `Infrastructure/DependencyInjection.cs` (`AddInfrastructure(IConfiguration)`), both called from `Web/Program.cs`. Add Serilog bootstrap logging in `Program.cs`.
7. One placeholder test per test project so `dotnet test` runs.

## Done when
`dotnet build` has no warnings, `dotnet test` passes, and the Web app starts and shows the default page.
