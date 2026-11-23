---
description: "Development-only fake gateway and a simulator page to drive payments end to end."
---

# 07 · Fake payment gateway and simulator

Use **Agent mode**. Follow `.github/copilot-instructions.md`.

## Goal
Develop and demo payments without Paynow (`paynow.md` §8).

## Do
1. `FakePaymentGateway : IPaymentGateway` registered when `Payments:UseFake = true` (Development only; startup fails if true in Production).
2. Dev page `/dev/payments` (Owner only): lists Pending payments with buttons Simulate paid, Simulate cancelled, Simulate paid after 30 s. These post a correctly hashed form to the real webhook controller.
3. Fake `CheckStatusAsync` returns whatever was simulated.

## Done when
From the dev page, simulating Paid confirms the payment and creates a receipt through the same code path as Paynow.
