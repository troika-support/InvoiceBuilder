using System.ComponentModel.DataAnnotations;

namespace InvoiceBuilder.Application.Options;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Public https root used in client links and Paynow URLs. Required in Production.</summary>
    [Url]
    public string? PublicBaseUrl { get; set; }

    [Required]
    public string TimeZone { get; set; } = "Africa/Harare";
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Folder for assets and generated PDFs, outside the web root.</summary>
    [Required]
    public string FilesRoot { get; set; } = Path.Combine(AppContext.BaseDirectory, "files");
}

public sealed class KeyRingOptions
{
    public const string SectionName = "DataProtection";

    /// <summary>
    /// Folder for the Data Protection key ring, outside the web root. Required outside Development: without it keys live
    /// in memory on IIS, so encrypted Paynow keys and sign-in cookies stop working after every app-pool recycle.
    /// </summary>
    public string? KeysPath { get; set; }
}

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    /// <summary>Use the fake gateway and /dev/payments. Development only; startup fails if true elsewhere.</summary>
    public bool UseFake { get; set; }
}

public sealed class PdfOptions
{
    public const string SectionName = "Pdf";

    [Range(1, 16)]
    public int PoolSize { get; set; } = 2;

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 20;
}
