using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace System.Buffers;

/// <summary>
/// A fast access struct that wraps <see cref="IBufferWriter{T}"/>.
/// </summary>
/// <typeparam name="T">The type of element to be written.</typeparam>
internal ref struct BufferWriter<T> where T : IBufferWriter<byte>
{
    /// <summary>
    /// The underlying <see cref="IBufferWriter{T}"/>.
    /// </summary>
    private readonly T _output;

    /// <summary>
    /// The result of the last call to <see cref="IBufferWriter{T}.GetSpan(int)"/>, less any bytes already "consumed" with <see cref="Advance(int)"/>.
    /// Backing field for the <see cref="Span"/> property.
    /// </summary>
    private Span<byte> _span;

    /// <summary>
    /// The number of uncommitted bytes (all the calls to <see cref="Advance(int)"/> since the last call to <see cref="Commit"/>).
    /// </summary>
    private int _buffered;

    /// <summary>
    /// The total number of bytes written with this writer.
    /// Backing field for the <see cref="BytesCommitted"/> property.
    /// </summary>
    private long _bytesCommitted;

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferWriter{T}"/> struct.
    /// </summary>
    /// <param name="output">The <see cref="IBufferWriter{T}"/> to be wrapped.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BufferWriter(T output)
    {
        _buffered = 0;
        _bytesCommitted = 0;
        _output = output;
        _span = output.GetSpan();
    }

    /// <summary>
    /// Gets the result of the last call to <see cref="IBufferWriter{T}.GetSpan(int)"/>.
    /// </summary>
    public readonly Span<byte> Span => _span;

    /// <summary>
    /// Gets the total number of bytes written with this writer.
    /// </summary>
    public readonly long BytesCommitted => _bytesCommitted;

    /// <summary>
    /// Calls <see cref="IBufferWriter{T}.Advance(int)"/> on the underlying writer
    /// with the number of uncommitted bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Commit()
    {
        var buffered = _buffered;
        if (buffered > 0)
        {
            _bytesCommitted += buffered;
            _buffered = 0;
            _output.Advance(buffered);
        }
    }

    /// <summary>
    /// Used to indicate that part of the buffer has been written to.
    /// </summary>
    /// <param name="count">The number of bytes written to.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        _buffered += count;
        _span = _span[count..];
    }

    /// <summary>
    /// Copies the caller's buffer into this writer and calls <see cref="Advance(int)"/> with the length of the source buffer.
    /// </summary>
    /// <param name="source">The buffer to copy in.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ReadOnlySpan<byte> source)
    {
        if (_span.Length >= source.Length)
        {
            source.CopyTo(_span);
            Advance(source.Length);
        }
        else
        {
            WriteMultiBuffer(source);
        }
    }

    public void Write(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            var textSpan = text.AsSpan();

            // How much space do we need to HTML encode?
            // TODO: Stackalloc here if size is <= 256 chars
            var encodedCharsSpan = ArrayPool<char>.Shared.Rent(textSpan.Length * 4);
            var status = HtmlEncoder.Default.Encode(textSpan, (Span<char>)encodedCharsSpan, out int charsConsumed, out int charsWritten);

            if (status != OperationStatus.Done || charsWritten != text.Length)
            {
                throw new NotImplementedException();
            }

            // Copy to pipe
            WriteHtml(encodedCharsSpan);
            //var _ = Encoding.UTF8.GetBytes(encodedCharsSpan.AsSpan()[..charsWritten], _output);
            //Advance(charsWritten);

            ArrayPool<char>.Shared.Return(encodedCharsSpan);
        }
    }

    /// <summary>
    /// Writes HTML content without encoding it to the output writer.
    /// </summary>
    /// <param name="encoded"></param>
    public void WriteHtml(string? encoded)
    {
        if (!string.IsNullOrEmpty(encoded))
        {
            var textSpan = encoded.AsSpan();

            WriteHtml(textSpan);
        }
    }

    private void WriteHtml(ReadOnlySpan<char> encoded)
    {
        if (encoded.Length > 0)
        {
            //var writerSpan = _output.GetSpan(textSpan.Length);

            var operationStatus = OperationStatus.Done;

            while (encoded.Length > 0)
            {
                if (_span.Length == 0)
                {
                    EnsureMore();
                }

                //var writable = Math.Min(textSpan.Length, _span.Length);

                operationStatus = Utf8.FromUtf16(encoded, _span, out var charsRead, out var bytesWritten);

                //source[..writable].CopyTo(_span);

                encoded = encoded[charsRead..];
                Advance(bytesWritten);
            }

            if (operationStatus != OperationStatus.Done)
            {
                throw new InvalidOperationException("Uh oh");
            }
        }
    }

    /// <summary>
    /// Acquires a new buffer if necessary to ensure that some given number of bytes can be written to a single buffer.
    /// </summary>
    /// <param name="count">The number of bytes that must be allocated in a single buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Ensure(int count = 1)
    {
        if (_span.Length < count)
        {
            EnsureMore(count);
        }
    }

    /// <summary>
    /// Gets a fresh span to write to, with an optional minimum size.
    /// </summary>
    /// <param name="count">The minimum size for the next requested buffer.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EnsureMore(int count = 0)
    {
        if (_buffered > 0)
        {
            Commit();
        }

        _span = _output.GetSpan(count);
    }

    /// <summary>
    /// Copies the caller's buffer into this writer, potentially across multiple buffers from the underlying writer.
    /// </summary>
    /// <param name="source">The buffer to copy into this writer.</param>
    private void WriteMultiBuffer(ReadOnlySpan<byte> source)
    {
        while (source.Length > 0)
        {
            if (_span.Length == 0)
            {
                EnsureMore();
            }

            var writable = Math.Min(source.Length, _span.Length);
            source[..writable].CopyTo(_span);
            source = source[writable..];
            Advance(writable);
        }
    }
}