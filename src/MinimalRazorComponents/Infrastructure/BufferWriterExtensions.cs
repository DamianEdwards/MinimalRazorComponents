using System.Buffers;
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
            
            // How much space do we need to HTML encoding?
            var encodedCharsSpan = ArrayPool<char>.Shared.Rent(textSpan.Length * 4);
            var status = HtmlEncoder.Default.Encode(textSpan, (Span<char>)encodedCharsSpan, out int _, out int charsWritten);
            
            if (status != OperationStatus.Done || charsWritten != text.Length)
            {
                throw new NotImplementedException();
            }

            // Copy to pipe
            var _ = Encoding.UTF8.GetBytes(encodedCharsSpan.AsSpan()[..charsWritten], bufferWriter);

            ArrayPool<char>.Shared.Return(encodedCharsSpan);
        }
    }

    public static void WriteHtml(this IBufferWriter<byte> bufferWriter, string? encoded)
    {
        if (!string.IsNullOrEmpty(encoded))
        {
            var textSpan = encoded.AsSpan();
            var writerSpan = bufferWriter.GetSpan(textSpan.Length);

            var status = Utf8.FromUtf16(textSpan, writerSpan, out var _, out var bytesWritten);

            bufferWriter.Advance(bytesWritten);

            if (status != OperationStatus.Done)
            {
                throw new NotImplementedException();
            }
        }
    }
}
