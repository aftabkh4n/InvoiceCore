using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace InvoiceCore;

/// <summary>
/// Default implementation of <see cref="IInvoiceService"/>. Stateless apart from the
/// injected <see cref="TimeProvider"/> used for overdue detection.
/// Construct with <c>new InvoiceService()</c>. No DI framework required.
/// </summary>
public sealed class InvoiceService : IInvoiceService
{

    private readonly TimeProvider _clock;

    /// <summary>Initialises an <see cref="InvoiceService"/>.</summary>
    /// <param name="clock">Time source for overdue detection. Defaults to <see cref="TimeProvider.System"/>.</param>
    public InvoiceService(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public Invoice Create(CreateInvoiceRequest request)
    {
        var result = InvoiceValidator.Validate(request);
        if (!result.IsValid)
            throw new InvoiceValidationException(result);

        var currencyCode = (request.CurrencyCode ?? "USD").Trim().ToUpperInvariant();
        return new Invoice(
            request.LineItems,
            request.TaxRates,
            request.InvoiceNumber.Trim(),
            request.IssuedDate,
            request.DueDate,
            request.Customer!,
            currencyCode,
            request.TaxMode,
            request.DiscountPercent,
            request.Notes,
            request.Id == Guid.Empty ? null : request.Id,
            request.BaseCurrencyCode,
            request.ExchangeRate);
    }

    /// <inheritdoc/>
    public Invoice UpdateStatus(Invoice invoice, InvoiceStatus status) => invoice.UpdateStatus(status);

    /// <inheritdoc/>
    public ValidationResult Validate(CreateInvoiceRequest request) => InvoiceValidator.Validate(request);

    /// <inheritdoc/>
    public string ExportToJson(Invoice invoice, JsonExportOptions? options = null)
    {
        var opts = options ?? new JsonExportOptions();
        if (opts.MoneyFormat == MoneyFormat.String)
        {
            var dto = InvoiceStringDto.From(invoice);
            var ti = opts.IncludeNulls
                ? InvoiceStringJsonContextWithNulls.Default.InvoiceStringDto
                : InvoiceStringJsonContext.Default.InvoiceStringDto;
            return opts.Indented ? SerializeIndented(dto, ti) : JsonSerializer.Serialize(dto, ti);
        }

        var numDto = InvoiceDto.From(invoice);
        var numTi = opts.IncludeNulls
            ? InvoiceJsonContext.Default.InvoiceDto
            : InvoiceJsonContextNoNulls.Default.InvoiceDto;
        return opts.Indented ? SerializeIndented(numDto, numTi) : JsonSerializer.Serialize(numDto, numTi);
    }

    /// <inheritdoc/>
    public string ExportToJson(IEnumerable<Invoice> invoices, JsonExportOptions? options = null)
    {
        var opts = options ?? new JsonExportOptions();
        if (opts.MoneyFormat == MoneyFormat.String)
        {
            var dtos = invoices.Select(InvoiceStringDto.From).ToArray();
            var ti = opts.IncludeNulls
                ? InvoiceStringJsonContextWithNulls.Default.InvoiceStringDtoArray
                : InvoiceStringJsonContext.Default.InvoiceStringDtoArray;
            return opts.Indented ? SerializeIndented(dtos, ti) : JsonSerializer.Serialize(dtos, ti);
        }

        var numDtos = invoices.Select(InvoiceDto.From).ToArray();
        var numTi = opts.IncludeNulls
            ? InvoiceJsonContext.Default.InvoiceDtoArray
            : InvoiceJsonContextNoNulls.Default.InvoiceDtoArray;
        return opts.Indented ? SerializeIndented(numDtos, numTi) : JsonSerializer.Serialize(numDtos, numTi);
    }

    private static string SerializeIndented<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        JsonSerializer.Serialize(writer, value, typeInfo);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <inheritdoc/>
    public string ExportToCsv(IEnumerable<Invoice> invoices, CsvExportOptions? options = null)
    {
        var sb = new StringBuilder();
        if (options?.IncludeByteOrderMark == true)
            sb.Append('﻿');

        sb.Append("InvoiceNumber,IssuedDate,DueDate,Status,EffectiveStatus,CustomerName,CustomerEmail,")
          .Append("CustomerTaxNumber,CurrencyCode,TaxMode,Subtotal,DiscountPercent,DiscountAmount,")
          .Append("TaxAmount,Total,BaseCurrencyCode,ExchangeRate,Notes\r\n");

        foreach (var inv in invoices)
        {
            sb.Append(CsvField(inv.InvoiceNumber)).Append(',');
            sb.Append(inv.IssuedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(inv.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(CsvField(inv.Status.ToString())).Append(',');
            sb.Append(CsvField(inv.GetEffectiveStatus(_clock).ToString())).Append(',');
            sb.Append(CsvField(inv.Customer.Name)).Append(',');
            sb.Append(CsvField(inv.Customer.Email)).Append(',');
            sb.Append(CsvField(inv.Customer.TaxNumber)).Append(',');
            sb.Append(CsvField(inv.CurrencyCode)).Append(',');
            sb.Append(CsvField(inv.TaxMode.ToString())).Append(',');
            sb.Append(inv.Subtotal.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(inv.DiscountPercent.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(inv.DiscountAmount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(inv.TaxAmount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(inv.Total.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(CsvField(inv.BaseCurrencyCode)).Append(',');
            sb.Append(inv.ExchangeRate?.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(CsvField(inv.Notes)).Append("\r\n");
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public string ExportLineItemsToCsv(IEnumerable<Invoice> invoices, CsvExportOptions? options = null)
    {
        var sb = new StringBuilder();
        if (options?.IncludeByteOrderMark == true)
            sb.Append('﻿');

        sb.Append("InvoiceNumber,LineNumber,Description,Quantity,UnitPrice,DiscountPercent,LineTotal\r\n");

        foreach (var inv in invoices)
        {
            for (var i = 0; i < inv.LineItems.Count; i++)
            {
                var line = inv.LineItems[i];
                sb.Append(CsvField(inv.InvoiceNumber)).Append(',');
                sb.Append(i + 1).Append(',');
                sb.Append(CsvField(line.Description)).Append(',');
                sb.Append(line.Quantity.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(line.UnitPrice.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(line.DiscountPercent.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(line.Total.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            }
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public IEnumerable<Invoice> GetOverdue(IEnumerable<Invoice> invoices)
        => invoices.Where(inv => inv.IsOverdue(_clock));

    /// <inheritdoc/>
    public InvoiceSummary GetSummary(IEnumerable<Invoice> invoices)
    {
        var list = invoices.ToList();

        var countByStatus = list
            .GroupBy(inv => inv.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        var nonCancelled = list.Where(inv => inv.Status != InvoiceStatus.Cancelled).ToList();

        var byCurrency = nonCancelled
            .GroupBy(inv => inv.CurrencyCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => new CurrencyTotals
                {
                    Total = g.Sum(inv => inv.Total),
                    Paid = g.Where(inv => inv.Status == InvoiceStatus.Paid).Sum(inv => inv.Total),
                    Outstanding = g.Where(inv => inv.Status == InvoiceStatus.Sent).Sum(inv => inv.Total),
                    Overdue = g.Where(inv => inv.IsOverdue(_clock)).Sum(inv => inv.Total),
                    TaxAmount = g.Sum(inv => inv.TaxAmount),
                },
                StringComparer.OrdinalIgnoreCase);

        BaseCurrencyTotals? rollup = null;
        if (nonCancelled.Count > 0
            && nonCancelled.All(inv => inv.BaseCurrencyCode is not null && inv.ExchangeRate.HasValue)
            && nonCancelled.Select(inv => inv.BaseCurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
        {
            var baseCurrency = nonCancelled[0].BaseCurrencyCode!;
            rollup = new BaseCurrencyTotals
            {
                CurrencyCode = baseCurrency,
                Total = nonCancelled.Sum(inv => inv.Total * inv.ExchangeRate!.Value),
                Paid = nonCancelled.Where(inv => inv.Status == InvoiceStatus.Paid)
                                   .Sum(inv => inv.Total * inv.ExchangeRate!.Value),
                Outstanding = nonCancelled.Where(inv => inv.Status == InvoiceStatus.Sent)
                                          .Sum(inv => inv.Total * inv.ExchangeRate!.Value),
                Overdue = nonCancelled.Where(inv => inv.IsOverdue(_clock))
                                      .Sum(inv => inv.Total * inv.ExchangeRate!.Value),
                TaxAmount = nonCancelled.Sum(inv => inv.TaxAmount * inv.ExchangeRate!.Value),
            };
        }

        return new InvoiceSummary
        {
            Count = list.Count,
            CountByStatus = countByStatus,
            ByCurrency = byCurrency,
            Rollup = rollup,
        };
    }

    private static string CsvField(string? value)
    {
        if (value is null) return "";
        if (value.IndexOfAny([',', '"', '\r', '\n']) >= 0)
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
