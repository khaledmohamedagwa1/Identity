using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Platform.Domain.Ids;

/// <summary>
/// Generates RFC 9562 UUID version 7 values that are unique and strictly increasing within a process
/// (ADR-0009). Layout: 48-bit Unix milliseconds, version, 12-bit monotonic counter (method 1 of
/// RFC 9562 section 6.2), variant, 62 random bits. When the counter overflows inside one millisecond
/// the timestamp is advanced by one, so ordering is never broken.
/// </summary>
/// <remarks>
/// <see cref="Guid.CreateVersion7()"/> is not monotonic within a millisecond, which breaks the
/// "time-ordered" requirement (TC-F02-06), hence this generator.
/// Ordering is by the big-endian byte representation, which is what PostgreSQL <c>uuid</c>
/// comparison and the canonical string form use.
/// </remarks>
public static class Uuid7
{
    private static readonly Lock Gate = new();
    private static long _lastMillis;
    private static int _counter;

    /// <summary>Creates a new monotonic UUIDv7 using the system clock.</summary>
    public static Guid NewGuid() => NewGuid(TimeProvider.System);

    /// <summary>Creates a new monotonic UUIDv7 using <paramref name="clock"/> for the timestamp.</summary>
    public static Guid NewGuid(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Span<byte> random = stackalloc byte[10];
        RandomNumberGenerator.Fill(random);

        long millis;
        int counter;
        lock (Gate)
        {
            millis = clock.GetUtcNow().ToUnixTimeMilliseconds();
            if (millis > _lastMillis)
            {
                _lastMillis = millis;
                // Seed the counter in the lower half so there is room to increment.
                _counter = random[0] & 0x7FF;
            }
            else
            {
                _counter++;
                if (_counter > 0xFFF)
                {
                    _lastMillis++;
                    _counter = 0;
                }

                millis = _lastMillis;
            }

            counter = _counter;
        }

        Span<byte> bytes = stackalloc byte[16];
        // 48-bit big-endian timestamp.
        Span<byte> ts = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(ts, millis);
        ts[2..].CopyTo(bytes);
        // Version 7 + 12-bit counter.
        bytes[6] = (byte)(0x70 | ((counter >> 8) & 0x0F));
        bytes[7] = (byte)(counter & 0xFF);
        // Variant 10xx + random.
        bytes[8] = (byte)(0x80 | (random[1] & 0x3F));
        random[2..9].CopyTo(bytes[9..]);

        return new Guid(bytes, bigEndian: true);
    }

    /// <summary>Extracts the Unix millisecond timestamp from a UUIDv7.</summary>
    public static DateTimeOffset GetTimestamp(Guid uuid)
    {
        Span<byte> bytes = stackalloc byte[16];
        uuid.TryWriteBytes(bytes, bigEndian: true, out _);
        Span<byte> ts = stackalloc byte[8];
        bytes[..6].CopyTo(ts[2..]);
        return DateTimeOffset.FromUnixTimeMilliseconds(BinaryPrimitives.ReadInt64BigEndian(ts));
    }
}
