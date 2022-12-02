using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.IO.Pipelines;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components;

namespace MinimalRazorComponents.Infrastructure;

#pragma warning disable BL0006 // Do not use RenderTree types
internal sealed class HtmlRenderer : Renderer
{
    private static readonly HashSet<string> SelfClosingElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"
    };

    private static readonly Task CanceledRenderTask = Task.FromCanceled(new CancellationToken(canceled: true));
    private readonly PipeWriter _writer;

    public HtmlRenderer(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, PipeWriter writer)
        : base(serviceProvider, loggerFactory)
    {
        _writer = writer;
    }

    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => CanceledRenderTask;

    public async Task RenderComponentAsync(Type componentType, ParameterView initialParameters)
    {
        var component = InstantiateComponent(componentType);
        var componentId = AssignRootComponentId(component);

        await RenderRootComponentAsync(componentId, initialParameters);

        var context = new HtmlRenderingContext(_writer);
        var frames = GetCurrentRenderTreeFrames(componentId);
        var newPosition = RenderFrames(context, frames, 0, frames.Count);

        // When to flush the writer?
        await _writer.FlushAsync();
    }

    public Task RenderComponentAsync<TComponent>(ParameterView initialParameters) where TComponent : IComponent
    {
        return RenderComponentAsync(typeof(TComponent), initialParameters);
    }

    /// <inheritdoc />
    protected override void HandleException(Exception exception) => ExceptionDispatchInfo.Capture(exception).Throw();

    private int RenderFrames(HtmlRenderingContext context, ArrayRange<RenderTreeFrame> frames, int position, int maxElements)
    {
        var nextPosition = position;
        var endPosition = position + maxElements;
        while (position < endPosition)
        {
            nextPosition = RenderCore(context, frames, position);
            if (position == nextPosition)
            {
                throw new InvalidOperationException("We didn't consume any input.");
            }
            position = nextPosition;
        }

        return nextPosition;
    }

    private int RenderCore(HtmlRenderingContext context, ArrayRange<RenderTreeFrame> frames, int position)
    {
        ref var frame = ref frames.Array[position];
        switch (frame.FrameType)
        {
            case RenderTreeFrameType.Element:
                return RenderElement(context, frames, position);
            case RenderTreeFrameType.Attribute:
                throw new InvalidOperationException($"Attributes should only be encountered within {nameof(RenderElement)}");
            case RenderTreeFrameType.Text:
                context.Writer.Append(frame.TextContent);
                return ++position;
            case RenderTreeFrameType.Markup:
                context.Writer.AppendHtml(frame.MarkupContent);
                return ++position;
            case RenderTreeFrameType.Component:
                return RenderChildComponent(context, frames, position);
            case RenderTreeFrameType.Region:
                return RenderFrames(context, frames, position + 1, frame.RegionSubtreeLength - 1);
            case RenderTreeFrameType.ElementReferenceCapture:
            case RenderTreeFrameType.ComponentReferenceCapture:
                return ++position;
            default:
                throw new InvalidOperationException($"Invalid element frame type '{frame.FrameType}'.");
        }
    }

    private int RenderChildComponent(HtmlRenderingContext context, ArrayRange<RenderTreeFrame> frames, int position)
    {
        ref var frame = ref frames.Array[position];
        var childFrames = GetCurrentRenderTreeFrames(frame.ComponentId);
        RenderFrames(context, childFrames, 0, childFrames.Count);
        return position + frame.ComponentSubtreeLength;
    }

    private int RenderElement( HtmlRenderingContext context, ArrayRange<RenderTreeFrame> frames, int position)
    {
        ref var frame = ref frames.Array[position];
        var writer = context.Writer;
        writer.AppendHtml("<");
        writer.AppendHtml(frame.ElementName);
        var afterAttributes = RenderAttributes(context, frames, position + 1, frame.ElementSubtreeLength - 1, out var capturedValueAttribute);

        // When we see an <option> as a descendant of a <select>, and the option's "value" attribute matches the
        // "value" attribute on the <select>, then we auto-add the "selected" attribute to that option. This is
        // a way of converting Blazor's select binding feature to regular static HTML.
        if (context.ClosestSelectValueAsString != null
            && string.Equals(frame.ElementName, "option", StringComparison.OrdinalIgnoreCase)
            && string.Equals(capturedValueAttribute, context.ClosestSelectValueAsString, StringComparison.Ordinal))
        {
            writer.AppendHtml(" selected");
        }

        var remainingElements = frame.ElementSubtreeLength + position - afterAttributes;
        if (remainingElements > 0)
        {
            writer.AppendHtml(">");

            var isSelect = string.Equals(frame.ElementName, "select", StringComparison.OrdinalIgnoreCase);
            if (isSelect)
            {
                context.ClosestSelectValueAsString = capturedValueAttribute;
            }

            var afterElement = RenderChildren(context, frames, afterAttributes, remainingElements);

            if (isSelect)
            {
                // There's no concept of nested <select> elements, so as soon as we're exiting one of them,
                // we can safely say there is no longer any value for this
                context.ClosestSelectValueAsString = null;
            }

            writer.AppendHtml("</");
            writer.AppendHtml(frame.ElementName);
            writer.AppendHtml(">");
            Debug.Assert(afterElement == position + frame.ElementSubtreeLength);
            return afterElement;
        }
        else
        {
            if (SelfClosingElements.Contains(frame.ElementName))
            {
                writer.AppendHtml(" />");
            }
            else
            {
                writer.AppendHtml(">");
                writer.AppendHtml("</");
                writer.AppendHtml(frame.ElementName);
                writer.AppendHtml(">");
            }
            Debug.Assert(afterAttributes == position + frame.ElementSubtreeLength);
            return afterAttributes;
        }
    }

    private int RenderChildren(HtmlRenderingContext context, ArrayRange<RenderTreeFrame> frames, int position, int maxElements)
    {
        if (maxElements == 0)
        {
            return position;
        }

        return RenderFrames(context, frames, position, maxElements);
    }

    private static int RenderAttributes(HtmlRenderingContext context,
        ArrayRange<RenderTreeFrame> frames, int position, int maxElements, out string? capturedValueAttribute)
    {
        capturedValueAttribute = null;

        if (maxElements == 0)
        {
            return position;
        }

        var result = context.Writer;

        for (var i = 0; i < maxElements; i++)
        {
            var candidateIndex = position + i;
            ref var frame = ref frames.Array[candidateIndex];
            if (frame.FrameType != RenderTreeFrameType.Attribute)
            {
                return candidateIndex;
            }

            if (frame.AttributeName.Equals("value", StringComparison.OrdinalIgnoreCase))
            {
                capturedValueAttribute = frame.AttributeValue as string;
            }

            switch (frame.AttributeValue)
            {
                case bool flag when flag:
                    result.AppendHtml(" ");
                    result.AppendHtml(frame.AttributeName);
                    break;
                case string value:
                    result.AppendHtml(" ");
                    result.AppendHtml(frame.AttributeName);
                    result.AppendHtml("=");
                    result.AppendHtml("\"");
                    result.Append(value);
                    result.AppendHtml("\"");
                    break;
                default:
                    break;
            }
        }

        return position + maxElements;
    }

    private sealed class HtmlRenderingContext
    {
        public HtmlRenderingContext(PipeWriter writer)
        {
            Writer = writer;
        }

        public PipeWriter Writer { get; }

        public string? ClosestSelectValueAsString { get; set; }
    }
}
#pragma warning restore BL0006 // Do not use RenderTree types
