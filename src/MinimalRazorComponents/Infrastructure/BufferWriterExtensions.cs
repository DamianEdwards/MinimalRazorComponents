﻿using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace MinimalRazorComponents.Infrastructure;

public static class BufferWriterExtensions
{
    public static void Write(this IBufferWriter<byte> bufferWriter, string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            var textSpan = text.AsSpan();
            var encodeStatus = OperationStatus.Done;

            char[]? rentedBuffer = null;
            var encodedBuffer = Array.Empty<char>();

            while (textSpan.Length > 0)
            {
                if (encodedBuffer.Length == 0)
                {
                    if (rentedBuffer is not null)
                    {
                        ArrayPool<char>.Shared.Return(rentedBuffer);
                    }

                    // TODO: What size should this be?
                    var rentedSpanSize = Math.Min(1024, textSpan.Length * 2);
                    rentedBuffer = ArrayPool<char>.Shared.Rent(rentedSpanSize);
                    encodedBuffer = rentedBuffer;
                }

                // Encode to rented buffer
                encodeStatus = HtmlEncoder.Default.Encode(textSpan, (Span<char>)encodedBuffer, out int charsConsumed, out int charsWritten);

                // Write encoded chars to the writer
                var encoded = encodedBuffer.AsSpan()[..charsWritten];
                WriteHtml(bufferWriter, encoded);

                textSpan = textSpan[charsConsumed..];
                encodedBuffer = encodedBuffer[charsWritten..];
            }

            if (rentedBuffer is not null)
            {
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }

            if (encodeStatus != OperationStatus.Done)
            {
                throw new InvalidOperationException("Bad math");
            }
        }
    }

    public static void WriteHtml(this IBufferWriter<byte> bufferWriter, string? encoded)
    {
        if (!string.IsNullOrEmpty(encoded))
        {
            var textSpan = encoded.AsSpan();
            WriteHtml(bufferWriter, textSpan);
        }
    }

    private static void WriteHtml(IBufferWriter<byte> bufferWriter, ReadOnlySpan<char> encoded)
    {
        var writerSpan = bufferWriter.GetSpan();
        var status = OperationStatus.Done;

        while (encoded.Length > 0)
        {
            if (writerSpan.Length == 0)
            {
                writerSpan = bufferWriter.GetSpan();
            }

            status = Utf8.FromUtf16(encoded, writerSpan, out var charsWritten, out var bytesWritten);

            encoded = encoded[charsWritten..];
            writerSpan = writerSpan[bytesWritten..];
            bufferWriter.Advance(bytesWritten);
        }

        if (status != OperationStatus.Done)
        {
            throw new InvalidOperationException("Bad math");
        }
    }
}
