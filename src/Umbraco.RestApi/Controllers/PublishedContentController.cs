using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Filters;
using System.Web.Security;
using Examine;
using Examine.Providers;
using Microsoft.Owin.Security.Authorization.WebApi;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.Models;
using Umbraco.RestApi.Models;
using Umbraco.RestApi.Routing;
using Umbraco.RestApi.Security;
using Umbraco.Web;
using Umbraco.Web.WebApi;

namespace Umbraco.RestApi.Controllers
{
    [ResourceAuthorize(Policy = AuthorizationPolicies.PublishedContentRead)]
    [UmbracoRoutePrefix("rest/v1/content/published")]    
    public class PublishedContentController : UmbracoHalController
    {
        public PublishedContentController()
        {
        }

        /// <summary>
        /// All dependencies
        /// </summary>
        /// <param name="umbracoContext"></param>
        /// <param name="umbracoHelper"></param>
        /// <param name="searchProvider"></param>
        /// <param name="pcrFactory"></param>
        public PublishedContentController(
            UmbracoContext umbracoContext,
            UmbracoHelper umbracoHelper,
            BaseSearchProvider searchProvider,
            IPublishedContentRequestFactory pcrFactory)
            : base(umbracoContext, umbracoHelper)
        {
            _pcrFactory = pcrFactory;
            _searchProvider = searchProvider ?? throw new ArgumentNullException("searchProvider");            
        }

        private IPublishedContentRequestFactory _pcrFactory;
        protected IPublishedContentRequestFactory PcrFactory => _pcrFactory ?? (_pcrFactory = new PublishedContentRequestFactory(UmbracoContext, UmbracoConfig.For.UmbracoSettings().WebRouting, Roles.GetRolesForUser));

        private BaseSearchProvider _searchProvider;
        protected BaseSearchProvider SearchProvider => _searchProvider ?? (_searchProvider = ExamineManager.Instance.SearchProviderCollection["ExternalSearcher"]);        

        [HttpGet]
        [CustomRoute("")]
        public virtual HttpResponseMessage Get()
        {
            var rootContent = Umbraco.TypedContentAtRoot().ToArray();
            if (rootContent.Length > 0)
                PcrFactory.Create(rootContent[0], Request.RequestUri);
            
            var result = AutoMapper.Mapper.Map<IEnumerable<PublishedContentRepresentation>>(rootContent).ToList();
            var representation = new PublishedContentListRepresenation(result);
            return Request.CreateResponse(HttpStatusCode.OK, representation);
        }


        [HttpGet]
        [CustomRoute("{id}")]
        public HttpResponseMessage Get(Guid id)
        {
            var content = Umbraco.TypedContent(id);
            if (content == null) return Request.CreateResponse(HttpStatusCode.NotFound);

            PcrFactory.Create(content, Request.RequestUri);

            var result = AutoMapper.Mapper.Map<PublishedContentRepresentation>(content);

            return result == null
                ? Request.CreateResponse(HttpStatusCode.NotFound)
                : Request.CreateResponse(HttpStatusCode.OK, result);
        }


        [HttpGet]
        [CustomRoute("{id}/children")]
        public HttpResponseMessage GetChildren(Guid id,
            [System.Web.Http.ModelBinding.ModelBinder(typeof(PagedQueryModelBinder))]
            PagedQuery query)
        {
            var content = Umbraco.TypedContent(id);
            if (content == null) return Request.CreateResponse(HttpStatusCode.NotFound);

            PcrFactory.Create(content, Request.RequestUri);

            var resolved = (string.IsNullOrEmpty(query.Query)) ? content.Children().ToArray() : content.Children(query.Query.Split(',')).ToArray();
            var total = resolved.Length;
            var pages = (total + query.PageSize - 1) / query.PageSize;

            var items = AutoMapper.Mapper.Map<IEnumerable<PublishedContentRepresentation>>(resolved.Skip(ContentControllerHelper.GetSkipSize(query.Page - 1, query.PageSize)).Take(query.PageSize)).ToList();
            var result = new PublishedContentPagedListRepresentation(items, total, pages, query.Page - 1, query.PageSize, LinkTemplates.PublishedContent.PagedChildren, new { id = id });

            return Request.CreateResponse(HttpStatusCode.OK, result);
        }


        [HttpGet]
        [CustomRoute("{id}/descendants/")]
        public HttpResponseMessage GetDescendants(Guid id,
            [System.Web.Http.ModelBinding.ModelBinder(typeof(PagedQueryModelBinder))]
            PagedQuery query)
        {
            var content = Umbraco.TypedContent(id);
            if (content == null) return Request.CreateResponse(HttpStatusCode.NotFound);

            PcrFactory.Create(content, Request.RequestUri);

            var resolved = (string.IsNullOrEmpty(query.Query)) ? content.Descendants().ToArray() : content.Descendants(query.Query).ToArray();

            var total = resolved.Length;
            var pages = (total + query.PageSize - 1) / query.PageSize;
            var items = AutoMapper.Mapper.Map<IEnumerable<PublishedContentRepresentation>>(resolved.Skip(ContentControllerHelper.GetSkipSize(query.Page - 1, query.PageSize)).Take(query.PageSize)).ToList();
            var result = new PublishedContentPagedListRepresentation(items, total, pages, query.Page - 1, query.PageSize, LinkTemplates.PublishedContent.PagedDescendants, new { id = id });
            return Request.CreateResponse(HttpStatusCode.OK, result);
        }

        [HttpGet]
        [CustomRoute("{id}/ancestors/{page?}/{pageSize?}")]
        public HttpResponseMessage GetAncestors(Guid id,
            [System.Web.Http.ModelBinding.ModelBinder(typeof(PagedQueryModelBinder))]
            PagedQuery query)
        {
            var content = Umbraco.TypedContent(id);
            if (content == null) return Request.CreateResponse(HttpStatusCode.NotFound);

            PcrFactory.Create(content, Request.RequestUri);

            var resolved = (string.IsNullOrEmpty(query.Query)) ? content.Ancestors().ToArray() : content.Ancestors(query.Query).ToArray();

            var total = resolved.Length;
            var pages = (total + query.PageSize - 1) / query.PageSize;

            var items = AutoMapper.Mapper.Map<IEnumerable<PublishedContentRepresentation>>(resolved.Skip(ContentControllerHelper.GetSkipSize(query.Page - 1, query.PageSize)).Take(query.PageSize)).ToList();
            var result = new PublishedContentPagedListRepresentation(items, total, pages, query.Page - 1, query.PageSize, LinkTemplates.PublishedContent.PagedAncestors, new { id = id });
            return Request.CreateResponse(HttpStatusCode.OK, result);
        }


        [HttpGet]
        [CustomRoute("query/{id?}")]
        public HttpResponseMessage GetQuery(
            [System.Web.Http.ModelBinding.ModelBinder(typeof(PagedQueryModelBinder))]
            PagedQuery query,
            Guid id = default(Guid))
        {
            var rootQuery = "";
            if (id != default(Guid))
            {
                var intId = Services.EntityService.GetIdForKey(id, UmbracoObjectTypes.Document);

                if (intId.Result < 0)
                    return Request.CreateResponse(HttpStatusCode.NotFound);

                rootQuery = $"//*[@id='{intId.Result}']";
            }

            var skip = (query.Page - 1) * query.PageSize;
            var take = query.PageSize;

            var result = new IPublishedContent[0];

            try
            {
                result = Umbraco.TypedContentAtXPath(rootQuery + query.Query).ToArray();
            }
            catch (Exception)
            {
                //in case the xpath query fails - do nothing as we will return a empty array instead    
            }

            var paged = result.Skip((int)skip).Take(take);
            var items = AutoMapper.Mapper.Map<IEnumerable<PublishedContentRepresentation>>(paged).ToList();
            var representation = new PublishedContentPagedListRepresentation(items, result.Length, 1, query.Page - 1, query.PageSize, LinkTemplates.PublishedContent.Query, new { query = query.Query, pageSize = query.PageSize });

            return Request.CreateResponse(HttpStatusCode.OK, representation);
        }
        
        [HttpGet]
        [CustomRoute("search")]
        public HttpResponseMessage Search(
            [System.Web.Http.ModelBinding.ModelBinder(typeof(PagedQueryModelBinder))]
            PagedQuery query)
        {
            if (query.Query.IsNullOrWhiteSpace()) throw new HttpResponseException(HttpStatusCode.NotFound);

            //TODO: This would be more efficient if we went straight to the ExamineManager and used it's built in Skip method
            // but then we have to write our own model mappers and don't have time for that right now.

            var result = Umbraco.ContentQuery.TypedSearch(SearchProvider.CreateSearchCriteria().RawQuery(query.Query), _searchProvider).ToArray();
            var paged = result.Skip(ContentControllerHelper.GetSkipSize(query.Page - 1, query.PageSize)).Take(query.PageSize);

            var items = AutoMapper.Mapper.Map<IEnumerable<PublishedContentRepresentation>>(paged).ToList();
            var representation = new PublishedContentPagedListRepresentation(items, result.Length, 1, query.Page - 1, query.PageSize, LinkTemplates.PublishedContent.Search, new { query = query.Query, pageSize = query.PageSize });

            return Request.CreateResponse(HttpStatusCode.OK, representation);
        }

        [HttpGet]
        [CustomRoute("url")]
        public HttpResponseMessage GetByUrl(string url)
        {
            var content = UmbracoContext.ContentCache.GetByRoute(url);
            if (content == null) return Request.CreateResponse(HttpStatusCode.NotFound);

            PcrFactory.Create(content, Request.RequestUri);

            var result = AutoMapper.Mapper.Map<PublishedContentRepresentation>(content);

            return result == null
                ? Request.CreateResponse(HttpStatusCode.NotFound)
                : Request.CreateResponse(HttpStatusCode.OK, result);
        }

        [HttpGet]
        [CustomRoute("tag/{tag}")]
        public HttpResponseMessage GetByTag(string tag, string group = null, int page = 0, int size = 100)
        {
            var content = Umbraco.TagQuery.GetContentByTag(tag, group).ToArray();

            if (content.Length > 0)
                PcrFactory.Create(content[0], Request.RequestUri);

            var skip = (page * size);
            var total = content.Length;
            var pages = (total + size - 1) / size;

            var items = AutoMapper.Mapper.Map<IEnumerable<PublishedContentRepresentation>>(content.Skip(skip).Take(size)).ToList();
            var representation = new PublishedContentPagedListRepresentation(items, total, pages, page, size, LinkTemplates.PublishedContent.Search, new
            {
                tag = tag,
                group = group,
                page = page,
                size = size
            });

            return Request.CreateResponse(HttpStatusCode.OK, representation);
        }

    }
}
