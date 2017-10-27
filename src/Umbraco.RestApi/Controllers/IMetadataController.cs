using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Umbraco.RestApi.Controllers
{
    /// <summary>
    /// This is used to ensure consistency between controllers which allows for better testing
    /// </summary>
    public interface IMetadataController
    {
        Task<HttpResponseMessage> GetMetadata(Guid id);
    }
}