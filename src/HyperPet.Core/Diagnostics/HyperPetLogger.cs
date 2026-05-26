using System.Globalization;
using System.IO;
using System.Text;

namespace HyperPet.Core.Diagnostics;

/// <summary>
/// Append-only file logger. Thread-safe. Rotates at <see cref="MaxFileSizeBytes"/>
/// to a single <c>.1</c> backup. Never throws — diagnostics must not become a
/// new crash source.
/// </summary>
public sealed class HyperPetLogger
{
    public const long MaxFileSizeBytes = 1 * 1024 * 1024;

    private readonly object _gate = new();
    private readonly string _logPath;
    private readonly string _rotatedPath;

    public HyperPetLogger(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch
        {
            // Logger never throws.
        }

        _logPath = Path.Combine(directory, "hyperpet.log");
        _rotatedPath = Path.Combine(directory, "hyperpet.log.1");
    }

    public string LogPath => _logPath;

    public void Info(string message) => Write("INFO", message, exception: null);

    public void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    public void Fatal(string message, Exception? exception = null) => Write("FATAL", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            var builder = new StringBuilder(256);
            builder.Append(DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append('[').Append(level).Append(']');
            builder.Append(" tid=").Append(Environment.CurrentManagedThreadId);
            builder.Append(' ');
            builder.AppendLine(message ?? string.Empty);

            if (exception is not null)
            {
                builder.AppendLine(exception.ToString());
            }

            lock (_gate)
            {
                RotateIfNeeded();
                File.AppendAllText(_logPath, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Swallow. Logger must never crash the app.
        }
    }

    private void RotateIfNeeded()
    {
        try
        {
            var info = new FileInfo(_logPath);
            if (!info.Exists || info.Length < MaxFileSizeBytes)
            {
                return;
            }

            if (File.Exists(_rotatedPath))
            {
                File.Delete(_rotatedPath);
            }

            File.Move(_logPath, _rotatedPath);
        }
        catch
        {
            // Rotation failures must not block logging.
        }
    }
}
