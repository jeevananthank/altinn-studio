using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;

using Altinn.Common.PEP.Interfaces;

using Altinn.Platform.Storage.UnitTest.Mocks;
using Altinn.Platform.Storage.UnitTest.Mocks.Authentication;
using Altinn.Platform.Storage.UnitTest.Utils;
using Altinn.Platform.Storage.Interface.Models;
using Altinn.Platform.Storage.Repository;
using Altinn.Platform.Storage.Wrappers;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Azure.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Moq;
using Newtonsoft.Json;
using Xunit;
using Altinn.Platform.Storage.UnitTest.Mocks.Repository;
using App.IntegrationTests.Utils;

namespace Altinn.Platform.Storage.UnitTest.TestingControllers
{
    /// <summary>
    /// Represents a group of tests using the TestServer system to perform integration tests.
    /// Tests using the TestServer should ideally be limited to testing of controllers.
    /// </summary>
    public partial class IntegrationTests
    {
        public class InstancesControllerTests : IClassFixture<WebApplicationFactory<Startup>>
        {
            private const string BasePath = "storage/api/v1/instances";

            private readonly WebApplicationFactory<Startup> _factory;
            private readonly Mock<IInstanceRepository> _instanceRepository;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="factory">The web application factory.</param>
            public InstancesControllerTests(WebApplicationFactory<Startup> factory)
            {
                _factory = factory;
                _instanceRepository = new Mock<IInstanceRepository>();
            }

            /// <summary>
            /// Test case: User has to low authentication level. 
            /// Expected: Returns status forbidden.
            /// </summary>
            [Fact]
            public async void Get_UserHasTooLowAuthLv_ReturnsStatusForbidden()
            {
                // Arrange
                int instanceOwnerPartyId = 1337;
                string instanceGuid = "20475edd-dc38-4ae0-bd64-1b20643f506c";
                string requestUri = $"{BasePath}/{instanceOwnerPartyId}/{instanceGuid}";

                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetToken(1337, 0);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.GetAsync(requestUri);

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }


            [Fact]
            public async void Get_One_Ok()
            {
                // Arrange
                int instanceOwnerPartyId = 1337;
                string instanceGuid = "46133fb5-a9f2-45d4-90b1-f6d93ad40713";
                string requestUri = $"{BasePath}/{instanceOwnerPartyId}/{instanceGuid}";

                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetToken(1337, 3);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.GetAsync(requestUri);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                string responseContent = await response.Content.ReadAsStringAsync();
                Instance instance = (Instance)JsonConvert.DeserializeObject(responseContent, typeof(Instance));
                Assert.Equal("1337", instance.InstanceOwner.PartyId);
            }


            [Fact]
            public async void Get_One_Twice_Ok()
            {
                // Arrange
                int instanceOwnerPartyId = 1337;
                string instanceGuid = "377efa97-80ee-4cc6-8d48-09de12cc273d";
                string requestUri = $"{BasePath}/{instanceOwnerPartyId}/{instanceGuid}";

                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetToken(1337, 3);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                RequestTracker.Clear();
                HttpResponseMessage response = await client.GetAsync(requestUri);
                HttpResponseMessage response2 = await client.GetAsync(requestUri);

                // Assert
                Assert.Equal(1, RequestTracker.GetRequestCount("GetDecisionForRequest"));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                string responseContent = await response.Content.ReadAsStringAsync();
                Instance instance = (Instance)JsonConvert.DeserializeObject(responseContent, typeof(Instance));
                Assert.Equal("1337", instance.InstanceOwner.PartyId);
                Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
            }

            /// <summary>
            /// Test case: User tries to access element that he is not authorized for
            /// Expected: Returns status forbidden.
            /// </summary>
            [Fact]
            public async void Get_ReponseIsDeny_ReturnsStatusForbidden()
            {
                // Arrange
                int instanceOwnerPartyId = 1337;
                string instanceGuid = "23d6aa98-df3b-4982-8d8a-8fe67a53b828";
                string requestUri = $"{BasePath}/{instanceOwnerPartyId}/{instanceGuid}";

                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetToken(1, 3);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.GetAsync(requestUri);

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            /// <summary>
            /// Test case: Response is deny. 
            /// Expected: Returns status forbidden.
            /// </summary>
            [Fact]
            public async void Post_ReponseIsDeny_ReturnsStatusForbidden()
            {
                // Arrange
                string appId = "tdd/endring-av-navn";
                string requestUri = $"{BasePath}?appId={appId}";

                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetToken(-1);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Laste opp test instance.. 
                Instance instance = new Instance() { InstanceOwner = new InstanceOwner() { PartyId = "1337" }, Org = "tdd", AppId = "tdd/endring-av-navn" };

                // Act
                HttpResponseMessage response = await client.PostAsync(requestUri, new StringContent(JsonConvert.SerializeObject(instance), Encoding.UTF8, "application/json"));

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            /// <summary>
            /// Test case: User has to low authentication level. 
            /// Expected: Returns status forbidden.
            /// </summary>
            [Fact]
            public async void Post_UserHasTooLowAuthLv_ReturnsStatusForbidden()
            {
                // Arrange
                string appId = "tdd/endring-av-navn";
                string requestUri = $"{BasePath}?appId={appId}";

                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetToken(1337,0);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Laste opp test instance.. 
                Instance instance = new Instance() { InstanceOwner = new InstanceOwner() { PartyId = "1337" }, Org = "tdd", AppId = "tdd/endring-av-navn" };

                // Act
                HttpResponseMessage response = await client.PostAsync(requestUri, new StringContent(JsonConvert.SerializeObject(instance), Encoding.UTF8, "application/json"));

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }


            /// <summary>
            /// Test case: User has to low authentication level. 
            /// Expected: Returns status forbidden.
            /// </summary>
            [Fact]
            public async void Post_Ok()
            {
                // Arrange
                string appId = "tdd/endring-av-navn";
                string requestUri = $"{BasePath}?appId={appId}";

                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetToken(1337, 3);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Laste opp test instance.. 
                Instance instance = new Instance() { InstanceOwner = new InstanceOwner() { PartyId = "1337" }, Org = "tdd", AppId = "tdd/endring-av-navn" };

                // Act
                HttpResponseMessage response = await client.PostAsync(requestUri, new StringContent(JsonConvert.SerializeObject(instance), Encoding.UTF8, "application/json"));

                // Assert
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                string json = await response.Content.ReadAsStringAsync();
                Instance createdInstance = JsonConvert.DeserializeObject<Instance>(json);

                TestDataUtil.DeleteInstanceAndData(1337, createdInstance.Id.Split("/")[1]);
            }

            /// <summary>
            /// Test case: User has to low authentication level. 
            /// Expected: Returns status forbidden.
            /// </summary>
            [Fact]
            public async void Delete_UserHasTooLowAuthLv_ReturnsStatusForbidden()
            {
                // Arrange
                int instanceOwnerId = 1337;
                string instanceGuid = "7e6cc8e2-6cd4-4ad4-9ce8-c37a767677b5";
                 
                string requestUri = $"{BasePath}/{instanceOwnerId}/{instanceGuid}";

                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetToken(1337, 0);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.DeleteAsync(requestUri);

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            /// <summary>
            /// Test case: User tries to delete a element it is not authorized to delete
            /// Expected: Returns status forbidden.
            /// </summary>
            [Fact]
            public async void Delete_ReponseIsDeny_ReturnsStatusForbidden()
            {
                // Arrange
                int instanceOwnerId = 1337;
                string instanceGuid = "7e6cc8e2-6cd4-4ad4-9ce8-c37a767677b5";

                string requestUri = $"{BasePath}/{instanceOwnerId}/{instanceGuid}";

                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetToken(1, 3);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.DeleteAsync(requestUri);

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            /// <summary>
            /// Test case: Get Multiple instances without specifying org.
            /// Expected: Returns status bad request.
            /// </summary>
            [Fact]
            public async void GetMany_NoOrgDefined_ReturnsBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}";

                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetOrgToken("testOrg", scope: "altinn:instances.read");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.GetAsync(requestUri);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            /// <summary>
            /// Test case: Get Multiple instances using client with incorrect scope.
            /// Expected: Returns status forbidden.
            /// </summary>
            [Fact]
            public async void GetMany_IncorrectScope_ReturnsForbidden()
            {
                // Arrange
                string requestUri = $"{BasePath}?org=testOrg";

                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetOrgToken("testOrg", scope: "altinn:instances.write");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.GetAsync(requestUri);

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            /// <summary>
            /// Test case: Response is deny. 
            /// Expected: Returns status forbidden.
            /// </summary>
            [Fact]
            public async void GetMany_QueryingDifferentOrgThanInClaims_ReturnsForbidden()
            {
                // Arrange
                string requestUri = $"{BasePath}?org=paradiseHotelOrg";

                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetOrgToken("testOrg", scope: "altinn:instances.read");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.GetAsync(requestUri);

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   A stakeholder calls the complete operation to indicate that they consider the instance as completed.
            ///   The stakeholder is authorized and it is the first times they make this call.
            /// Result:
            ///   The given instance is updated with a new entry in CompleteConfirmations.
            /// </summary>
            [Fact]
            public async void AddCompleteConfirmation_PostAsValidAppOwner_RespondsWithUpdatedInstance()
            {
                // Arrange
                string org = "tdd";
                int instanceOwnerPartyId = 1337;
                string instanceGuid = "2f7fa5ce-e878-4e1f-a241-8c0eb1a83eab";
                TestDataUtil.DeleteInstanceAndData(instanceOwnerPartyId, new Guid(instanceGuid));
                TestDataUtil.PrepareInstance(instanceOwnerPartyId, new Guid(instanceGuid));
                string requestUri = $"{BasePath}/{instanceOwnerPartyId}/{instanceGuid}/complete";

                Mock<IInstanceRepository> instanceRepository = new Mock<IInstanceRepository>();
               
                HttpClient client = GetTestClient();

                string token = PrincipalUtil.GetOrgToken(org);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.PostAsync(requestUri, new StringContent(string.Empty));

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                string json = await response.Content.ReadAsStringAsync();
                Instance updatedInstance = JsonConvert.DeserializeObject<Instance>(json);

                // Don't compare original and updated instance in asserts. The two instances are identical.
                Assert.NotNull(updatedInstance);
                Assert.Equal(org, updatedInstance.CompleteConfirmations[0].StakeholderId);
                Assert.Equal("111111111", updatedInstance.LastChangedBy);

                TestDataUtil.DeleteInstanceAndData(instanceOwnerPartyId, new Guid(instanceGuid));
            }

            /// <summary>
            /// Scenario:
            ///   A stakeholder calls the complete operation to indicate that they consider the instance as completed.
            ///   Something goes wrong when trying to save the updated instancee.
            /// Result:
            ///   The operation returns status InternalServerError
            /// </summary>
            [Fact]
            public async void AddCompleteConfirmation_ExceptionDuringInstanceUpdate_ReturnsInternalServerError()
            {
                // Arrange
                string org = "tdd";
                int instanceOwnerPartyId = 1337;
                string instanceGuid = "d3b326de-2dd8-49a1-834a-b1d23b11e540";
                string requestUri = $"{BasePath}/{instanceOwnerPartyId}/{instanceGuid}/complete";

                Mock<IInstanceRepository> instanceRepository = new Mock<IInstanceRepository>();

                HttpClient client = GetTestClient();

                string token = PrincipalUtil.GetOrgToken(org);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.PostAsync(requestUri, new StringContent(string.Empty));

                // Assert
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   A stakeholder calls the complete operation to indicate that they consider the instance as completed, but
            ///   they have already done so from before. The API makes no changes and return the original instancee.
            /// Result:
            ///   The given instancee keeps the existing complete confirmation.
            /// </summary>
            [Fact]
            public async void AddCompleteConfirmation_PostAsValidAppOwnerTwice_RespondsWithSameInstance()
            {
                // Arrange
                string org = "tdd";
                int instanceOwnerPartyId = 1337;
                string instanceGuid = "ef1b16fc-4566-4577-b2d8-db74fbee4f7c";
                string requestUri = $"{BasePath}/{instanceOwnerPartyId}/{instanceGuid}/complete";

                Mock<IInstanceRepository> instanceRepository = new Mock<IInstanceRepository>();

                HttpClient client = GetTestClient();

                string token = PrincipalUtil.GetOrgToken(org);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.PostAsync(requestUri, new StringContent(string.Empty));

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                string json = await response.Content.ReadAsStringAsync();
                Instance updatedInstance = JsonConvert.DeserializeObject<Instance>(json);

                // Don't compare original and updated instance in asserts. The two instances are identical.
                Assert.NotNull(updatedInstance);
                Assert.Equal(org, updatedInstance.CompleteConfirmations[0].StakeholderId);
                Assert.Equal("1337", updatedInstance.LastChangedBy);
                // Verify it is the stored instance that is returned
                Assert.Equal(6, updatedInstance.CompleteConfirmations[0].ConfirmedOn.Minute);

            }

            /// <summary>
            /// Scenario:
            ///   A stakeholder calls the complete operation to indicate that they consider the instance as completed, but
            ///   the attempt to get the instance from the document database fails in an exception.
            /// Result:
            ///   The response has status code 500.
            /// </summary>
            [Fact]
            public async void AddCompleteConfirmation_CompleteNonExistantInstance_ExceptionDuringAuthorization_RespondsWithInternalServerError()
            {
                // Arrange
                string org = "tdd";
                int instanceOwnerPartyId = 1337;
                string instanceGuid = "406d1e74-e4f5-4df1-833f-06ef16246a6f";
                string requestUri = $"{BasePath}/{instanceOwnerPartyId}/{instanceGuid}/complete";

                HttpClient client = GetTestClient();

                string token = PrincipalUtil.GetOrgToken(org);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.PostAsync(requestUri, new StringContent(string.Empty));

                // Assert
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   A stakeholder calls the complete operation to indicate that they consider the instance as completed, but
            ///   the attempt to get the instance from the document database fails in an exception.
            /// Result:
            ///   The response has status code 500.
            /// </summary>
            [Fact]
            public async void AddCompleteConfirmation_AttemptToCompleteInstanceAsUser_ReturnsForbidden()
            {
                // Arrange
                string org = "brg";
                int instanceOwnerPartyId = 1337;
                string instanceGuid = "8727385b-e7cb-4bf2-b042-89558c612826";
                string requestUri = $"{BasePath}/{instanceOwnerPartyId}/{instanceGuid}/complete";

                HttpClient client = GetTestClient();

                string token = PrincipalUtil.GetOrgToken(org);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.PostAsync(requestUri, new StringContent(string.Empty));

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            private HttpClient GetTestClient()
            {
                Mock<IApplicationRepository> applicationRepository = new Mock<IApplicationRepository>();
                Application testApp1 = new Application() { Id = "test/testApp1", Org = "test" };

                applicationRepository.Setup(s => s.FindOne(It.Is<string>(p => p.Equals("test/testApp1")), It.IsAny<string>())).ReturnsAsync(testApp1);
                
                // No setup required for these services. They are not in use by the InstanceController
                Mock<IDataRepository> dataRepository = new Mock<IDataRepository>();
                Mock<ISasTokenProvider> sasTokenProvider = new Mock<ISasTokenProvider>();
                Mock<IKeyVaultClientWrapper> keyVaultWrapper = new Mock<IKeyVaultClientWrapper>();

                Program.ConfigureSetupLogging();
                HttpClient client = _factory.WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        services.AddSingleton<IApplicationRepository, ApplicationRepositoryMock>();
                        services.AddSingleton(dataRepository.Object);
                        services.AddSingleton<IInstanceEventRepository, InstanceEventRepositoryMock>();
                        services.AddSingleton<IInstanceRepository, InstanceRepositoryMock>();
                        services.AddSingleton(sasTokenProvider.Object);
                        services.AddSingleton(keyVaultWrapper.Object);
                        services.AddSingleton<IPDP, PepWithPDPAuthorizationMockSI>();
                        services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                    });
                }).CreateClient();

                return client;
            }

            private static DocumentClientException CreateDocumentClientExceptionForTesting(string message, HttpStatusCode httpStatusCode)
            {
                Type type = typeof(DocumentClientException);

                string fullName = type.FullName ?? "wtf?";

                object documentClientExceptionInstance = type.Assembly.CreateInstance(
                    fullName,
                    false,
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new object[] { message, null, null, httpStatusCode, null },
                    null,
                    null);

                return (DocumentClientException)documentClientExceptionInstance;
            }
        }
    }
}
