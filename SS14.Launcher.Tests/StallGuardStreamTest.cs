using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Tests;

[TestFixture]
[TestOf(typeof(StallGuardStream))]
public sealed class StallGuardStreamTest
{
    [Test]
    public async Task TestSilentHostIsReportedAsStalled()
    {
        await using var guarded = new StallGuardStream(new SilentStream(), TimeSpan.FromMilliseconds(300));
        var buffer = new byte[16];

        Assert.ThrowsAsync<DownloadStalledException>(async () => await guarded.ReadAsync(buffer));
    }

    [Test]
    public async Task TestNormalDataPassesThrough()
    {
        await using var guarded = new StallGuardStream(new MemoryStream([1, 2, 3, 4]), TimeSpan.FromSeconds(5));
        var buffer = new byte[16];

        var read = await guarded.ReadAsync(buffer);

        Assert.That(read, Is.EqualTo(4));
        Assert.That(buffer[3], Is.EqualTo(4));
    }

    [Test]
    public async Task TestUserCancelStaysACancel()
    {
        await using var guarded = new StallGuardStream(new SilentStream(), TimeSpan.FromSeconds(30));
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var buffer = new byte[16];

        // A cancel from the user must not be dressed up as a server problem.
        Assert.ThrowsAsync<TaskCanceledException>(async () => await guarded.ReadAsync(buffer, cancel.Token));
    }

    /// <summary>
    /// A connection that stays open and never delivers a byte.
    /// </summary>
    private sealed class SilentStream : Stream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    }
}
