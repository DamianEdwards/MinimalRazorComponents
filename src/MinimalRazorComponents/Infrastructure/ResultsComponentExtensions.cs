using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Microsoft.AspNetCore.Http;

public static class ResultsComponentExtensions
{
    public static IResult Component<TComponent>(this IResultExtensions resultExtensions)
        where TComponent : IComponent
    {
        return new ComponentResult<TComponent>() { };
    }

    public static IResult Component<TComponent>(this IResultExtensions resultExtensions, object parameters)
        where TComponent : IComponent
    {
        return new ComponentResult<TComponent>() { Parameters = HtmlHelper.ObjectToDictionary(parameters).AsReadOnly() };
    }
}
