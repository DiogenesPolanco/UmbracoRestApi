using System;
using System.Net.Http;
using System.Threading.Tasks;
using Umbraco.RestApi.Models;

namespace Umbraco.RestApi.Controllers
{
    /// <summary>
    /// This is used to ensure consistency between controllers which allows for better testing
    /// </summary>
    public interface ITraversableController<in TRepresentation> : ISearchController, ICrudController<TRepresentation>, IRootController, IMetadataController
        where TRepresentation : ContentRepresentationBase
    {
        Task<HttpResponseMessage> GetChildren(Guid id, PagedQuery query);

        Task<HttpResponseMessage> GetDescendants(Guid id, PagedQuery query);

        Task<HttpResponseMessage> GetAncestors(Guid id, PagedRequest query);
    }
}