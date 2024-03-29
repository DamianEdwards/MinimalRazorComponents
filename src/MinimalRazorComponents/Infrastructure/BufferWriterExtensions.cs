﻿using System.Buffers;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace MinimalRazorComponents.Infrastructure;

public static class BufferWriterExtensions
{
    private const int SmallWriteByteSize = 256;
    private const int SmallWriteCharSize = SmallWriteByteSize / 2;
    private const int RentedCharArrayMaxSize = 1024;

    public static void Write(this IBufferWriter<byte> bufferWriter, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ReadOnlySpan<char> textSpan = text;
        var encodeStatus = OperationStatus.Done;

        if (textSpan.Length <= SmallWriteCharSize)
        {
            WriteSmall(bufferWriter, textSpan);
            return;
        }

        char[]? rentedBuffer = null;
        Span<char> encodedBuffer = Array.Empty<char>();

        while (textSpan.Length > 0)
        {
            if (encodedBuffer.Length == 0)
            {
                if (rentedBuffer is not null)
                {
                    ArrayPool<char>.Shared.Return(rentedBuffer);
                }

                // TODO: What size should this be?
                var rentedSpanSize = Math.Min(RentedCharArrayMaxSize, textSpan.Length * 2);
                rentedBuffer = ArrayPool<char>.Shared.Rent(rentedSpanSize);
                encodedBuffer = rentedBuffer;
            }

            // Encode to rented buffer
            encodeStatus = HtmlEncoder.Default.Encode(textSpan, encodedBuffer, out int charsConsumed, out int charsWritten);

            // Write encoded chars to the writer
            Span<char> encoded = encodedBuffer[..charsWritten];
            WriteHtml(bufferWriter, encoded);

            textSpan = textSpan[charsConsumed..];
            encodedBuffer = encodedBuffer[charsWritten..];
        }

        if (rentedBuffer is not null)
        {
            ArrayPool<char>.Shared.Return(rentedBuffer);
        }

        Debug.Assert(encodeStatus == OperationStatus.Done, "Bad math in IBufferWriter HTML writing extensions");
    }

    private static void WriteSmall(IBufferWriter<byte> bufferWriter, ReadOnlySpan<char> textSpan)
    {
        Span<char> encodedBuffer = stackalloc char[SmallWriteByteSize];

        // Encode to buffer
        var encodeStatus = HtmlEncoder.Default.Encode(textSpan, encodedBuffer, out int charsConsumed, out int charsWritten);

        // Write encoded chars to the writer
        Span<char> encoded = encodedBuffer[..charsWritten];
        WriteHtml(bufferWriter, encoded);

        Debug.Assert(encodeStatus == OperationStatus.Done, "Bad math in IBufferWriter HTML writing extensions");
    }

    public static void WriteHtml(this IBufferWriter<byte> bufferWriter, string? encoded)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return;
        }

        ReadOnlySpan<char> textSpan = encoded;
        WriteHtml(bufferWriter, textSpan);
    }

    private static void WriteHtml(IBufferWriter<byte> bufferWriter, ReadOnlySpan<char> encoded)
    {
        Span<byte> writerSpan = bufferWriter.GetSpan();
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

        Debug.Assert(status == OperationStatus.Done, "Bad math in IBufferWriter HTML writing extensions");
    }
}
