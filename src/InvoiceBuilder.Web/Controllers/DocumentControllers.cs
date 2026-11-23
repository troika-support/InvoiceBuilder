using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Documents;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceBuilder.Web.Controllers;

/// <summary>Staff PDF download: stored PDF for issued documents, on-demand render for drafts.</summary>
[ApiController]
[Authorize(Policy = "Staff")]
[Route("api/documents")]
public sealed class DocumentPdfController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, [FromQuery] Guid? templateVersionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDocumentPdfQuery(id, templateVersionId), cancellationToken);
        return result.IsFailure ? NotFound() : File(result.Value.Content, "application/pdf", result.Value.FileName);
    }
}

/// <summary>Public client view (R26): the token grants access to exactly one issued document.</summary>
[ApiController]
[AllowAnonymous]
[Route("d")]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(ClientLinkController.RateLimitPolicy)]
public sealed class ClientLinkController(
    ISender sender,
    IDocumentRepository documents,
    IScopeOverride scope,
    IDocumentHtmlService html,
    IPaymentRepository payments,
    IPaymentGatewayConfigRepository gateways,
    IClientRepository clients,
    IBusinessRepository businesses,
    ITemplateRepository templates) : ControllerBase
{
    public const string RateLimitPolicy = "client-link";

    private const string PanelCss = "<style>.ib-pill{display:inline-block;padding:2px 10px;border:2px solid #111111;border-radius:999px;font-weight:600}.ib-btn2{background:#FFFFFF!important;color:#111111!important;border:2px solid #111111!important}.ib-panel{font-family:system-ui,sans-serif;max-width:40rem;margin:1.5rem auto;padding:1rem;background:#FFFFFF;color:#111111;border:1px solid #E7E5E4;border-radius:12px}.ib-panel h2{font-size:1.25rem;margin:0 0 .75rem}.ib-panel h3{font-size:1rem;margin:1rem 0 .5rem}.ib-panel label{display:block;margin:.5rem 0;color:#44403C}.ib-field{box-sizing:border-box;width:100%;min-height:44px;padding:.5rem .75rem;border:1px solid #6B6B6B;border-radius:8px;font:inherit}.ib-btn{min-height:44px;width:100%;margin-top:.75rem;padding:.5rem 1rem;border:0;border-radius:8px;background:#F59E0B;color:#111111;font:inherit;font-weight:600;cursor:pointer}.ib-btn:focus-visible,.ib-field:focus-visible{outline:3px solid #111111;outline-offset:2px}.ib-panel input[type=radio],.ib-panel input[type=checkbox]{width:1.25rem;height:1.25rem;margin-right:.5rem;vertical-align:middle}.ib-panel ul{padding-left:1.25rem}@media (prefers-reduced-motion:reduce){*{transition:none!important}}</style>";

    private string themedCss = PanelCss;

    private string? _nonce;

    /// <summary>Per-response nonce for the few inline scripts on client pages; nothing else may run.</summary>
    private string Nonce => _nonce ??= Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));

    /// <summary>
    /// Client pages are public: no framing (clickjacking on Accept/Pay), no external loads, and only our own
    /// nonce-tagged scripts. Inline styles, data: fonts and images are what the renderer produces.
    /// </summary>
    private void ApplyClientPageHeaders()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Frame-Options"] = "DENY";
        Response.Headers.ContentSecurityPolicy =
            "default-src 'none'; img-src data:; font-src data:; style-src 'unsafe-inline'; script-src 'nonce-" + Nonce + "'; "
            + "connect-src 'self'; frame-src 'self'; base-uri 'none'; frame-ancestors 'none'";
    }

    private static bool IsHex(string? value) =>
        value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

    private async Task ApplyThemeAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var version = await templates.GetVersionAsync(versionId, cancellationToken);
        var template = version is null ? null : await templates.GetByIdAsync(version.TemplateId, cancellationToken);
        var theme = template is null ? null : await templates.GetThemeAsync(template.ThemeId, cancellationToken);
        if (theme?.Tokens is not { Length: > 0 } json)
        {
            return;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            string? Get(string name) => doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && doc.RootElement.TryGetProperty(name, out var p) && p.ValueKind == System.Text.Json.JsonValueKind.String ? p.GetString() : null;
            var ink = Get("ink");
            var accent = Get("accent");
            var css = PanelCss;
            if (IsHex(ink))
            {
                css = css.Replace("#111111", ink, StringComparison.Ordinal);
            }

            if (IsHex(accent))
            {
                css = css.Replace("#F59E0B", accent, StringComparison.Ordinal);
            }

            themedCss = css;
        }
        catch (System.Text.Json.JsonException)
        {
        }
    }

    private async Task<string> BuildHeaderAsync(InvoiceBuilder.Domain.Documents.Document document, CancellationToken cancellationToken)
    {
        var business = await businesses.GetByIdAsync(document.BusinessId, cancellationToken);
        var isInvoice = document.Type == InvoiceBuilder.Domain.Documents.DocumentType.Invoice;
        var clientName = (await clients.GetByIdAsync(document.ClientId, cancellationToken))?.Name;
        var title = (isInvoice ? "Invoice " : "Quote ") + document.Number + (!isInvoice && !string.IsNullOrWhiteSpace(clientName) ? " for " + clientName : string.Empty);
        var amount = isInvoice ? string.Empty : "<p style=\"margin:0 0 .5rem;font:600 36px ui-monospace,Consolas,monospace\">" + document.Total.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) + " " + document.Currency + "</p>";
        var date = isInvoice ? document.DueDate : document.ValidUntil;
        var pill = date is { } d ? "<span class=\"ib-pill\">" + (isInvoice ? "Due " : "Valid until ") + d.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) + "</span>" : string.Empty;
        var logo = business?.Logo is { Length: > 0 } && business.LogoContentType is { } ct && ct.StartsWith("image/", StringComparison.Ordinal)
            ? "<img alt=\"\" height=\"40\" style=\"display:block;margin-bottom:.5rem\" src=\"data:" + System.Net.WebUtility.HtmlEncode(ct) + ";base64," + Convert.ToBase64String(business.Logo) + "\">"
            : string.Empty;
        return "<header class=\"ib-panel\">" + logo + "<p style=\"margin:0;font-weight:700\">" + System.Net.WebUtility.HtmlEncode(business?.TradingName ?? string.Empty) + "</p><h1 style=\"margin:.25rem 0 .5rem;font-size:1.5rem\">" + System.Net.WebUtility.HtmlEncode(title) + "</h1>" + amount + pill + "</header>";
    }

    private async Task<string> BuildLinksPanelAsync(InvoiceBuilder.Domain.Documents.Document document, string token, CancellationToken cancellationToken)
    {
        var links = "<a class=\"ib-btn\" style=\"display:block;box-sizing:border-box;text-align:center;text-decoration:none;line-height:28px\" href=\"/d/" + Uri.EscapeDataString(token) + "/pdf\">Save PDF</a>";
        var digits = new string(((await businesses.GetByIdAsync(document.BusinessId, cancellationToken))?.WhatsAppNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length > 0)
        {
            var text = Uri.EscapeDataString("Question about " + document.Number);
            links += "<a class=\"ib-btn ib-btn2\" style=\"display:block;box-sizing:border-box;text-align:center;text-decoration:none;line-height:28px\" href=\"https://wa.me/" + digits + "?text=" + text + "\">Ask a question</a>";
        }

        return "<section class=\"ib-panel\">" + links + "</section>";
    }

    private string BuildShell(InvoiceBuilder.Domain.Documents.Document document, string renderedHtml, string header)
    {
        var size = System.Text.RegularExpressions.Regex.Match(renderedHtml, @"@page\{size:([\d.]+)mm ([\d.]+)mm", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));
        var width = size.Success ? size.Groups[1].Value : "210";
        var height = size.Success ? size.Groups[2].Value : "297";
        var frame = "<div style=\"max-width:100%;overflow-x:auto;margin:1.5rem 0\"><iframe title=\"Document preview\" sandbox=\"allow-same-origin\" srcdoc=\""
            + System.Net.WebUtility.HtmlEncode(renderedHtml)
            + "\" style=\"display:block;border:0;background:#FFFFFF;box-shadow:0 1px 3px rgba(0,0,0,.2);margin:0 auto;max-width:100%;width:" + width + "mm;height:" + height + "mm\""
            + " data-autosize></iframe></div>"
            + "<script nonce=\"" + Nonce + "\">document.querySelectorAll('iframe[data-autosize]').forEach(function(f){function fit(){try{f.style.height=f.contentDocument.documentElement.scrollHeight+'px'}catch(e){}}f.addEventListener('load',fit);fit();});</script>";
        return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>"
            + System.Net.WebUtility.HtmlEncode(document.Number?.ToString() ?? string.Empty)
            + "</title></head><body style=\"margin:0;padding:1rem;background:#F5F5F4\">" + themedCss + header + frame + "</body></html>";
    }

    private static string AddPanel(string pageHtml, string panel)
    {
        var index = pageHtml.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return index < 0 ? pageHtml + panel : pageHtml.Insert(index, panel);
    }

    private string InjectPanel(string pageHtml, string panel)
    {
        if (!pageHtml.Contains("name=\"viewport\"", StringComparison.OrdinalIgnoreCase))
        {
            var head = pageHtml.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            if (head >= 0)
            {
                pageHtml = pageHtml.Insert(head, "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            }
        }

        var index = pageHtml.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        var block = themedCss + panel + "<p style=\"text-align:center;color:#44403C;font-family:system-ui,sans-serif\">Sent with Invoice Builder</p>";
        return index < 0 ? pageHtml + block : pageHtml.Insert(index, block);
    }

    private static string AppendQuoteActions(string pageHtml, InvoiceBuilder.Domain.Documents.Document document, string token, string businessName)
    {
        var status = document.Status;
        var open = document.Type == InvoiceBuilder.Domain.Documents.DocumentType.Quote
            && (status == InvoiceBuilder.Domain.Documents.DocumentStatus.Issued
                || status == InvoiceBuilder.Domain.Documents.DocumentStatus.Sent
                || status == InvoiceBuilder.Domain.Documents.DocumentStatus.Viewed);
        var expired = document.Type == InvoiceBuilder.Domain.Documents.DocumentType.Quote
            && (status == InvoiceBuilder.Domain.Documents.DocumentStatus.Expired
                || (open && document.ValidUntil is { } until && DateOnly.FromDateTime(DateTime.UtcNow) > until));
        if (expired)
        {
            var when = document.ValidUntil is { } vu ? " on " + vu.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
            return AddPanel(pageHtml, "<section class=\"ib-panel\" role=\"status\"><p><strong>This quote expired" + when + ".</strong></p><p>Ask " + businessName + " for an updated one.</p></section>");
        }

        if (!open)
        {
            return pageHtml;
        }

        var planItems = string.Concat(document.Instalments.OrderBy(i => i.Position).Select(i => "<li>Instalment " + i.Position + " &middot; " + i.Amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) + " " + document.Currency
            + (i.DueKind == InvoiceBuilder.Domain.Documents.InstalmentDueKind.OnAcceptance ? " &middot; On acceptance" : i.DueDate is { } dd ? " &middot; Due " + dd.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) : string.Empty) + "</li>"));
        var planCard = planItems.Length > 0 ? "<h2>Payment plan</h2><ul>" + planItems + "</ul>" : string.Empty;
        var action = "/d/" + Uri.EscapeDataString(token);
        var deposit = document.Instalments.OrderBy(i => i.Position).FirstOrDefault(i => i.Position == 1 && i.DueKind == InvoiceBuilder.Domain.Documents.InstalmentDueKind.OnAcceptance);
        var acceptLabel = deposit is null ? "Accept quote" : $"Accept &amp; pay instalment 1 · {deposit.Amount:N2} {document.Currency}";
        var panel = "<section class=\"ib-panel\">" + planCard
            + "<h2>Accept this quote</h2><form method=\"post\" action=\"" + action + "/accept\">"
            + "<label for=\"accept-name\">Your full name</label><br>"
            + "<input id=\"accept-name\" name=\"signerName\" required autocomplete=\"name\" class=\"ib-field\"><br>"
            + "<label><input type=\"checkbox\" required> I accept the scope, price and terms.</label><br>"
            + "<button type=\"submit\" class=\"ib-btn\">" + acceptLabel + "</button></form><p style=\"color:#44403C;font-size:.875rem\">Your name and the time of acceptance are recorded on the quote.</p>"
            + "<h2>Request changes</h2><form method=\"post\" action=\"" + action + "/decline\">"
            + "<label for=\"decline-name\">Your full name</label><br>"
            + "<input id=\"decline-name\" name=\"signerName\" required autocomplete=\"name\" class=\"ib-field\"><br>"
            + "<label for=\"decline-comment\">What should change?</label><br>"
            + "<textarea id=\"decline-comment\" name=\"comment\" rows=\"3\" class=\"ib-field\"></textarea><br>"
            + "<button type=\"submit\" class=\"ib-btn\">Request changes</button></form></section>";
        return AddPanel(pageHtml, panel);
    }

    private async Task<string> AppendPayActionsAsync(string pageHtml, InvoiceBuilder.Domain.Documents.Document document, string token, CancellationToken cancellationToken)
    {
        if (document.Type != InvoiceBuilder.Domain.Documents.DocumentType.Invoice
            || document.Status is InvoiceBuilder.Domain.Documents.DocumentStatus.Draft or InvoiceBuilder.Domain.Documents.DocumentStatus.Void)
        {
            return pageHtml;
        }

        var credited = (await documents.ListCreditNotesAsync(document.Id, cancellationToken)).Sum(c => c.Total);
        var paid = await payments.ConfirmedAllocatedTotalAsync(document.Id, cancellationToken);
        var balance = InvoiceBuilder.Domain.Payments.PaymentAllocator.Balance(document.Total, credited, paid);
        string panel;
        if (balance <= 0m)
        {
            var receiptLinks = string.Concat((await documents.ListReceiptsAsync(document.Id, cancellationToken)).Where(r => r.Number is not null).Select(r => "<p><a href=\"/d/" + Uri.EscapeDataString(token) + "/receipts/" + r.Id + "\">Receipt " + System.Net.WebUtility.HtmlEncode(r.Number!.Value.ToString()) + "</a></p>"));
            panel = "<section class=\"ib-panel\"><p><strong>Paid in full</strong></p>" + receiptLinks + "</section>";
        }
        else if (await gateways.GetForWebhookAsync(document.BusinessId, InvoiceBuilder.Domain.Payments.PaymentProvider.Paynow, document.Currency, cancellationToken) is null)
        {
            return pageHtml;
        }
        else
        {
            var states = document.GetInstalmentStates(paid, credited, DateOnly.FromDateTime(DateTime.UtcNow));
            var next = InvoiceBuilder.Application.Payments.StartClientPaymentHandler.NextInstalmentAmount(states);
            var planRows = string.Concat(states.Select(s => $"<li>Instalment {s.Position} · {s.EffectiveAmount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)} {document.Currency} · {s.Status}</li>"));
            var planHtml = states.Count > 0 ? "<h3>Payment plan</h3><ul>" + planRows + "</ul>" : string.Empty;
            var clientPhone = System.Net.WebUtility.HtmlEncode((await clients.GetByIdAsync(document.ClientId, cancellationToken))?.Phone ?? string.Empty);
            var action = "/d/" + Uri.EscapeDataString(token) + "/pay";
            var nextChoice = next > 0m && next < balance ? "<label><input type=\"radio\" name=\"amountChoice\" value=\"next\" checked> Pay next instalment · " + next.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) + " " + document.Currency + "</label><br>" : string.Empty;
            var fullLabel = "<label><input type=\"radio\" name=\"amountChoice\" value=\"full\"" + (nextChoice.Length == 0 ? " checked" : string.Empty) + "> Pay full balance · " + balance.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) + " " + document.Currency + "</label><br>";
            var otherLabel = "<label><input type=\"radio\" name=\"amountChoice\" value=\"other\"> Other amount</label> <input name=\"amount\" inputmode=\"decimal\" class=\"ib-field\"><br>";
            var choices = nextChoice + fullLabel + otherLabel;
            panel = "<section class=\"ib-panel\"><p style=\"margin:0;color:#44403C\">Amount due</p><p style=\"margin:0 0 .75rem;font:600 36px ui-monospace,Consolas,monospace\">" + balance.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) + " " + document.Currency + "</p><h2>Pay online</h2>" + planHtml + "<form method=\"post\" action=\"" + action + "\">" + choices
                + "<label for=\"pay-method\">Method</label><br><select id=\"pay-method\" name=\"method\" class=\"ib-field\"><option value=\"EcoCash\">EcoCash</option><option value=\"OneMoney\">OneMoney</option><option value=\"InnBucks\">InnBucks</option><option value=\"Card\">Card</option></select><br>"
                + "<label for=\"pay-phone\">Mobile number</label><br><input id=\"pay-phone\" name=\"phone\" type=\"tel\" autocomplete=\"tel\" value=\"" + clientPhone + "\" class=\"ib-field\"><br>"
                + "<button type=\"submit\" class=\"ib-btn\">Pay now</button></form></section>";
        }

        return AddPanel(pageHtml, panel);
    }


    private ContentResult InvalidLink()
    {
        ApplyClientPageHeaders();
        return new ContentResult { StatusCode = StatusCodes.Status404NotFound, ContentType = "text/html; charset=utf-8", Content = "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Link not valid</title></head><body style=\"font-family:system-ui,sans-serif;background:#F5F5F4;color:#111111;margin:0;padding:1rem\"><main style=\"max-width:32rem;margin:2rem auto;padding:1.5rem;background:#FFFFFF;border:1px solid #E7E5E4;border-radius:12px\"><h1>This link isn't valid anymore</h1><p>Please ask the sender for a new link.</p></main><p style=\"text-align:center;color:#44403C\">Sent with Invoice Builder</p></body></html>" };
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> View(string token, CancellationToken cancellationToken)
    {
        var document = await documents.GetByLinkTokenHashAsync(ClientLinkToken.Hash(token), cancellationToken);
        if (document is null || document.Number is null || document.TemplateVersionId is not { } versionId)
        {
            return InvalidLink();
        }

        scope.UseScope(document.TenantId, document.BusinessId);
        await sender.Send(new RecordViewCommand(token), cancellationToken);
        document = await documents.GetByLinkTokenHashAsync(ClientLinkToken.Hash(token), cancellationToken) ?? document;
        var link = $"{Request.Scheme}://{Request.Host}/d/{token}";
        var rendered = await html.RenderAsync(document, versionId, link, cancellationToken);
        if (rendered.IsFailure)
        {
            return NotFound();
        }

        ApplyClientPageHeaders();
        await ApplyThemeAsync(versionId, cancellationToken);
        var header = await BuildHeaderAsync(document, cancellationToken);
        var shell = BuildShell(document, rendered.Value, header);
        var page = AppendQuoteActions(shell, document, token, System.Net.WebUtility.HtmlEncode((await businesses.GetByIdAsync(document.BusinessId, cancellationToken))?.TradingName ?? string.Empty));
        page = await AppendPayActionsAsync(page, document, token, cancellationToken);
        page = InjectPanel(page, await BuildLinksPanelAsync(document, token, cancellationToken));
        return Content(page, "text/html; charset=utf-8");
    }

    [HttpGet("{token}/pdf")]
    public async Task<IActionResult> Pdf(string token, CancellationToken cancellationToken)
    {
        var document = await documents.GetByLinkTokenHashAsync(ClientLinkToken.Hash(token), cancellationToken);
        if (document is null || document.Number is null)
        {
            return InvalidLink();
        }

        scope.UseScope(document.TenantId, document.BusinessId);
        var result = await sender.Send(new GetDocumentPdfQuery(document.Id, null), cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        return File(result.Value.Content, "application/pdf", result.Value.FileName);
    }

    [HttpPost("{token}/pay")]
    public async Task<IActionResult> Pay(string token, [FromForm] string method, [FromForm] string? phone, [FromForm] string? amountChoice, [FromForm] decimal? amount, [FromForm] string? email, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<InvoiceBuilder.Domain.Payments.PaymentMethod>(method, out var parsed))
        {
            return BadRequest("Choose a payment method.");
        }

        var result = await sender.Send(new InvoiceBuilder.Application.Payments.StartClientPaymentCommand(token, amountChoice == "other" ? amount : null, parsed, phone, email, amountChoice == "next"), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code == ClientLinkErrors.Invalid.Code ? NotFound() : BadRequest(result.Error.Message);
        }

        if (result.Value.RedirectUrl is { Length: > 0 } redirect)
        {
            return Redirect(redirect);
        }

        // InnBucks returns a code the client must enter in the InnBucks app, and mobile money may return
        // instructions: show them now, because only this response carries them.
        return result.Value.AuthorizationCode is { Length: > 0 } || result.Value.Instructions is { Length: > 0 }
            ? await RenderPaidAsync(token, "Pending", result.Value.PaymentId, cancellationToken, result.Value)
            : Redirect($"/d/{Uri.EscapeDataString(token)}/paid?payment={result.Value.PaymentId}");
    }

    [HttpGet("{token}/receipts/{receiptId:guid}")]
    public async Task<IActionResult> Receipt(string token, Guid receiptId, CancellationToken cancellationToken)
    {
        var invoice = await documents.GetByLinkTokenHashAsync(ClientLinkToken.Hash(token), cancellationToken);
        if (invoice is null || invoice.Type != InvoiceBuilder.Domain.Documents.DocumentType.Invoice)
        {
            return InvalidLink();
        }

        scope.UseScope(invoice.TenantId, invoice.BusinessId);
        var receipt = (await documents.ListReceiptsAsync(invoice.Id, cancellationToken)).FirstOrDefault(r => r.Id == receiptId);
        if (receipt is null || receipt.TemplateVersionId is not { } versionId)
        {
            return InvalidLink();
        }

        var rendered = await html.RenderAsync(receipt, versionId, null, cancellationToken);
        if (rendered.IsFailure)
        {
            return NotFound();
        }

        ApplyClientPageHeaders();
        return Content(rendered.Value, "text/html; charset=utf-8");
    }

    [HttpGet("{token}/payments/{paymentId:guid}")]
    public async Task<IActionResult> PaymentStatus(string token, Guid paymentId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new InvoiceBuilder.Application.Payments.GetClientPaymentStatusQuery(token, paymentId), cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return result.IsFailure ? NotFound() : Ok(new { status = result.Value.Status });
    }

    [HttpGet("{token}/paid")]
    public async Task<IActionResult> Paid(string token, [FromQuery] Guid? payment, CancellationToken cancellationToken)
    {
        var status = "Pending";
        if (payment is { } id)
        {
            var result = await sender.Send(new InvoiceBuilder.Application.Payments.GetClientPaymentStatusQuery(token, id), cancellationToken);
            if (result.IsFailure)
            {
                return InvalidLink();
            }

            status = result.Value.Status;
        }

        return await RenderPaidAsync(token, status, payment, cancellationToken);
    }
    private async Task<IActionResult> RenderPaidAsync(string token, string status, Guid? paymentId, CancellationToken cancellationToken, InvoiceBuilder.Application.Payments.StartedPayment? started = null)
    {
        var invoice = await documents.GetByLinkTokenHashAsync(ClientLinkToken.Hash(token), cancellationToken);
        if (invoice is null || invoice.Number is null)
        {
            return InvalidLink();
        }

        scope.UseScope(invoice.TenantId, invoice.BusinessId);
        if (invoice.TemplateVersionId is { } versionId)
        {
            await ApplyThemeAsync(versionId, cancellationToken);
        }

        var back = "/d/" + Uri.EscapeDataString(token);
        var business = await businesses.GetByIdAsync(invoice.BusinessId, cancellationToken);
        var client = await clients.GetByIdAsync(invoice.ClientId, cancellationToken);
        var clientName = System.Net.WebUtility.HtmlEncode(client?.Name ?? string.Empty);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string heading;
        string message;
        var details = string.Empty;
        var actions = LinkButton(back, "Back to invoice");
        if (status == "Confirmed")
        {
            heading = "&#10003; Payment received";
            message = "Thank you" + (clientName.Length > 0 ? ", " + clientName : string.Empty) + ".";
            var amountPaid = 0m; var method = string.Empty;
            if (paymentId is { } id)
            {
                var result = await sender.Send(new InvoiceBuilder.Application.Payments.GetClientPaymentStatusQuery(token, id), cancellationToken);
                amountPaid = result.IsSuccess ? result.Value.Amount : 0m; method = result.IsSuccess ? result.Value.Method : string.Empty;
            }

            var credited = (await documents.ListCreditNotesAsync(invoice.Id, cancellationToken)).Sum(c => c.Total);
            var paid = await payments.ConfirmedAllocatedTotalAsync(invoice.Id, cancellationToken);
            var balance = InvoiceBuilder.Domain.Payments.PaymentAllocator.Balance(invoice.Total, credited, paid);
            details = "<dl style=\"margin:1rem 0\"><dt>Amount paid</dt><dd style=\"margin:0 0 .5rem;font-weight:600\">" + amountPaid.ToString("N2", inv) + " " + invoice.Currency
                + (method.Length > 0 ? "</dd><dt>Method</dt><dd style=\"margin:0 0 .5rem;font-weight:600\">" + System.Net.WebUtility.HtmlEncode(method) : string.Empty) + "</dd><dt>Invoice</dt><dd style=\"margin:0 0 .5rem;font-weight:600\">" + System.Net.WebUtility.HtmlEncode(invoice.Number.Value.ToString())
                + "</dd><dt>Balance due</dt><dd style=\"margin:0;font-weight:600\">" + balance.ToString("N2", inv) + " " + invoice.Currency + "</dd></dl>";
            var receipt = (await documents.ListReceiptsAsync(invoice.Id, cancellationToken)).LastOrDefault(r => r.Number is not null);
            if (receipt is not null)
            {
                actions = LinkButton(back + "/receipts/" + receipt.Id, "Download receipt") + actions;
            }
        }
        else if (status == "Failed")
        {
            heading = "The payment didn't go through";
            message = "No money was taken. You can try again from your invoice.";
            actions = LinkButton(back, "Try again");
        }
        else
        {
            heading = "Waiting for confirmation from Paynow&hellip;";
            message = "Approve the payment on your phone. This page refreshes every 5 seconds.";
            if (started?.AuthorizationCode is { Length: > 0 } code)
            {
                heading = "Pay with InnBucks";
                message = "Open the InnBucks app and enter this code to approve the payment. This page updates by itself once it's paid.";
                var expires = started.AuthorizationExpires is { Length: > 0 } e ? "<p>Use it before " + System.Net.WebUtility.HtmlEncode(e) + ".</p>" : string.Empty;
                details = "<p style=\"margin:1rem 0 .25rem;color:#44403C\">Your InnBucks code</p><p style=\"margin:0;font:600 32px ui-monospace,Consolas,monospace;letter-spacing:.08em\">" + System.Net.WebUtility.HtmlEncode(code) + "</p>" + expires;
                if (started.AuthorizationDeepLink is { } deepLink)
                {
                    actions = LinkButton(System.Net.WebUtility.HtmlEncode(deepLink), "Open InnBucks on this phone") + actions;
                }
            }
            else if (started?.Instructions is { Length: > 0 } instructions)
            {
                details = "<p style=\"white-space:pre-line\">" + System.Net.WebUtility.HtmlEncode(instructions) + "</p>";
            }
        }

        var poll = status == "Pending" && paymentId is not null
            ? "<script nonce=\"" + Nonce + "\">(function(){var n=0;var t=setInterval(function(){n++;fetch('" + back + "/payments/" + paymentId + "',{cache:'no-store'}).then(function(r){return r.json()}).then(function(d){if(d.status!=='Pending'||n>=60){clearInterval(t);location.replace('" + back + "/paid?payment=" + paymentId + "');}})},5000);})();</script>"
            : string.Empty;
        ApplyClientPageHeaders();
        var page = "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Payment</title></head>"
            + "<body style=\"font-family:system-ui,sans-serif;background:#F5F5F4;color:#111111;margin:0;padding:1rem\">" + themedCss
            + "<main class=\"ib-panel\"><p style=\"margin:0;font-weight:700\">" + System.Net.WebUtility.HtmlEncode(business?.TradingName ?? string.Empty) + "</p><h1 style=\"font-size:1.5rem;margin:.25rem 0 .5rem\">" + heading + "</h1>"
            + "<p role=\"status\" aria-live=\"polite\">" + message + "</p>" + details + actions + "</main>"
            + "<p style=\"text-align:center;color:#44403C\">Sent with Invoice Builder</p>" + poll + "</body></html>";
        return Content(page, "text/html; charset=utf-8");
    }

    private static string LinkButton(string href, string text) =>
        "<a class=\"ib-btn\" style=\"display:block;box-sizing:border-box;text-align:center;text-decoration:none;line-height:28px\" href=\"" + href + "\">" + text + "</a>";



    [HttpPost("{token}/accept")]
    public async Task<IActionResult> Accept(string token, [FromForm] string signerName, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AcceptQuoteAndIssueCommand(token, signerName, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code == ClientLinkErrors.Invalid.Code ? NotFound() : BadRequest(result.Error.Message);
        }

        await sender.Send(new InvoiceBuilder.Application.Messaging.NotifyQuoteAcceptedCommand(token), cancellationToken);
        return Redirect($"/d/{result.Value.InvoiceLinkToken ?? token}");
    }

    [HttpPost("{token}/decline")]
    public async Task<IActionResult> Decline(string token, [FromForm] string signerName, [FromForm] string? comment, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeclineQuoteCommand(token, signerName, comment, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()), cancellationToken);
        return result.IsSuccess ? Redirect($"/d/{token}") : result.Error.Code == ClientLinkErrors.Invalid.Code ? NotFound() : BadRequest(result.Error.Message);
    }
}
