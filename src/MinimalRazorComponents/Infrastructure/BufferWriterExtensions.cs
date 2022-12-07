using System.Buffers;
using System.Numerics;
using System.Text;
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
            var status = OperationStatus.Done;

            // TODO: What size should this be?
            var rentedSpanSize = Math.Min(1024, textSpan.Length * 2);
            var encodedCharsSpan = ArrayPool<char>.Shared.Rent(rentedSpanSize);

            while (textSpan.Length > 0)
            {
                if (encodedCharsSpan.Length == 0)
                {
                    rentedSpanSize = Math.Min(1024, textSpan.Length * 2);
                    encodedCharsSpan = ArrayPool<char>.Shared.Rent(rentedSpanSize);
                }

                status = HtmlEncoder.Default.Encode(textSpan, (Span<char>)encodedCharsSpan, out int charsConsumed, out int charsWritten);

                textSpan = textSpan[charsConsumed..];
                encodedCharsSpan = encodedCharsSpan[charsWritten..];
            }

            //var status = HtmlEncoder.Default.Encode(textSpan, (Span<char>)encodedCharsSpan, out int charsConsumed, out int charsWritten);
            
            if (status != OperationStatus.Done)
            {
                throw new NotImplementedException();
            }

            // Copy to pipe
            //var _ = Encoding.UTF8.GetBytes(encodedCharsSpan.AsSpan()[..charsWritten], bufferWriter);
            Encoding.UTF8.GetBytes(encodedCharsSpan.AsSpan()[..charsWritten], bufferWriter);

            ArrayPool<char>.Shared.Return(encodedCharsSpan);
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

        //var status = Utf8.FromUtf16(textSpan, writerSpan, out var charsWritten, out var bytesWritten);

        //bufferWriter.Advance(bytesWritten);

        if (status != OperationStatus.Done)
        {
            throw new InvalidOperationException("Writing math is wrong :(");
        }
    }
}
