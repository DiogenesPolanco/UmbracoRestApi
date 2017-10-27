using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Examine.Providers;
using Examine.SearchCriteria;
using Microsoft.Owin.Testing;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Umbraco.Core.Configuration;
using Umbraco.RestApi.Routing;
using Umbraco.RestApi.Tests.TestHelpers;
using Task = System.Threading.Tasks.Task;

namespace Umbraco.RestApi.Tests
{

    //TODO: Make this inherit from ControllerTests to streamline all tests and use the underlying base class logic

    [TestFixture]
    public class PublishedContentControllerTests
    {
        [OneTimeSetUp]
        public void TearDown()
        {
            ConfigurationManager.AppSettings.Set("umbracoPath", "~/umbraco");
            ConfigurationManager.AppSettings.Set("umbracoConfigurationStatus", UmbracoVersion.Current.ToString(3));
        }

        [Test]
        public async Task Get_Children_Is_200_Response()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    var mockTypedContent = Mock.Get(testServices.PublishedContentQuery);
                    mockTypedContent.Setup(x => x.TypedContent(It.IsAny<int>())).Returns(() => ModelMocks.SimpleMockedPublishedContent(123, 456, 789));
                });

            using (var server = TestServer.Create(builder => startup.UseDefaultTestSetup(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}/{1}/9d072570-aa6f-4902-8c8d-34a49abc3534/children", RouteConstants.ContentSegment, RouteConstants.PublishedSegment)),
                    Method = HttpMethod.Get,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        [Test]
        public async Task Search_200_Result()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    var mockTypedContent = Mock.Get(testServices.PublishedContentQuery);
                    mockTypedContent.Setup(x => x.TypedSearch(It.IsAny<ISearchCriteria>(), It.IsAny<BaseSearchProvider>()))
                        .Returns(new[]
                        {
                            ModelMocks.SimpleMockedPublishedContent(123, -1, 789),
                            ModelMocks.SimpleMockedPublishedContent(456, -1, 321)
                        });

                    var mockSearchProvider = Mock.Get(testServices.SearchProvider);
                    mockSearchProvider.Setup(x => x.CreateSearchCriteria()).Returns(Mock.Of<ISearchCriteria>());                  

                });

            using (var server = TestServer.Create(builder => startup.UseDefaultTestSetup(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}/{1}/search?query=parentID:\\-1", RouteConstants.ContentSegment, RouteConstants.PublishedSegment)),
                    Method = HttpMethod.Get,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        [Test]
        public async Task Get_Id_Result()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (testServices) =>
                {
                    var mockTypedContent = Mock.Get(testServices.PublishedContentQuery);
                    mockTypedContent.Setup(x => x.TypedContent(It.IsAny<int>())).Returns(() => ModelMocks.SimpleMockedPublishedContent(123, 456, 789));
                });

            using (var server = TestServer.Create(builder => startup.UseDefaultTestSetup(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}/{1}/9d072570-aa6f-4902-8c8d-34a49abc3534", RouteConstants.ContentSegment, RouteConstants.PublishedSegment)),
                    Method = HttpMethod.Get,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
                
                Assert.AreEqual("application/hal+json", result.Content.Headers.ContentType.MediaType);
                Assert.IsAssignableFrom<StreamContent>(result.Content);
                
                //TODO: Need to assert more values!

                var djson = JsonConvert.DeserializeObject<JObject>(json);

                Assert.AreEqual("/umbraco/rest/v1/content/published/123", djson["_links"]["self"]["href"].Value<string>());
                Assert.AreEqual("/umbraco/rest/v1/content/published/456", djson["_links"]["parent"]["href"].Value<string>());
                Assert.AreEqual("/umbraco/rest/v1/content/published/123/children{?page,size,query}", djson["_links"]["children"]["href"].Value<string>());
                Assert.AreEqual("/umbraco/rest/v1/content/published", djson["_links"]["root"]["href"].Value<string>());

                var properties = djson["properties"].ToObject<IDictionary<string, object>>();
                Assert.AreEqual(2, properties.Count());
                Assert.IsTrue(properties.ContainsKey("TestProperty1"));
                Assert.IsTrue(properties.ContainsKey("testProperty2"));
            }
        }

        [Test]
        public async Task Get_Root_With_OPTIONS()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services,
                (testServices) =>
                {
                    var mockTypedContent = Mock.Get(testServices.PublishedContentQuery);
                    mockTypedContent.Setup(x => x.TypedContentAtRoot()).Returns(new[]
                    {
                        ModelMocks.SimpleMockedPublishedContent(123, -1, 789),
                        ModelMocks.SimpleMockedPublishedContent(456, -1, 321)
                    });
                });

            using (var server = TestServer.Create(builder => startup.UseDefaultTestSetup(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri($"http://testserver/umbraco/rest/v1/{RouteConstants.ContentSegment}/{RouteConstants.PublishedSegment}"),
                    Method = HttpMethod.Get,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));
                request.Headers.Add("Access-Control-Request-Headers", "accept, authorization");
                request.Headers.Add("Access-Control-Request-Method", "GET");
                request.Headers.Add("Origin", "http://localhost:12061");
                request.Headers.Add("Referer", "http://localhost:12061/browser.html");

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        [Test]
        public async Task Get_Root_Result()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services,
                (testServices) =>
                {
                    var mockTypedContent = Mock.Get(testServices.PublishedContentQuery);
                    mockTypedContent.Setup(x => x.TypedContentAtRoot()).Returns(new[]
                    {
                        ModelMocks.SimpleMockedPublishedContent(123, -1, 789),
                        ModelMocks.SimpleMockedPublishedContent(456, -1, 321)
                    });
                });

            using (var server = TestServer.Create(builder => startup.UseDefaultTestSetup(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}/{1}", RouteConstants.ContentSegment, RouteConstants.PublishedSegment)),
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

                Assert.AreEqual("/umbraco/rest/v1/content/published", djson["_links"]["root"]["href"].Value<string>());
                Assert.AreEqual(2, djson["_links"]["content"].Count());
                Assert.AreEqual(2, djson["_embedded"]["content"].Count()); 
            }
        }

    }
}

