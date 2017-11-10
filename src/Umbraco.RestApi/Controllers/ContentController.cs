using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using AutoMapper;
using Examine;
using Examine.Providers;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.RestApi.Models;
using Umbraco.RestApi.Routing;
using Umbraco.Web;
using System.Web.Http.ModelBinding;
using Microsoft.Owin.Security.Authorization.WebApi;
using Newtonsoft.Json;
using umbraco.BusinessLogic.Actions;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Publishing;
using Umbraco.Web.Models.ContentEditing;
using Umbraco.Core.Services;
using Umbraco.RestApi.Security;
using Umbraco.Web.WebApi;
using WebApi.Hal;
using Task = System.Threading.Tasks.Task;
using System.Web;

namespace Umbraco.RestApi.Controllers
{
    /// <summary>
    /// A controller for working with non-published content (database level)
    /// </summary>
    /// <remarks>
    /// TODO: Query access to this controller will generally only work if the Id claim type belongs to a real Umbraco User since permissions
    /// for that user need to be looked up. The only way around this would be to be able to have an IPermissionService that could be added
    /// to the rest api options and a developer could replace that.
    /// </remarks>
    [ContentControllerConfiguration]
    [ResourceAuthorize(Policy = AuthorizationPolicies.DefaultRestApi)]
    [UmbracoRoutePrefix("rest/v1/content")]
    public class ContentController : UmbracoHalController, ITraversableController<ContentRepresentation>
    {

        private class ContentControllerConfigurationAttribute : Attribute, IControllerConfiguration
        {
            public void Initialize(HttpControllerSettings controllerSettings, HttpControllerDescriptor controllerDescriptor)
            {
                controllerSettings.Services.Replace(typeof(IHttpActionSelector), new ParameterSwapControllerActionSelector(
                    new ParameterSwapControllerActionSelector.ParameterSwapInfo("Get", "id", typeof(int), typeof(Guid))
                ));
            }
        }


        /// <summary>
        /// Default ctor
        /// </summary>
        public ContentController()
        {
        }

        /// <summary>
        /// All dependencies
        /// </summary>
        /// <param name="umbracoContext"></param>
        /// <param name="umbracoHelper"></param>
        /// <param name="searchProvider"></param>
        public ContentController(
            UmbracoContext umbracoContext,
            UmbracoHelper umbracoHelper, 
            BaseSearchProvider searchProvider)
            : base(umbracoContext, umbracoHelper)
        {
            _searchProvider = searchProvider ?? throw new ArgumentNullException("searchProvider");
        }

        //this is the default language culture for umbraco translation files
        private static readonly CultureInfo DefaultCulture = CultureInfo.GetCultureInfo("en-US");
        private BaseSearchProvider _searchProvider;
        protected BaseSearchProvider SearchProvider => _searchProvider ?? (_searchProvider = ExamineManager.Instance.SearchProviderCollection["InternalSearcher"]);
        
        /// <summary>
        /// Returns the root level content for the authorized user
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [CustomRoute("")]
        public virtual async Task<HttpResponseMessage> Get()
        {
            var startContentIdsAsInt = ClaimsPrincipal.GetContentStartNodeIds();
            if (startContentIdsAsInt == null || startContentIdsAsInt.Length == 0)
                return Request.CreateResponse(HttpStatusCode.Unauthorized);

            if (!await AuthorizationService.AuthorizeAsync(ClaimsPrincipal, new ContentResourceAccess(startContentIdsAsInt), AuthorizationPolicies.ContentRead))
                return Request.CreateResponse(HttpStatusCode.Unauthorized);

            var rootContent = startContentIdsAsInt.Contains(Constants.System.Root)
                ? Services.ContentService.GetRootContent()
                : Services.ContentService.GetByIds(startContentIdsAsInt);

            var result = Mapper.Map<IEnumerable<ContentRepresentation>>(rootContent).ToList();
            var representation = new ContentListRepresenation(result);

            return Request.CreateResponse(HttpStatusCode.OK, representation);
        }

        [HttpGet]
        [CustomRoute("{id}")]
        public async Task<HttpResponseMessage> Get(Guid id)
        {
            //added here as otherwise it would require a substantial change to ContentResourceAccess
            var nodeId = Services.EntityService.GetIdForKey(id, UmbracoObjectTypes.Document);
            if(nodeId.Success)
                return await Get(nodeId.Result);

            return Request.CreateResponse(HttpStatusCode.NotFound);
        }

        [HttpGet]
        [CustomRoute("{id}")]
        public async Task<HttpResponseMessage> Get(int id)
        {
            if (!await AuthorizationService.AuthorizeAsync(ClaimsPrincipal, new ContentResourceAccess(id), AuthorizationPolicies.ContentRead))
                return Request.CreateResponse(HttpStatusCode.Unauthorized);

            var content = Services.ContentService.GetById(id);
            var result = Mapper.Map<ContentRepresentation>(content);

            return result == null
                ? Request.CreateResponse(HttpStatusCode.NotFound)
                : Request.CreateResponse(HttpStatusCode.OK, result);
        }

        [HttpGet]
        [CustomRoute("{id}/meta")]
        public async Task<HttpResponseMessage> GetMetadata(int id)
        {
            if (!await AuthorizationService.AuthorizeAsync(ClaimsPrincipal, new ContentResourceAccess(id), AuthorizationPolicies.ContentRead))
                return Request.CreateResponse(HttpStatusCode.Unauthorized);

            var found = Services.ContentService.GetById(id);
            if (found == null) throw new HttpResponseException(HttpStatusCode.NotFound);

            var helper = new ContentControllerHelper(Services.TextService);

            var result = new ContentMetadataRepresentation(LinkTemplates.Content.MetaData, LinkTemplates.Content.Self, id)
            {
                Fields = helper.GetDefaultFieldMetaData(ClaimsPrincipal),
                Properties = Mapper.Map<IDictionary<string, ContentPropertyInfo>>(found),
                CreateTemplate = Mapper.Map<ContentCreationTemplate>(found)
            };

            return Request.CreateResponse(HttpStatusCode.OK, result); 
        }

        [HttpGet]
        [CustomRoute("{id}/children")]
        public async Task<HttpResponseMessage> GetChildren(int id,
            [ModelBinder(typeof(PagedQueryModelBinder))]
            PagedQuery query)
        {
            if (!await AuthorizationService.AuthorizeAsync(ClaimsPrincipal, new ContentResourceAccess(id), AuthorizationPolicies.ContentRead))
                return Request.CreateResponse(HttpStatusCode.Unauthorized);

            var items = Services.ContentService.GetPagedChildren(id, query.Page - 1, query.PageSize, out var total, filter:query.Query);
            var pages = ContentControllerHelper.GetTotalPages(total, query.PageSize);
            var mapped = Mapper.Map<IEnumerable<ContentRepresentation>>(items).ToList();

            var result = new ContentPagedListRepresentation(mapped, total, pages, query.Page, query.PageSize, LinkTemplates.Content.PagedChildren, new { id = id });

            FilterAllowedOutgoingContent(result);

            return Request.CreateResponse(HttpStatusCode.OK, result);
        }

        [HttpGet]
        [CustomRoute("{id}/descendants/")]
        public async Task<HttpResponseMessage> GetDescendants(int id,
            [ModelBinder(typeof(PagedQueryModelBinder))]
            PagedQuery query)
        {
            if (!await AuthorizationService.AuthorizeAsync(ClaimsPrincipal, new ContentResourceAccess(id), AuthorizationPolicies.ContentRead))
                return Request.CreateResponse(HttpStatusCode.Unauthorized);

            var items = Services.ContentService.GetPagedDescendants(id, query.Page - 1, query.PageSize, out var total, filter: query.Query);
            var pages = ContentControllerHelper.GetTotalPages(total, query.PageSize);
            var mapped = Mapper.Map<IEnumerable<ContentRepresentation>>(items).ToList();

            var result = new ContentPagedListRepresentation(mapped, total, pages, query.Page - 1, query.PageSize, LinkTemplates.Content.PagedDescendants, new { id = id });

            FilterAllowedOutgoingContent(result);

            return Request.CreateResponse(HttpStatusCode.OK, result);
        }

        [HttpGet]
        [CustomRoute("{id}/ancestors/")]
        public async Task<HttpResponseMessage> GetAncestors(int id,
           [ModelBinder(typeof(PagedQueryModelBinder))]
           PagedRequest query)
        {
            if (!await AuthorizationService.AuthorizeAsync(ClaimsPrincipal, new ContentResourceAccess(id), AuthorizationPolicies.ContentRead))
                return Request.CreateResponse(HttpStatusCode.Unauthorized);

            var items = Services.ContentService.GetAncestors(id).ToArray();
            var total = items.Length;
            var pages = (total + query.PageSize - 1) / query.PageSize;
            var paged = items.Skip(ContentControllerHelper.GetSkipSize(query.Page - 1, query.PageSize)).Take(query.PageSize);
            var mapped = Mapper.Map<IEnumerable<ContentRepresentation>>(paged).ToList();

            var result = new ContentPagedListRepresentation(mapped, total, pages, query.Page - 1, query.PageSize, LinkTemplates.Content.PagedAncestors, new { id = id });

            FilterAllowedOutgoingContent(result);

            return Request.CreateResponse(HttpStatusCode.OK, result);
        }

        [HttpGet]
        [CustomRoute("search")]
        public async Task<HttpResponseMessage> Search(
            [ModelBinder(typeof(PagedQueryModelBinder))]
            PagedQuery query)
        {
            if (!await AuthorizationService.AuthorizeAsync(ClaimsPrincipal, ContentResourceAccess.Empty(), AuthorizationPolicies.ContentRead))
                return Request.CreateResponse(HttpStatusCode.Unauthorized);

            //TODO: Authorize this! how? Same as core, i guess we just filter the results

            if (query.Query.IsNullOrWhiteSpace()) throw new HttpResponseException(HttpStatusCode.NotFound);

            //Query prepping - ensure that we only search for content items...
            var mediaQuery = "__IndexType:content AND " + query.Query;

            //search
            var result = SearchProvider.Search(
                    SearchProvider.CreateSearchCriteria().RawQuery(mediaQuery),
                    query.PageSize);

            //paging
            var paged = result.Skip(ContentControllerHelper.GetSkipSize(query.Page - 1, query.PageSize)).ToArray();
            var pages = (result.TotalItemCount + query.PageSize - 1) / query.PageSize;

            var foundContent = Enumerable.Empty<IContent>();

            //Map to Imedia
            if (paged.Any())
            {
                foundContent = Services.ContentService.GetByIds(paged.Select(x => x.Id)).WhereNotNull();
            }

            //Map to representation
            var items = Mapper.Map<IEnumerable<ContentRepresentation>>(foundContent).ToList();

            //return as paged list of media items
            var representation = new ContentPagedListRepresentation(items, result.TotalItemCount, pages, query.Page - 1, query.PageSize, LinkTemplates.Content.Search, new { query = query.Query, pageSize = query.PageSize });

            //TODO: Enable this
            //FilterAllowedOutgoingContent(result);

            return Request.CreateResponse(HttpStatusCode.OK, representation);
        }

        // Content CRUD:

        [HttpPost]
        [CustomRoute("")]
        public async Task<HttpResponseMessage> Post(ContentRepresentation content)
        {
            if (content == null) return Request.CreateResponse(HttpStatusCode.NotFound);

            if (!await AuthorizationService.AuthorizeAsync(ClaimsPrincipal, new ContentResourceAccess(content.ParentId), AuthorizationPolicies.ContentCreate))
                return Request.CreateResponse(HttpStatusCode.Unauthorized);

            try
            {
                //we cannot continue here if the mandatory items are empty (i.e. name, etc...)
                if (!ModelState.IsValid)
                {
                    throw ValidationException(ModelState, content, LinkTemplates.Content.Root);
                }

                var contentType = Services.ContentTypeService.GetContentType(content.ContentTypeAlias);
                if (contentType == null)
                {
                    ModelState.AddModelError("content.contentTypeAlias", "No content type found with alias " + content.ContentTypeAlias);
                    throw ValidationException(ModelState, content, LinkTemplates.Content.Root);
                }

                //create an item before persisting of the correct content type
                var created = Services.ContentService.CreateContent(content.Name, content.ParentId, content.ContentTypeAlias, ClaimsPrincipal.GetUserId() ?? 0);

                //Validate properties
                var validator = new ContentPropertyValidator<IContent>(ModelState, Services.DataTypeService);
                validator.ValidateItem(content, created);

                if (!ModelState.IsValid)
                {
                    throw ValidationException(ModelState, content, LinkTemplates.Content.Root);
                }

                Mapper.Map(content, created);
                Services.ContentService.Save(created, ClaimsPrincipal.GetUserId() ?? 0);

                var msg = Request.CreateResponse(HttpStatusCode.Created, Mapper.Map<ContentRepresentation>(created));
                msg.Headers.Add("location", VirtualPathUtility.ToAbsolute(LinkTemplates.Content.Self.CreateLink(new { id = created.Id }).Href));

                return msg;
                
            }
            catch (ModelValidationException exception)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, exception.Errors);
            }
        }

        /// <summary>
        /// Updates a content item
        /// </summary>
        /// <param name="id"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        /// <remarks>
        /// This can also be used to publish/unpublish an item
        /// </remarks>
        [HttpPut]
        [CustomRoute("{id}")]
        public async Task<HttpResponseMessage> Put(int id, ContentRepresentation content)
        {
            if (content == null) return Request.CreateResponse(HttpStatusCode.NotFound);

            //TODO: Since this Id is based on a route parameter it should be possible to authz this with an attribute
            if (!await AuthorizationService.AuthorizeAsync(ClaimsPrincipal, new ContentResourceAccess(id), AuthorizationPolicies.ContentUpdate))
                return Request.CreateResponse(HttpStatusCode.Unauthorized);

            try
            {
                var found = Services.ContentService.GetById(id);
                if (found == null) throw new HttpResponseException(HttpStatusCode.NotFound);

                //Validate properties
                var validator = new ContentPropertyValidator<IContent>(ModelState, Services.DataTypeService);
                validator.ValidateItem(content, found);

                if (!ModelState.IsValid)
                {
                    throw ValidationException(ModelState, content, LinkTemplates.Content.Self, id: id);
                }

                Mapper.Map(content, found);

                if (!content.Published)
                {
                    //if the flag is not published then we just save a draft
                    Services.ContentService.Save(found, ClaimsPrincipal.GetUserId() ?? 0);
                }
                else
                {
                    //publish it if the flag is set, if it's already published that's ok too
                    var result = Services.ContentService.SaveAndPublishWithStatus(found, ClaimsPrincipal.GetUserId() ?? 0);
                    if (!result.Success)
                    {
                        SetModelStateForPublishStatus(result.Result);
                        throw ValidationException(ModelState, content, LinkTemplates.Content.Self, id: id);
                    }
                }

                var rep = Mapper.Map<ContentRepresentation>(found);
                return Request.CreateResponse(HttpStatusCode.OK, rep);
            }
            catch (ModelValidationException exception)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, exception.Errors);
            }
        }

        [HttpDelete]
        [CustomRoute("{id}")]
        public async Task<HttpResponseMessage> Delete(int id)
        {
            //TODO: Since this Id is based on a route parameter it should be possible to authz this with an attribute
            if (!await AuthorizationService.AuthorizeAsync(ClaimsPrincipal, new ContentResourceAccess(id), AuthorizationPolicies.ContentDelete))
                return Request.CreateResponse(HttpStatusCode.Unauthorized);

            var found = Services.ContentService.GetById(id);
            if (found == null)
                return Request.CreateResponse(HttpStatusCode.NotFound);

            Services.ContentService.Delete(found);
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        private void FilterAllowedOutgoingContent(SimpleListRepresentation<ContentRepresentation> rep)
        {
            if (rep == null || rep.ResourceList == null) return;

            var user = ClaimsPrincipal.GetUserFromClaims(Services.UserService);
            if (user == null)
                throw new HttpResponseException(HttpStatusCode.Unauthorized);
            
            var helper = new FilterAllowedOutgoingContent(Services.UserService, ActionBrowse.Instance.Letter.ToString());
            helper.FilterBasedOnPermissions((IList)rep.ResourceList, user);
        }

        private void SetModelStateForPublishStatus(PublishStatus status)
        {
            switch (status.StatusType)
            {                
                case PublishStatusType.FailedPathNotPublished:
                    ModelState.AddModelError(
                        "content.isPublished",
                        Services.TextService.Localize(
                            "publish/contentPublishedFailedByParent",
                            DefaultCulture, 
                            new[] {$"{status.ContentItem.Name} ({status.ContentItem.Id})"}).Trim());
                    break;
                case PublishStatusType.FailedCancelledByEvent:
                    ModelState.AddModelError(
                        "content.isPublished",
                        Services.TextService.Localize("speechBubbles/contentPublishedFailedByEvent", DefaultCulture));
                    break;
                case PublishStatusType.FailedAwaitingRelease:
                    ModelState.AddModelError(
                        "content.isPublished",
                        Services.TextService.Localize(
                            "publish/contentPublishedFailedAwaitingRelease",
                            CultureInfo.GetCultureInfo("en-US"),
                            new[] {$"{status.ContentItem.Name} ({status.ContentItem.Id})"}).Trim());          
                    break;
                case PublishStatusType.FailedHasExpired:
                    ModelState.AddModelError(
                        "content.isPublished",
                        Services.TextService.Localize(
                            "publish/contentPublishedFailedExpired",
                            DefaultCulture,
                            new[] { $"{status.ContentItem.Name} ({status.ContentItem.Id})" }).Trim());
                    break;
                case PublishStatusType.FailedIsTrashed:
                    //TODO: We should add proper error messaging for this!
                    break;
                case PublishStatusType.FailedContentInvalid:
                    ModelState.AddModelError(
                        "content.isPublished",
                        Services.TextService.Localize(
                            "publish/contentPublishedFailedInvalid",
                            DefaultCulture,
                            new[]
                            {
                                $"{status.ContentItem.Name} ({status.ContentItem.Id})",
                                string.Join(",", status.InvalidProperties.Select(x => x.Alias))
                            }).Trim());
                    break;
                case PublishStatusType.Success:
                case PublishStatusType.SuccessAlreadyPublished:
                default:
                    return;
            }
        }

    }
   
}
