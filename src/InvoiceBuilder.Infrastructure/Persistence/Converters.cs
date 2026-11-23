using InvoiceBuilder.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InvoiceBuilder.Infrastructure.Persistence;

public sealed class CurrencyCodeConverter()
    : ValueConverter<CurrencyCode, string>(v => v.Code, v => CurrencyCode.Create(v).Value);

public sealed class HsCodeConverter()
    : ValueConverter<HsCode, string>(v => v.Value, v => HsCode.Create(v).Value);

public sealed class DocumentNumberConverter()
    : ValueConverter<DocumentNumber, string>(v => v.Value, v => DocumentNumber.Create(v).Value);

public sealed class TaxPercentConverter()
    : ValueConverter<TaxPercent, decimal>(v => v.Value, v => TaxPercent.Create(v).Value);
