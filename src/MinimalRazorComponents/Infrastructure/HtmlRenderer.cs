﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This file adapted from https://github.com/dotnet/aspnetcore/blob/792e021af928d435276ffdb2149082ea3d8ce9c5/src/Mvc/Mvc.ViewFeatures/src/RazorComponents/HtmlRenderer.cs

using System.Buffers;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.RenderTree;

namespace MinimalRazorComponents.Infrastructure;

#pragma warning disable BL0006 // Do not use RenderTree types
internal sealed class HtmlRenderer : Renderer
{
    private static readonly HashSet<string> SelfClosingElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"
    };

    private static readonly Task CanceledRenderTask = Task.FromCanceled(new CancellationToken(canceled: true));

    private bool _initialized;
    private readonly object _lock = new();
    private readonly IServiceProvider _serviceProvider;

    public HtmlRenderer(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        : base(serviceProvider, loggerFactory)
    {
        _serviceProvider = serviceProvider;
    }

    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => CanceledRenderTask;

    public async Task<string?> RenderComponentAsync(
        Type componentType,
        ParameterView initialParameters,
        IBufferWriter<byte> bufferWriter,
        bool allowNavigation,
        ClaimsPrincipal? user = null)
    {


        var component = InstantiateComponent(componentType);
        var componentId = AssignRootComponentId(component);

        var context = new HtmlRenderingContext(bufferWriter, allowNavigation, user);

        InitializeStandardComponentServices(context);

        await RenderRootComponentAsync(componentId, initialParameters);

        var frames = GetCurrentRenderTreeFrames(componentId);
        var _ = RenderFrames(context, frames, 0, frames.Count);

        if (context.RequiresClientComponentScripts)
        {
            bufferWriter.WriteHtml(@"<script src=""_framework/blazor.webassembly.js""></script>");
        }

        return context.RedirectToUrl;
    }

    public Task<string?> RenderComponentAsync<TComponent>(
        ParameterView initialParameters,
        IBufferWriter<byte> bufferWriter,
        bool allowNavigation,
        ClaimsPrincipal? user = null)
        where TComponent : IComponent
    {
        return RenderComponentAsync(typeof(TComponent), initialParameters, bufferWriter, allowNavigation, user);
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
                context.Writer.Write(frame.TextContent);
                return ++position;
            case RenderTreeFrameType.Markup:
                context.Writer.WriteHtml(frame.MarkupContent);
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
        var component = frame.Component;

        var isClient = component.GetType().GetCustomAttributes(false)
            .Any(a => string.Equals(a.GetType().Name, "ClientComponentAttribute", StringComparison.Ordinal));

        if (isClient)
        {
            PrerenderChildClientComponent(context, ref frame, frames, position);
        }
        else
        {
            var childFrames = GetCurrentRenderTreeFrames(frame.ComponentId);
            RenderFrames(context, childFrames, 0, childFrames.Count);
        }
        return position + frame.ComponentSubtreeLength;
    }

    private void PrerenderChildClientComponent(HtmlRenderingContext context, ref RenderTreeFrame frame, ArrayRange<RenderTreeFrame> frames, int position)
    {
        var component = frame.Component;
        var componentType = component.GetType();

        // Get the parameters from the parent component
        var parameters = GetComponentParameters(frames, position);
        var marker = WebAssemblyComponentSerializer.SerializeInvocation(componentType, parameters, prerendered: true);
        WebAssemblyComponentSerializer.AppendPreamble(context.Writer, marker);

        try
        {
            var childFrames = GetCurrentRenderTreeFrames(frame.ComponentId);
            RenderFrames(context, childFrames, 0, childFrames.Count);
        }
        catch (NavigationException navigationException)
        {
            // Navigation was attempted during prerendering.
            if (context.AllowNavigation)
            {
                // We can't perform a redirect as the server already started sending the response.
                // This is considered an application error as the developer should buffer the response until
                // all components have rendered.
                throw new InvalidOperationException("A navigation command was attempted during prerendering after the server already started sending the response. " +
                    "Navigation commands can not be issued during server-side prerendering after the response from the server has started. Applications must buffer the" +
                    "response and avoid using features like FlushAsync() before all components on the page have been rendered to prevent failed navigation commands.", navigationException);
            }

            context.RedirectToUrl = navigationException.Location;
        }

        WebAssemblyComponentSerializer.AppendEpilogue(context.Writer, marker);

        context.RequiresClientComponentScripts = true;
    }

    private static ParameterView GetComponentParameters(ArrayRange<RenderTreeFrame> frames, int ownerIndex)
    {
        var ownerDescendantsEndIndexExcl = ownerIndex + frames.Array[ownerIndex].ElementSubtreeLength;
        var attributeFramesStartIndex = ownerIndex + 1;
        var attributeFramesEndIndexExcl = attributeFramesStartIndex;

        Dictionary<string, object?>? parameters = null;

        while (attributeFramesEndIndexExcl < ownerDescendantsEndIndexExcl && frames.Array[attributeFramesEndIndexExcl].FrameType == RenderTreeFrameType.Attribute)
        {
            var parameterFrame = frames.Array[attributeFramesEndIndexExcl];
            parameters ??= new();
            parameters.Add(parameterFrame.AttributeName, parameterFrame.AttributeValue);
            attributeFramesEndIndexExcl++;
        }

        return parameters is null
            ? ParameterView.Empty
            : ParameterView.FromDictionary(parameters);
    }

    private void InitializeStandardComponentServices(HtmlRenderingContext htmlRenderingContext)
    {
        // This might not be the first component in the request we are rendering, so
        // we need to check if we already initialized the services in this request.
        lock (_lock)
        {
            if (!_initialized)
            {
                InitializeCore(_serviceProvider, htmlRenderingContext);
                _initialized = true;
            }
        }

        static void InitializeCore(IServiceProvider serviceProvider, HtmlRenderingContext context)
        {
            if (context.User is { } user
                && serviceProvider.GetService<AuthenticationStateProvider>() is IHostEnvironmentAuthenticationStateProvider authenticationStateProvider)
            {
                var authenticationState = new AuthenticationState(user);
                authenticationStateProvider.SetAuthenticationState(Task.FromResult(authenticationState));
            }

            // It's important that this is initialized since a component might try to restore state during prerendering
            // (which will obviously not work, but should not fail)
            var componentApplicationLifetime = serviceProvider.GetRequiredService<ComponentStatePersistenceManager>();

            // This is actually sync as it delegates to calling the store passed in which is the implementation below
            componentApplicationLifetime.RestoreStateAsync(new PrerenderComponentApplicationStore()).GetAwaiter().GetResult();
        }
    }

    private class PrerenderComponentApplicationStore : IPersistentComponentStateStore
    {
        public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() => Task.FromResult((IDictionary<string, byte[]>)new Dictionary<string, byte[]>());

        public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> state) => throw new NotImplementedException();
    }

    private int RenderElement(HtmlRenderingContext context, ArrayRange<RenderTreeFrame> frames, int position)
    {
        ref var frame = ref frames.Array[position];
        var writer = context.Writer;
        writer.WriteHtml("<");
        writer.WriteHtml(frame.ElementName);
        var afterAttributes = RenderAttributes(context, frames, position + 1, frame.ElementSubtreeLength - 1, out var capturedValueAttribute);

        // When we see an <option> as a descendant of a <select>, and the option's "value" attribute matches the
        // "value" attribute on the <select>, then we auto-add the "selected" attribute to that option. This is
        // a way of converting Blazor's select binding feature to regular static HTML.
        if (context.ClosestSelectValueAsString != null
            && string.Equals(frame.ElementName, "option", StringComparison.OrdinalIgnoreCase)
            && string.Equals(capturedValueAttribute, context.ClosestSelectValueAsString, StringComparison.Ordinal))
        {
            writer.WriteHtml(" selected");
        }

        var remainingElements = frame.ElementSubtreeLength + position - afterAttributes;
        if (remainingElements > 0)
        {
            writer.WriteHtml(">");

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

            writer.WriteHtml("</");
            writer.WriteHtml(frame.ElementName);
            writer.WriteHtml(">");
            Debug.Assert(afterElement == position + frame.ElementSubtreeLength);
            return afterElement;
        }
        else
        {
            if (SelfClosingElements.Contains(frame.ElementName))
            {
                writer.WriteHtml(" />");
            }
            else
            {
                writer.WriteHtml(">");
                writer.WriteHtml("</");
                writer.WriteHtml(frame.ElementName);
                writer.WriteHtml(">");
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
                    result.WriteHtml(" ");
                    result.WriteHtml(frame.AttributeName);
                    break;
                case string value:
                    result.WriteHtml(" ");
                    result.WriteHtml(frame.AttributeName);
                    result.WriteHtml("=");
                    result.WriteHtml("\"");
                    result.Write(value);
                    result.WriteHtml("\"");
                    break;
                default:
                    break;
            }
        }

        return position + maxElements;
    }

    private sealed class HtmlRenderingContext
    {
        public HtmlRenderingContext(
            IBufferWriter<byte> writer,
            bool allowNavigation,
            ClaimsPrincipal? user = null)
        {
            Writer = writer;
            AllowNavigation = allowNavigation;
            User = user;
        }

        public IBufferWriter<byte> Writer { get; }

        public ClaimsPrincipal? User { get; }

        public bool AllowNavigation { get; }

        public string? ClosestSelectValueAsString { get; set; }

        public bool RequiresClientComponentScripts { get; set; }

        public string? RedirectToUrl { get; set; }
    }
}
#pragma warning restore BL0006 // Do not use RenderTree types
