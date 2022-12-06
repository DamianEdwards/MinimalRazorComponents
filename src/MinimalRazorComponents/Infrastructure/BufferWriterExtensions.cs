using System.Buffers;
using System.IO.Pipelines;
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
            var status = HtmlEncoder.Default.Encode(textSpan, (Span<char>)encodedCharsSpan, out int charsConsumed, out int charsWritten);
            
            if (status != OperationStatus.Done || charsWritten != text.Length)
            {
                throw new NotImplementedException();
            }

            // Copy to pipe
            // TODO: Verify the offset math here is correct, I have a feeling it's not for non-8-bit chars, etc.
            var bytesWritten = Encoding.UTF8.GetBytes(encodedCharsSpan.AsSpan()[..charsWritten], bufferWriter);

            ArrayPool<char>.Shared.Return(encodedCharsSpan);
        }
    }

    public static void WriteHtml(this IBufferWriter<byte> bufferWriter, string? encoded)
    {
        if (!string.IsNullOrEmpty(encoded))
        {
            var textSpan = encoded.AsSpan();
            var writerSpan = bufferWriter.GetSpan(textSpan.Length);

            var status = Utf8.FromUtf16(textSpan, writerSpan, out var charsRead, out var bytesWritten);

            bufferWriter.Advance(bytesWritten);

            if (status != OperationStatus.Done)
            {
                throw new NotImplementedException();
            }
        }
    }
}
