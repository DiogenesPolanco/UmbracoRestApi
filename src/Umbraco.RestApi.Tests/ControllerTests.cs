using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Owin.Testing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Owin;
using Umbraco.Core.Configuration;
using Umbraco.Core.PropertyEditors;
using Umbraco.RestApi.Routing;
using Umbraco.RestApi.Tests.TestHelpers;

namespace Umbraco.RestApi.Tests
{
    public abstract class ControllerTests
    {
        [OneTimeSetUp]
        public void FixtureSetUp()
        {
            ConfigurationManager.AppSettings.Set("umbracoPath", "~/umbraco");
            ConfigurationManager.AppSettings.Set("umbracoConfigurationStatus", UmbracoVersion.Current.ToString(3));
            var mockSettings = MockUmbracoSettings.GenerateMockSettings();
            UmbracoConfig.For.CallMethod("SetUmbracoSettings", mockSettings);
        }

        [TearDown]
        public void TearDown()
        {
            //Hack - because Reset is internal
            typeof(PropertyEditorResolver).CallStaticMethod("Reset", true);
            UmbracoRestApiOptionsInstance.Options = new UmbracoRestApiOptions();
        }

        protected async Task<JObject> GetResult(Action<IAppBuilder> appBuilder, Uri uri, HttpStatusCode expectedResult)
        {
            using (var server = TestServer.Create(appBuilder))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = uri,
                    Method = HttpMethod.Get,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(expectedResult, result.StatusCode);

                var djson = JsonConvert.DeserializeObject<JObject>(json);

                return djson;
            }
        }

        protected async Task Get_Root_With_OPTIONS(Action<IAppBuilder> appBuilder, string segment)
        {
            using (var server = TestServer.Create(appBuilder))
            {

                var request = new HttpRequestMessage
                {
                    RequestUri = new Uri($"http://testserver/umbraco/rest/v1/{segment}"),
                    Method = HttpMethod.Options,
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

        protected async Task<JObject> Get_Root_Result(Action<IAppBuilder> appBuilder, string segment)
        {
            var djson = await GetResult(appBuilder, new Uri($"http://testserver/umbraco/rest/v1/{segment}"), HttpStatusCode.OK);
            Assert.AreEqual($"/umbraco/rest/v1/{segment}", djson["_links"]["root"]["href"].Value<string>());
            return djson;
        }
        
        protected async Task<JObject> Get_Id_Result(Action<IAppBuilder> appBuilder, string segment)
        {
            return await GetResult(appBuilder, new Uri($"http://testserver/umbraco/rest/v1/{segment}/9d072570-aa6f-4902-8c8d-34a49abc3534"), HttpStatusCode.OK);
        }

        protected async Task<JObject> Get_Metadata_Is_200(Action<IAppBuilder> appBuilder, string segment)
        {
            var djson = await GetResult(appBuilder, new Uri($"http://testserver/umbraco/rest/v1/{segment}/9d072570-aa6f-4902-8c8d-34a49abc3534/meta"), HttpStatusCode.OK);
            return djson;
        }

        protected async Task Post_Is_201_Response(Action<IAppBuilder> appBuilder, string segment, StringContent content)
        {
            using (var server = TestServer.Create(appBuilder))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri($"http://testserver/umbraco/rest/v1/{segment}"),
                    Method = HttpMethod.Post,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));
                request.Content = content;

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);
            }
        }
        
        protected async Task Put_Is_200_Response(Action<IAppBuilder> appBuilder, string segment, StringContent content)
        {
            using (var server = TestServer.Create(appBuilder))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri($"http://testserver/umbraco/rest/v1/{segment}/9d072570-aa6f-4902-8c8d-34a49abc3534"),
                    Method = HttpMethod.Put,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));
                request.Content = content;

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        protected async Task Search_200_Result(Action<IAppBuilder> appBuilder, string segment)
        {
            using (var server = TestServer.Create(appBuilder))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri($"http://testserver/umbraco/rest/v1/{segment}/search?query=parentID:\\-1"),
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

        protected async Task Get_Children_Is_200_Response(Action<IAppBuilder> appBuilder, string segment)
        {
            using (var server = TestServer.Create(appBuilder))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri($"http://testserver/umbraco/rest/v1/{segment}/9d072570-aa6f-4902-8c8d-34a49abc3534/children"),
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
        
        protected async Task Get_Descendants_Is_200_Response(Action<IAppBuilder> appBuilder, string segment)
        {
            using (var server = TestServer.Create(appBuilder))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri($"http://testserver/umbraco/rest/v1/{segment}/9d072570-aa6f-4902-8c8d-34a49abc3534/descendants?page=2&size=3&query=hello"),
                    Method = HttpMethod.Get,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        protected async Task Get_Ancestors_Is_200_Response(Action<IAppBuilder> appBuilder, string segment)
        {
            using (var server = TestServer.Create(appBuilder))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri($"http://testserver/umbraco/rest/v1/{segment}/9d072570-aa6f-4902-8c8d-34a49abc3534/ancestors?page=2&size=3&query=hello"),
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
    }
}