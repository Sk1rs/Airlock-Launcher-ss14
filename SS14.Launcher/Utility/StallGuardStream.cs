using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SS14.Launcher.Utility;

/// <summary>
/// Thrown when a download connection stays open but stops delivering data.
/// </summary>
public sealed class DownloadStalledException(TimeSpan silence)
    : Exception($"No data received for {silence.TotalSeconds:0} seconds")
{
    public TimeSpan Silence { get; } = silence;
}

/// <summary>
/// A read-through stream that gives up when the other side goes quiet.
/// </summary>
/// <remarks>
/// A throttled or half-dead host keeps the connection open and simply never sends the next byte,
/// and a plain read on that waits forever, which the user sees as a progress bar that never moves.
/// Every read here is given a deadline; a read that misses it ends the download with an error the
/// UI can explain instead of an eternal wait. The caller's own cancellation is left alone.
/// </remarks>
public sealed class StallGuardStream(Stream inner, TimeSpan timeout) : Stream
{
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        try
        {
            return await inner.ReadAsync(buffer, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DownloadStalledException(timeout);
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        // Nobody reads this stream synchronously, but if someone does they get the same deadline.
        return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    }

    public override int Read(Span<byte> buffer)
    {
        var rented = new byte[buffer.Length];
        var read = Read(rented, 0, rented.Length);
        rented.AsSpan(0, read).CopyTo(buffer);

        return read;
    }

    public override void Flush() => inner.Flush();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            inner.Dispose();

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync();
        await base.DisposeAsync();
    }
}
