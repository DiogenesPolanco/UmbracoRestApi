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
using umbraco.BusinessLogic.Actions;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Persistence.DatabaseModelDefinitions;
using Umbraco.Core.PropertyEditors;
using Umbraco.RestApi.Routing;
using Umbraco.RestApi.Tests.TestHelpers;
using Task = System.Threading.Tasks.Task;

namespace Umbraco.RestApi.Tests
{
    [TestFixture]
    public class MediaControllerTests : ControllerTests
    {
        [Test]
        public async Task Get_Root_With_OPTIONS()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services,
                (testServices) =>
                {
                    var mockMediaService = Mock.Get(testServices.ServiceContext.MediaService);
                    mockMediaService.Setup(x => x.GetRootMedia()).Returns(new[]
                    {
                        ModelMocks.SimpleMockedMedia(123, -1),
                        ModelMocks.SimpleMockedMedia(456, -1)
                    });

                    mockMediaService.Setup(x => x.GetChildren(123)).Returns(new[] { ModelMocks.SimpleMockedMedia(789, 123) });
                    mockMediaService.Setup(x => x.GetChildren(456)).Returns(new[] { ModelMocks.SimpleMockedMedia(321, 456) });
                });

            await Get_Root_With_OPTIONS(startup.UseDefaultTestSetup, RouteConstants.MediaSegment);
        }

        [Test]
        public async Task Get_Root_Result()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services,
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 123, 456);

                    var mockMediaService = Mock.Get(testServices.ServiceContext.MediaService);
                    mockMediaService.Setup(x => x.GetRootMedia()).Returns(new[]
                    {
                        ModelMocks.SimpleMockedMedia(123, -1),
                        ModelMocks.SimpleMockedMedia(456, -1)
                    });

                    mockMediaService.Setup(x => x.GetChildren(123)).Returns(new[] { ModelMocks.SimpleMockedMedia(789, 123) });
                    mockMediaService.Setup(x => x.GetChildren(456)).Returns(new[] { ModelMocks.SimpleMockedMedia(321, 456) });
                });

            var djson = await Get_Root_Result(startup.UseDefaultTestSetup, RouteConstants.MediaSegment);
            Assert.AreEqual(2, djson["_links"]["content"].Count());
            Assert.AreEqual(2, djson["_embedded"]["content"].Count());
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

                    var mockMediaService = Mock.Get(testServices.ServiceContext.MediaService);
                    mockMediaService.Setup(x => x.GetByIds(It.IsAny<IEnumerable<int>>()))
                        .Returns(new[]
                        {
                            ModelMocks.SimpleMockedMedia(789),
                            ModelMocks.SimpleMockedMedia(456)
                        });
                });

            await Search_200_Result(startup.UseDefaultTestSetup, RouteConstants.MediaSegment);
        }

        [Test]
        public async Task Get_Id_Result()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                 (testServices) =>
                 {
                     MockServicesForAuthorizationSuccess(testServices, 123);

                     var mockMediaService = Mock.Get(testServices.ServiceContext.MediaService);

                     mockMediaService.Setup(x => x.GetById(It.IsAny<int>())).Returns(() => ModelMocks.SimpleMockedMedia());

                     mockMediaService.Setup(x => x.GetChildren(It.IsAny<int>())).Returns(new List<IMedia>(new[] { ModelMocks.SimpleMockedMedia(789) }));

                     mockMediaService.Setup(x => x.HasChildren(It.IsAny<int>())).Returns(true);
                 });

            var djson = await Get_Id_Result(startup.UseDefaultTestSetup, RouteConstants.MediaSegment);
            Assert.AreEqual($"/umbraco/rest/v1/{RouteConstants.MediaSegment}/123", djson["_links"]["self"]["href"].Value<string>());
            Assert.AreEqual($"/umbraco/rest/v1/{RouteConstants.MediaSegment}/456", djson["_links"]["parent"]["href"].Value<string>());
            Assert.AreEqual($"/umbraco/rest/v1/{RouteConstants.MediaSegment}/123/children{{?page,size,query}}", djson["_links"]["children"]["href"].Value<string>());
            Assert.AreEqual($"/umbraco/rest/v1/{RouteConstants.MediaSegment}", djson["_links"]["root"]["href"].Value<string>());

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

                     var mockMediaService = Mock.Get(testServices.ServiceContext.MediaService);

                     mockMediaService.Setup(x => x.GetById(It.IsAny<int>())).Returns(() => ModelMocks.SimpleMockedMedia());
                     mockMediaService.Setup(x => x.GetChildren(It.IsAny<int>())).Returns(new List<IMedia>(new[] { ModelMocks.SimpleMockedMedia(789) }));
                     mockMediaService.Setup(x => x.HasChildren(It.IsAny<int>())).Returns(true);

                     var mockTextService = Mock.Get(testServices.ServiceContext.TextService);

                     mockTextService.Setup(x => x.Localize(It.IsAny<string>(), It.IsAny<CultureInfo>(), It.IsAny<IDictionary<string, string>>()))
                         .Returns((string input, CultureInfo culture, IDictionary<string, string> tokens) => input);
                 });

            await Get_Metadata_Is_200(startup.UseDefaultTestSetup, RouteConstants.MediaSegment);
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

            await Get_Children_Is_200_Response(startup.UseDefaultTestSetup, RouteConstants.MediaSegment);
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

            await base.Get_Descendants_Is_200_Response(startup.UseDefaultTestSetup, RouteConstants.MediaSegment);
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

            await base.Get_Ancestors_Is_200_Response(startup.UseDefaultTestSetup, RouteConstants.MediaSegment);
        }

        [Test]
        public async Task Get_Children_Is_200_With_Params_Result()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 123);

                    var mockMediaService = Mock.Get(testServices.ServiceContext.MediaService);

                    mockMediaService.Setup(x => x.GetById(It.IsAny<int>())).Returns(() => ModelMocks.SimpleMockedMedia());

                    long total = 6;
                    mockMediaService.Setup(x => x.GetPagedChildren(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), out total, It.IsAny<string>(), Direction.Ascending, It.IsAny<string>()))
                        .Returns(new List<IMedia>(new[]
                        {
                            ModelMocks.SimpleMockedMedia(789),
                            ModelMocks.SimpleMockedMedia(456)
                        }));

                    mockMediaService.Setup(x => x.HasChildren(It.IsAny<int>())).Returns(true);
                });

            using (var server = TestServer.Create(builder => startup.UseDefaultTestSetup(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri($"http://testserver/umbraco/rest/v1/{RouteConstants.MediaSegment}/9d072570-aa6f-4902-8c8d-34a49abc3534/children?page=2&size=2"),
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
                    MediaServiceMocks.SetupMocksForPost(testServices.ServiceContext);
                });

            await base.Post_Is_201_Response(startup.UseDefaultTestSetup, RouteConstants.MediaSegment, new StringContent(@"{
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
                    MediaServiceMocks.SetupMocksForPost(testServices.ServiceContext);
                });

            using (var server = TestServer.Create(builder => startup.UseDefaultTestSetup(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}", RouteConstants.MediaSegment)),
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
                    MediaServiceMocks.SetupMocksForPost(testServices.ServiceContext);
                });

            using (var server = TestServer.Create(builder => startup.UseDefaultTestSetup(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}", RouteConstants.MediaSegment)),
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
                    MediaServiceMocks.SetupMocksForPost(testServices.ServiceContext);

                    var mockPropertyEditor = Mock.Get(PropertyEditorResolver.Current);
                    mockPropertyEditor.Setup(x => x.GetByAlias("testEditor")).Returns(new ModelMocks.SimplePropertyEditor());
                });

            using (var server = TestServer.Create(builder => startup.UseDefaultTestSetup(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}", RouteConstants.MediaSegment)),
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
        public async Task Put_Is_200_Response()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    MockServicesForAuthorizationSuccess(testServices, 456);
                    MediaServiceMocks.SetupMocksForPost(testServices.ServiceContext);
                });

            await base.Put_Is_200_Response(startup.UseDefaultTestSetup, RouteConstants.MediaSegment, new StringContent(@"{
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
                Mock.Get(testServices.ServiceContext.MediaService)
                    .Setup(x => x.GetById(contentId))
                    .Returns(ModelMocks.SimpleMockedMedia(contentId));                                
            }

            Mock.Get(testServices.ServiceContext.EntityService)
                .Setup(x => x.GetAllPaths(UmbracoObjectTypes.Media, It.IsAny<int[]>()))
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