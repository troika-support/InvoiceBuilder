using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Options;
using Microsoft.Extensions.Options;

namespace InvoiceBuilder.Infrastructure.Services;

/// <summary>Stores files under <see cref="StorageOptions.FilesRoot"/>. Keys are relative paths; anything that escapes the root is rejected.</summary>
public sealed class LocalFileStore(IOptions<StorageOptions> options) : IFileStore
{
    private readonly string _root = Path.GetFullPath(options.Value.FilesRoot);

    public async Task SaveAsync(string key, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        File.Delete(Resolve(key));
        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var full = Path.GetFullPath(Path.Combine(_root, key));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Key must stay inside the files root.", nameof(key));
        }

        return full;
    }
}
