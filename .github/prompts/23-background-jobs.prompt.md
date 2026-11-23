---
description: "Hangfire setup, recurring schedules (S9), reminders (incl. per instalment), quote expiry, payment polling."
---

# 23 · Background jobs and recurring invoices

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Spec R7, R29–R31, paynow.md §3 step 6, screen S9 (design-system §8.9).

## Do
1. Hangfire with SQL Server storage, queues `default`, `pdf`, `messages`; dashboard at `/ops/jobs` (Owner only).
2. Recurring jobs (Africa/Harare): `RecurringInvoicesJob` daily 06:00, `RemindersJob` daily 08:00, `ExpireQuotesJob` daily 00:15, `PollPendingPaymentsJob` every 2 minutes.
3. All jobs idempotent (R31): run-date keys, `ReminderLog` unique index, schedule `NextRunDate` advanced in the same transaction.
3a. `RemindersJob` handles plans per R39: offsets per instalment, one combined message per invoice per day naming each instalment.
4. `/recurring` list and editor drawer (cadence, start, end/count, Draft vs Auto issue, Paused); "Make recurring" action on issued invoices.
5. Owner notifications for recurring drafts ready (in-app + email).

## Tests first
Running each job twice on the same `FakeTimeProvider` date creates nothing extra; two instalments hitting reminder offsets on the same day → one combined reminder; monthly cadence on 31 Jan → 28/29 Feb → 31 Mar rule (use last day of month when the day doesn't exist).

## Done when
Jobs appear in the dashboard; a monthly schedule produces one draft per month.
