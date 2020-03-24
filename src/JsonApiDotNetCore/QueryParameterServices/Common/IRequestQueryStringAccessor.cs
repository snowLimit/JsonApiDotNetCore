using Microsoft.AspNetCore.Http;

namespace JsonApiDotNetCore.QueryParameterServices.Common
{
    /// <summary>
    /// Internally used service to access the original query collection of a request/
    /// </summary>
    public interface IQueryCollectionAccessor
    {
        /// <summary>
        /// The original query collection of a request.
        /// </summary>
        IQueryCollection Query { get; }
    }
}
