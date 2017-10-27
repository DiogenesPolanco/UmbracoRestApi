using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Examine;
using Examine.SearchCriteria;
using Microsoft.Owin.Testing;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Owin;
using umbraco.BusinessLogic.Actions;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Persistence.DatabaseModelDefinitions;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Publishing;
using Umbraco.Core.Security;
using Umbraco.RestApi.Routing;
using Umbraco.RestApi.Tests.TestHelpers;
using Task = System.Threading.Tasks.Task;

namespace Umbraco.RestApi.Tests
{
    [TestFixture]
    public class ContentControllerTests : ControllerTests
    {
        [Test]
        public async Task Get_Root_With_OPTIONS()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services,
                (testServices) =>
                {
                    var mockContentService = Mock.Get(testServices.ServiceContext.ContentService);
                    mockContentService.Setup(x => x.GetRootContent()).Returns(new[]
                    {
                        ModelMocks.SimpleMockedContent(123, -1),
                        ModelMocks.SimpleMockedContent(456, -1)
                    });

                    mockContentService.Setup(x => x.GetChildren(123)).Returns(new[] { ModelMocks.SimpleMockedContent(789, 123) });
                    mockContentService.Setup(x => x.GetChildren(456)).Returns(new[] { ModelMocks.SimpleMockedContent(321, 456) });
                });

            await Get_Root_With_OPTIONS(startup.UseDefaultTestSetup, RouteConstants.ContentSegment);
        }

        [Test]
        public async Task Get_Root_Result()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services,
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 123, 456);

                    var mockContentService = Mock.Get(testServices.ServiceContext.ContentService);
                    mockContentService.Setup(x => x.GetRootContent()).Returns(new[]
                    {
                        ModelMocks.SimpleMockedContent(123, -1),
                        ModelMocks.SimpleMockedContent(456, -1)
                    });

                    mockContentService.Setup(x => x.GetChildren(123)).Returns(new[] { ModelMocks.SimpleMockedContent(789, 123) });
                    mockContentService.Setup(x => x.GetChildren(456)).Returns(new[] { ModelMocks.SimpleMockedContent(321, 456) });
                });

            var djson = await Get_Root_Result(startup.UseDefaultTestSetup, RouteConstants.ContentSegment);
            Assert.AreEqual(2, djson["_links"]["content"].Count());
            Assert.AreEqual(2, djson["_embedded"]["content"].Count());
        }

        [Test]
        public async Task Get_Root_Result_With_Custom_Start_Nodes()
        {
            //represents the node(s) that the user will receive as their root based on their custom start node
            var rootNodes = ModelMocks.SimpleMockedContent(123, 456);

            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services,
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 456);

                    var mockContentService = Mock.Get(testServices.ServiceContext.ContentService);
                    mockContentService.Setup(x => x.GetByIds(It.IsAny<int[]>())).Returns(new[]
                    {
                        rootNodes
                    });

                    mockContentService.Setup(x => x.GetChildren(123)).Returns(new[] { ModelMocks.SimpleMockedContent(789, 123) });
                    mockContentService.Setup(x => x.GetChildren(456)).Returns(new[] { ModelMocks.SimpleMockedContent(321, 456) });
                });
            
            var djson = await Get_Root_Result(app =>
            {
                //we are doing a custom authz for this call so need to change the startup process

                var identity = new UmbracoBackOfficeIdentity(
                    new UserData(Guid.NewGuid().ToString())
                    {
                        Id = 0,
                        Roles = new[] { "admin" },
                        AllowedApplications = new[] { "content", "media", "members" },
                        Culture = "en-US",
                        RealName = "Admin",
                        StartContentNodes = new[] { 456 },
                        StartMediaNodes = new[] { -1 },
                        Username = "admin"
                    });

                var httpConfig = startup.UseTestWebApiConfiguration(app);                
                app.AuthenticateEverything(new AuthenticateEverythingAuthenticationOptions(identity));
                app.UseUmbracoRestApi(startup.ApplicationContext);
                app.UseWebApi(httpConfig);

            }, RouteConstants.ContentSegment);

            Assert.AreEqual(1, djson["_links"]["content"].Count());
            Assert.AreEqual("/umbraco/rest/v1/content/123", djson["_links"]["content"]["href"].Value<string>());
            Assert.AreEqual(1, djson["_embedded"]["content"].Count());
            Assert.AreEqual(rootNodes.Id, djson["_embedded"]["content"].First["id"].Value<int>());
        }

        [Test]
        public async Task Search_200_Result()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices);

                    var mockSearchResults = new Mock<ISearchResults>();
                    mockSearchResults.Setup(results => results.TotalItemCount).Returns(10);
                    mockSearchResults.Setup(results => results.Skip(It.IsAny<int>())).Returns(new[]
                    {
                        new SearchResult() {Id = 789},
                        new SearchResult() {Id = 456},
                    });

                    var mockSearchProvider = Mock.Get(testServices.SearchProvider);
                    mockSearchProvider.Setup(x => x.CreateSearchCriteria()).Returns(Mock.Of<ISearchCriteria>());
                    mockSearchProvider.Setup(x => x.Search(It.IsAny<ISearchCriteria>(), It.IsAny<int>()))
                        .Returns(mockSearchResults.Object);

                    var mockContentService = Mock.Get(testServices.ServiceContext.ContentService);
                    mockContentService.Setup(x => x.GetByIds(It.IsAny<IEnumerable<int>>()))
                        .Returns(new[]
                        {
                            ModelMocks.SimpleMockedContent(789),
                            ModelMocks.SimpleMockedContent(456)
                        });
                });

            await Search_200_Result(startup.UseDefaultTestSetup, RouteConstants.ContentSegment);
        }

        [Test]
        public async Task Get_Id_Result()
        {
            var startup = new TestStartup(
                 //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                 {
                     MockServicesForAuthorizationSuccess(testServices, 123);

                     var mockContentService = Mock.Get(testServices.ServiceContext.ContentService);

                     mockContentService.Setup(x => x.GetById(It.IsAny<int>())).Returns(() => ModelMocks.SimpleMockedContent());

                     mockContentService.Setup(x => x.GetChildren(It.IsAny<int>())).Returns(new List<IContent>(new[] { ModelMocks.SimpleMockedContent(789) }));

                     mockContentService.Setup(x => x.HasChildren(It.IsAny<int>())).Returns(true);
                 });

            var djson = await Get_Id_Result(startup.UseDefaultTestSetup, RouteConstants.ContentSegment);
            Assert.AreEqual($"/umbraco/rest/v1/{RouteConstants.ContentSegment}/123", djson["_links"]["self"]["href"].Value<string>());
            Assert.AreEqual($"/umbraco/rest/v1/{RouteConstants.ContentSegment}/456", djson["_links"]["parent"]["href"].Value<string>());
            Assert.AreEqual($"/umbraco/rest/v1/{RouteConstants.ContentSegment}/123/children{{?page,size,query}}", djson["_links"]["children"]["href"].Value<string>());
            Assert.AreEqual($"/umbraco/rest/v1/{RouteConstants.ContentSegment}", djson["_links"]["root"]["href"].Value<string>());

            var properties = djson["properties"].ToObject<IDictionary<string, object>>();
            Assert.AreEqual(2, properties.Count);
            Assert.IsTrue(properties.ContainsKey("TestProperty1"));
            Assert.IsTrue(properties.ContainsKey("testProperty2"));
        }

        [Test]
        public async Task Get_Metadata_Is_200()
        {
            var startup = new TestStartup(
                 //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                 {
                     MockServicesForAuthorizationSuccess(testServices, 123);

                     var mockContentService = Mock.Get(testServices.ServiceContext.ContentService);

                     mockContentService.Setup(x => x.GetById(It.IsAny<int>())).Returns(() => ModelMocks.SimpleMockedContent());
                     mockContentService.Setup(x => x.GetChildren(It.IsAny<int>())).Returns(new List<IContent>(new[] { ModelMocks.SimpleMockedContent(789) }));
                     mockContentService.Setup(x => x.HasChildren(It.IsAny<int>())).Returns(true);

                     var mockTextService = Mock.Get(testServices.ServiceContext.TextService);

                     mockTextService.Setup(x => x.Localize(It.IsAny<string>(), It.IsAny<CultureInfo>(), It.IsAny<IDictionary<string, string>>()))
                         .Returns((string input, CultureInfo culture, IDictionary<string, string> tokens) => input);
                 });

            await Get_Metadata_Is_200(startup.UseDefaultTestSetup, RouteConstants.ContentSegment);
        }

        [Test]
        public async Task Get_Children_Is_200_Response()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 123);
                });

            await Get_Children_Is_200_Response(startup.UseDefaultTestSetup, RouteConstants.ContentSegment);
        }

        [Test]
        public async Task Get_Children_With_Filter_By_Permissions()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 456);

                    long totalRecs;
                    Mock.Get(testServices.ServiceContext.ContentService)
                        .Setup(x => x.GetPagedChildren(456, It.IsAny<long>(), It.IsAny<int>(), out totalRecs, It.IsAny<string>(), It.IsAny<Direction>(), It.IsAny<string>()))
                        .Returns(new []
                        {
                            ModelMocks.SimpleMockedContent(10),
                            ModelMocks.SimpleMockedContent(11),
                            ModelMocks.SimpleMockedContent(12),
                            ModelMocks.SimpleMockedContent(13),
                        });
                                        
                    Mock.Get(testServices.ServiceContext.UserService)
                        .Setup(x => x.GetPermissions(It.IsAny<IUser>(), It.IsAny<int[]>()))
                        .Returns(() =>
                            new EntityPermissionCollection(new[]
                            {
                                new EntityPermission(1, 10, new[] {ActionBrowse.Instance.Letter.ToString()}),
                                new EntityPermission(1, 11, new[] {ActionSort.Instance.Letter.ToString()}),
                                new EntityPermission(1, 12, new[] {ActionBrowse.Instance.Letter.ToString()}),
                                new EntityPermission(1, 13, new[] { ActionSort.Instance.Letter.ToString()}),
                            }));
                });

            var djson = await GetResult(startup.UseDefaultTestSetup, new Uri($"http://testserver/umbraco/rest/v1/{RouteConstants.ContentSegment}/9d072570-aa6f-4902-8c8d-34a49abc3534/children"), HttpStatusCode.OK);
            Assert.AreEqual(2, djson["_links"]["content"].Count());            
            Assert.AreEqual(2, djson["_embedded"]["content"].Count());
            Assert.AreEqual(10, djson["_embedded"]["content"].First["id"].Value<int>());
            Assert.AreEqual(12, djson["_embedded"]["content"].Last["id"].Value<int>());
        }

        [Test]
        public async Task Get_Descendants_Is_200_Response()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 123);
                });

            await base.Get_Descendants_Is_200_Response(startup.UseDefaultTestSetup, RouteConstants.ContentSegment);
        }

        [Test]
        public async Task Get_Ancestors_Is_200_Response()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 123);
                });

            await base.Get_Ancestors_Is_200_Response(startup.UseDefaultTestSetup, RouteConstants.ContentSegment);
        }

        [Test]
        public async Task Get_Children_Is_200_With_Params_Result()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 123);

                    var mockContentService = Mock.Get(testServices.ServiceContext.ContentService);

                    mockContentService.Setup(x => x.GetById(It.IsAny<int>())).Returns(() => ModelMocks.SimpleMockedContent());

                    long total = 6;
                    mockContentService.Setup(x => x.GetPagedChildren(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), out total, It.IsAny<string>(), Direction.Ascending, It.IsAny<string>()))
                        .Returns(new List<IContent>(new[]
                        {
                            ModelMocks.SimpleMockedContent(789),
                            ModelMocks.SimpleMockedContent(456)
                        }));

                    mockContentService.Setup(x => x.HasChildren(It.IsAny<int>())).Returns(true);
                });
            
            using (var server = TestServer.Create(builder => startup.UseDefaultTestSetup(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri($"http://testserver/umbraco/rest/v1/{RouteConstants.ContentSegment}/9d072570-aa6f-4902-8c8d-34a49abc3534/children?page=2&size=2"),
                    Method = HttpMethod.Get,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);

                var djson = JsonConvert.DeserializeObject<JObject>(json);

                Assert.AreEqual(6, djson["totalResults"].Value<int>());
                Assert.AreEqual(2, djson["page"].Value<int>());
                Assert.AreEqual(2, djson["pageSize"].Value<int>());
                Assert.IsNotNull(djson["_links"]["next"]);
                Assert.IsNotNull(djson["_links"]["prev"]);
                
            }
        }

        [Test]
        public async Task Post_Is_201_Response()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 456);
                    ContentServiceMocks.SetupMocksForPost(testServices.ServiceContext);
                });

            await base.Post_Is_201_Response(startup.UseDefaultTestSetup, RouteConstants.ContentSegment, new StringContent(@"{
  ""contentTypeAlias"": ""testType"",
  ""parentId"": 456,
  ""templateId"": 9,
  ""name"": ""Home"",
  ""properties"": {
    ""TestProperty1"": ""property value1"",
    ""testProperty2"": ""property value2""
  }
}", Encoding.UTF8, "application/json"));
        }

        [Test]
        public async Task Post_Is_400_Validation_Required_Fields()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 456);
                    ContentServiceMocks.SetupMocksForPost(testServices.ServiceContext);
                });

            using (var server = TestServer.Create(builder => startup.UseDefaultTestSetup(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}", RouteConstants.ContentSegment)),
                    Method = HttpMethod.Post,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));
                //NOTE: it is missing
                request.Content = new StringContent(@"{
  ""contentTypeAlias"": """",
  ""parentId"": 456,
  ""templateId"": 9,
  ""name"": """",
  ""properties"": {
    ""TestProperty1"": ""property value1"",
    ""testProperty2"": ""property value2""
  }
}", Encoding.UTF8, "application/json");

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.BadRequest, result.StatusCode);

                var djson = JsonConvert.DeserializeObject<JObject>(json);

                Assert.AreEqual(2, djson["totalResults"].Value<int>());
                Assert.AreEqual("content.ContentTypeAlias", djson["_embedded"]["errors"][0]["logRef"].Value<string>());
                Assert.AreEqual("content.Name", djson["_embedded"]["errors"][1]["logRef"].Value<string>());

            }
        }

        [Test]
        public async Task Post_Is_400_Validation_Property_Missing()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 456);
                    ContentServiceMocks.SetupMocksForPost(testServices.ServiceContext);
                });

            using (var server = TestServer.Create(builder => startup.UseDefaultTestSetup(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}", RouteConstants.ContentSegment)),
                    Method = HttpMethod.Post,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));
                //NOTE: it is missing
                request.Content = new StringContent(@"{
    ""name"": ""test"",  
    ""contentTypeAlias"": ""test"",
  ""parentId"": 456,
  ""templateId"": 9,
  ""properties"": {
    ""thisDoesntExist"": ""property value1"",
    ""testProperty2"": ""property value2""
  }
}", Encoding.UTF8, "application/json");

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.BadRequest, result.StatusCode);

                var djson = JsonConvert.DeserializeObject<JObject>(json);

                Assert.AreEqual(1, djson["totalResults"].Value<int>());
                Assert.AreEqual("content.properties.thisDoesntExist", djson["_embedded"]["errors"][0]["logRef"].Value<string>());

            }
        }

        [Test]
        public async Task Post_Is_400_Validation_Property_Required()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 456);
                    ContentServiceMocks.SetupMocksForPost(testServices.ServiceContext);

                    var mockPropertyEditor = Mock.Get(PropertyEditorResolver.Current);
                    mockPropertyEditor.Setup(x => x.GetByAlias("testEditor")).Returns(new ModelMocks.SimplePropertyEditor());
                });

            using (var server = TestServer.Create(builder => startup.UseDefaultTestSetup(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}", RouteConstants.ContentSegment)),
                    Method = HttpMethod.Post,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));
                //NOTE: it is missing
                request.Content = new StringContent(@"{
    ""name"": ""test"",  
    ""contentTypeAlias"": ""test"",
  ""parentId"": 456,
  ""templateId"": 9,
  ""properties"": {
    ""TestProperty1"": """",
    ""testProperty2"": ""property value2""
  }
}", Encoding.UTF8, "application/json");

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.BadRequest, result.StatusCode);

                var djson = JsonConvert.DeserializeObject<JObject>(json);

                Assert.AreEqual(1, djson["totalResults"].Value<int>());
                Assert.AreEqual("content.properties.TestProperty1.value", djson["_embedded"]["errors"][0]["logRef"].Value<string>());

            }
        }

        [Test]
        public async Task Put_Is_200_Response_Non_Published()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 456);
                    ContentServiceMocks.SetupMocksForPost(testServices.ServiceContext);
                });

            await base.Put_Is_200_Response(startup.UseDefaultTestSetup, RouteConstants.ContentSegment, new StringContent(@"{
  ""contentTypeAlias"": ""testType"",
  ""parentId"": 456,
  ""templateId"": 9,
  ""published"": false,
  ""name"": ""Home"",
  ""properties"": {
    ""TestProperty1"": ""property value1"",
    ""testProperty2"": ""property value2""
  }
}", Encoding.UTF8, "application/json"));
        }

        [Test]
        public async Task Put_Is_200_Response_With_Published()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 456);
                    ContentServiceMocks.SetupMocksForPost(testServices.ServiceContext);
                    var mockContentService = Mock.Get(testServices.ServiceContext.ContentService);
                    mockContentService.Setup(x => x.SaveAndPublishWithStatus(It.IsAny<IContent>(), It.IsAny<int>(), It.IsAny<bool>()))
                        .Returns(Attempt<PublishStatus>.Succeed);
                });

            await base.Put_Is_200_Response(startup.UseDefaultTestSetup, RouteConstants.ContentSegment, new StringContent(@"{
  ""contentTypeAlias"": ""testType"",
  ""parentId"": 456,
  ""templateId"": 9,
  ""published"": true,
  ""name"": ""Home"",
  ""properties"": {
    ""TestProperty1"": ""property value1"",
    ""testProperty2"": ""property value2""
  }
}", Encoding.UTF8, "application/json"));
            
        }

        /// <summary>
        /// Sets up the services to return the correct data based on the Authorization logic for the non-published content controller
        /// </summary>
        /// <param name="testServices"></param>
        /// <param name="contentIds"></param>
        /// <remarks>
        /// Much of this is based on the call to Umbraco Core's ContentController.CheckPermissions which performs quite a few checks.
        /// Ideally we'd move this authorization logic to an interface so we can mock it instead.
        /// </remarks>
        private void MockServicesForAuthorizationSuccess(TestServices testServices, params int[] contentIds)
        {
            foreach (var contentId in contentIds)
            {
                Mock.Get(testServices.ServiceContext.ContentService)
                    .Setup(x => x.GetById(contentId))
                    .Returns(ModelMocks.SimpleMockedContent(contentId));

                Mock.Get(testServices.ServiceContext.UserService)
                    .Setup(x => x.GetPermissionsForPath(It.IsAny<IUser>(), It.IsAny<string>()))
                    .Returns(() =>
                        new EntityPermissionSet(contentId, new EntityPermissionCollection(new[]
                        {
                            new EntityPermission(1, contentId, new[]
                            {
                                ActionBrowse.Instance.Letter.ToString(),
                                ActionNew.Instance.Letter.ToString(),
                                ActionUpdate.Instance.Letter.ToString(),
                                ActionPublish.Instance.Letter.ToString(),
                                ActionDelete.Instance.Letter.ToString(),
                            })
                        })));

                Mock.Get(testServices.ServiceContext.UserService)
                    .Setup(x => x.GetPermissions(It.IsAny<IUser>(), new[] { contentId }))
                    .Returns(() =>
                        new EntityPermissionCollection(new[]
                        {
                            new EntityPermission(1, contentId, new[]
                            {
                                ActionBrowse.Instance.Letter.ToString(),
                                ActionNew.Instance.Letter.ToString(),
                                ActionUpdate.Instance.Letter.ToString(),
                                ActionPublish.Instance.Letter.ToString(),
                                ActionDelete.Instance.Letter.ToString(),
                            })
                        }));
            }

            Mock.Get(testServices.ServiceContext.EntityService)
                .Setup(x => x.GetAllPaths(UmbracoObjectTypes.Document, It.IsAny<int[]>()))
                .Returns((UmbracoObjectTypes objType, int[] ids) =>
                {
                    return ids.Select(i => new EntityPath
                    {
                        Id = i,
                        Path = i == Constants.System.Root ? "-1" : string.Concat("-1,", i)
                    });
                });
                       
        }

    }

}