using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Documents;

public sealed record LineCalcInput(decimal Quantity, decimal UnitPrice, decimal LineDiscount, decimal TaxPercent);

public sealed record LineCalcResult(decimal Amount, decimal LineDiscount, decimal AllocatedDiscount, decimal TaxableAmount, decimal Tax);

public sealed record DocumentTotals(
    IReadOnlyList<LineCalcResult> Lines,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal Total);

/// <summary>Line amounts, discount pro-rating and tax (R10, R13, R14) and due dates (R9). Pure.</summary>
public static class DocumentCalculator
{
    public static decimal LineAmount(decimal quantity, decimal unitPrice) =>
        Round(quantity * unitPrice);

    public static DateOnly DueDate(DateOnly issueDate, int termsDays) => issueDate.AddDays(termsDays);

    /// <summary>
    /// Subtotal is the sum of line amounts. Discount total is line discounts plus the document discount.
    /// The document discount is applied to each line after its own line discount and before tax (R14);
    /// tax is rounded per line, then summed (R10, R13).
    /// </summary>
    public static Result<DocumentTotals> Calculate(IReadOnlyList<LineCalcInput> lines, Discount discount)
    {
        var amounts = new decimal[lines.Count];
        var nets = new decimal[lines.Count];

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Quantity <= 0m || line.UnitPrice < 0m)
            {
                return Result.Failure<DocumentTotals>(DocumentErrors.InvalidLine("Quantity must be above zero and the unit price cannot be negative."));
            }

            amounts[i] = LineAmount(line.Quantity, line.UnitPrice);
            if (line.LineDiscount < 0m || line.LineDiscount > amounts[i])
            {
                return Result.Failure<DocumentTotals>(DocumentErrors.InvalidLine("A line discount must be between zero and the line amount."));
            }

            nets[i] = amounts[i] - line.LineDiscount;
        }

        var allocated = Allocate(nets, discount);
        if (allocated.IsFailure)
        {
            return Result.Failure<DocumentTotals>(allocated.Error);
        }

        var results = new List<LineCalcResult>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var taxable = nets[i] - allocated.Value[i];
            var tax = Round(taxable * lines[i].TaxPercent / 100m);
            results.Add(new LineCalcResult(amounts[i], lines[i].LineDiscount, allocated.Value[i], taxable, tax));
        }

        var subtotal = results.Sum(r => r.Amount);
        var discountTotal = results.Sum(r => r.LineDiscount + r.AllocatedDiscount);
        var taxTotal = results.Sum(r => r.Tax);

        return Result.Success(new DocumentTotals(results, subtotal, discountTotal, taxTotal, subtotal - discountTotal + taxTotal));
    }

    private static Result<decimal[]> Allocate(decimal[] nets, Discount discount)
    {
        var allocation = new decimal[nets.Length];
        var baseTotal = nets.Sum();

        switch (discount.Kind)
        {
            case DiscountKind.None:
                return Result.Success(allocation);

            case DiscountKind.Percent:
                for (var i = 0; i < nets.Length; i++)
                {
                    allocation[i] = Round(nets[i] * discount.Value / 100m);
                }

                return Result.Success(allocation);

            case DiscountKind.Fixed:
                if (discount.Value > baseTotal)
                {
                    return Result.Failure<decimal[]>(DocumentErrors.DiscountTooLarge);
                }

                if (baseTotal == 0m || nets.Length == 0)
                {
                    return Result.Success(allocation);
                }

                var allocatedSoFar = 0m;
                for (var i = 0; i < nets.Length - 1; i++)
                {
                    allocation[i] = Round(discount.Value * nets[i] / baseTotal);
                    allocatedSoFar += allocation[i];
                }

                allocation[^1] = discount.Value - allocatedSoFar;
                return Result.Success(allocation);

            default:
                return Result.Failure<decimal[]>(DocumentErrors.DiscountTooLarge);
        }
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
