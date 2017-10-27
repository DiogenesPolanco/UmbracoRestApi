using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Owin.Security.Authorization.WebApi;
using Umbraco.Core.Models;
using Umbraco.RestApi.Models;
using Umbraco.RestApi.Routing;
using Umbraco.RestApi.Security;
using Umbraco.Web;
using Umbraco.Web.WebApi;
using WebApi.Hal;
using Task = System.Threading.Tasks.Task;

namespace Umbraco.RestApi.Controllers
{
    
    [ResourceAuthorize(Policy = AuthorizationPolicies.DefaultRestApi)]
    [UmbracoRoutePrefix("rest/v1/relations")]
    public class RelationsController : UmbracoHalController, IRootController
    {
        /// <summary>
        /// Default ctor
        /// </summary>
        public RelationsController()
        {
        }

        /// <summary>
        /// All dependencies
        /// </summary>
        /// <param name="umbracoContext"></param>
        /// <param name="umbracoHelper"></param>
        public RelationsController(
            UmbracoContext umbracoContext,
            UmbracoHelper umbracoHelper)
            : base(umbracoContext, umbracoHelper)
        { }

        /// <summary>
        /// The root request for relations returns all relation types
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [CustomRoute("")]
        public Task<HttpResponseMessage> Get()
        {   
            var relationTypes = Services.RelationService.GetAllRelationTypes();
            var result = Mapper.Map<IEnumerable<RelationTypeRepresentation>>(relationTypes).ToList();
            var representation = new RelationTypeListRepresentation(result);

            return Task.FromResult(Request.CreateResponse(HttpStatusCode.OK, representation));
        }

        [HttpGet]
        [CustomRoute("relationtype/{alias}")]
        public Task<HttpResponseMessage> GetRelationType(string alias)
        {
            var relType = Services.RelationService.GetRelationTypeByAlias(alias);

            if (relType == null)
                return Task.FromResult(Request.CreateResponse(HttpStatusCode.NotFound));

            var mapped = Mapper.Map<RelationTypeRepresentation>(relType);
            
            return Task.FromResult(Request.CreateResponse(HttpStatusCode.OK, mapped));
        }

        [HttpGet]
        [CustomRoute("children/{id}")]
        public Task<HttpResponseMessage> GetByParent(Guid id, string relationType = null)
        {
            var parent = Services.EntityService.GetByKey(id);            

            if (parent == null)
                return Task.FromResult(Request.CreateResponse(HttpStatusCode.NotFound));
                
            var relations = (string.IsNullOrEmpty(relationType)) ? Services.RelationService.GetByParent(parent) : Services.RelationService.GetByParent(parent, relationType);
            var mapped = relations.Select(CreateRepresentation).ToList();

            var relationsRep = new RelationListRepresentation( mapped );
            return Task.FromResult(Request.CreateResponse(HttpStatusCode.OK, relationsRep));
        }

        [HttpGet]
        [CustomRoute("parents/{id}")]
        public Task<HttpResponseMessage> GetByChild(Guid id, string relationType = null)
        {
            var child = Services.EntityService.GetByKey(id);
            if (child == null)
                return Task.FromResult(Request.CreateResponse(HttpStatusCode.NotFound));

            var type = Services.RelationService.GetRelationTypeByAlias(relationType);
            if (type == null)
                return Task.FromResult(Request.CreateResponse(HttpStatusCode.NotFound));


            var relations = (string.IsNullOrEmpty(relationType)) ? Services.RelationService.GetByChild(child) : Services.RelationService.GetByChild(child, relationType);
            var mapped = relations.Select(CreateRepresentation).ToList();
            var relationsRep = new RelationListRepresentation(mapped);

            return Task.FromResult(Request.CreateResponse(HttpStatusCode.OK, relationsRep));
        }
        



        //RELATIONS CRUD
        [HttpGet]
        [CustomRoute("{id}")]
        public Task<HttpResponseMessage> Get(int id)
        {
            var result = Services.RelationService.GetById(id);

            return Task.FromResult(result == null
                ? Request.CreateResponse(HttpStatusCode.NotFound)
                : Request.CreateResponse(HttpStatusCode.OK, CreateRepresentation(result)));
        }

        /// <summary>
        /// Creates a relation
        /// </summary>
        /// <param name="relation"></param>
        /// <returns></returns>
        [HttpPost]
        [CustomRoute("")]
        public Task<HttpResponseMessage> Post(RelationRepresentation relation)
        {
            if (relation == null) throw new ArgumentNullException(nameof(relation));

            try
            {
                //we cannot continue here if the mandatory items are empty (i.e. name, etc...)
                if (!ModelState.IsValid)
                {
                    throw ValidationException(ModelState, relation, LinkTemplates.Relations.Root);
                }

                var created = Mapper.Map<IRelation>(relation);

                //during the mapping it will try to lookup the relation type so we need to validate that it exists
                if (created.RelationType == null)
                {
                    ModelState.AddModelError("relation.relationTypeAlias", "No relation type found with alias " + relation.RelationTypeAlias);
                    throw ValidationException(ModelState, relation, LinkTemplates.Relations.Root);
                }
                
                Services.RelationService.Save(created);
                
                return Task.FromResult(Request.CreateResponse(HttpStatusCode.Created, CreateRepresentation(created)));
            }
            catch (ModelValidationException exception)
            {
                return Task.FromResult(Request.CreateResponse(HttpStatusCode.BadRequest, exception.Errors));
            }
        }

        [HttpPut]
        [CustomRoute("{id}")]
        public Task<HttpResponseMessage> Put(int id, RelationRepresentation relation)
        {
            try
            {
                var found = Services.RelationService.GetById(id);
                if (found == null)
                    return Task.FromResult(Request.CreateResponse(HttpStatusCode.NotFound));

                Mapper.Map(relation, found);

                //during the mapping it will try to lookup the relation type so we need to validate that it exists
                if (found.RelationType == null)
                {
                    ModelState.AddModelError("relation.relationTypeAlias", "No relation type found with alias " + relation.RelationTypeAlias);
                    throw ValidationException(ModelState, relation, LinkTemplates.Relations.Root);
                }

                Services.RelationService.Save(found);

                return Task.FromResult(Request.CreateResponse(HttpStatusCode.OK, CreateRepresentation(found)));
            }
            catch (ModelValidationException exception)
            {
                return Task.FromResult(Request.CreateResponse(HttpStatusCode.BadRequest, exception.Errors));
            }
        }

        [HttpDelete]
        [CustomRoute("{id}")]
        public virtual Task<HttpResponseMessage> Delete(int id)
        {
            var found = Services.RelationService.GetById(id);
            if (found == null)
                return Task.FromResult(Request.CreateResponse(HttpStatusCode.NotFound));

            Services.RelationService.Delete(found);
            return Task.FromResult(Request.CreateResponse(HttpStatusCode.OK));
        }
        

        private RelationRepresentation CreateRepresentation(IRelation relation)
        {
            if (relation == null) throw new ArgumentNullException(nameof(relation));

            var parentLinkTemplate = GetLinkTemplate(relation.RelationType.ParentObjectType);
            var childLinkTemplate = GetLinkTemplate(relation.RelationType.ChildObjectType);

            var rep = new RelationRepresentation(parentLinkTemplate, childLinkTemplate);
            return Mapper.Map(relation, rep);
        }

        private Link GetLinkTemplate(Guid nodeObjectType)
        {
            switch (nodeObjectType.ToString().ToUpper())
            {
                case Core.Constants.ObjectTypes.Document:
                    return LinkTemplates.PublishedContent.Self;
                case Core.Constants.ObjectTypes.Media:
                    return LinkTemplates.Media.Self;
                case Core.Constants.ObjectTypes.Member:
                    return LinkTemplates.Members.Self;
                default:
                    throw new ArgumentOutOfRangeException();
            }            
        }
        
    }


}
