using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;

using Altinn.Common.PEP.Interfaces;

using Altinn.Platform.Storage.Helpers;
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
using System.IO;
using Altinn.Platform.Storage.UnitTest.Mocks.Repository;
using App.IntegrationTests.Utils;

namespace Altinn.Platform.Storage.UnitTest.TestingControllers
{
    public partial class IntegrationTests
    {

        public class MessageboxInstancesControllerTests : IClassFixture<WebApplicationFactory<Startup>>
        {
            private const string BasePath = "/storage/api/v1";
            private const string org = "tdd";
            private const string app = "test-applikasjon-1";   
            private readonly WebApplicationFactory<Startup> _factory;
            private readonly string _validToken, _validTokenUsr3;

            /// <summary>
            /// Initializes a new instance of the <see cref="MessageboxInstancesControllerTests"/> class with the given <see cref="WebApplicationFactory{TStartup}"/>.
            /// </summary>
            /// <param name="factory">The <see cref="WebApplicationFactory{TStartup}"/> to use when setting up the test server.</param>
            public MessageboxInstancesControllerTests(WebApplicationFactory<Startup> factory)
            {
                _factory = factory;
                _validToken = PrincipalUtil.GetToken(1);
                _validTokenUsr3 = PrincipalUtil.GetToken(3);
            }

            /// <summary>
            /// Scenario:
            ///   Request list of instances active without language settings.
            /// Expected result:
            ///   Requested language is not available, but a list of instances is returned regardless.
            /// Success criteria:
            ///   Default language is used for title, and the title contains the word "bokmål".
            /// </summary>
            [Fact]
            public async void GetMessageBoxInstanceList_RequestAllInstancesForAnOwnerWithtoutLanguage_ReturnsAllElementsUsingDefaultLanguage()
            {
                // Arrange
                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337,3));

                // Act
                HttpResponseMessage response = await client.GetAsync($"{BasePath}/sbl/instances/{1337}?state=active");

                // Assert
                string content = await response.Content.ReadAsStringAsync();
                List<MessageBoxInstance> messageBoxInstances = JsonConvert.DeserializeObject(content, typeof(List<MessageBoxInstance>)) as List<MessageBoxInstance>;

                int expectedCount = 8;
                string expectedTitle = "Endring av navn (RF-1453)";
                int actualCount = messageBoxInstances.Count;
                string actualTitle = messageBoxInstances.First().Title;
                Assert.Equal(expectedCount, actualCount);
                Assert.Equal(expectedTitle, actualTitle);
            }

            /// <summary>
            /// Scenario:
            ///   Request list of instances with language setting english.
            /// Expected:
            ///   Requested language is available and a list of instances is returned.
            /// Success:
            ///   English title is returned in the instances and the title contains the word "english".
            /// </summary>
            [Fact]
            public async void GetMessageBoxInstanceList_RequestAllInstancesForAnOwnerInEnglish_ReturnsAllElementsWithEnglishTitles()
            {
                // Arrange
                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, 3));

                // Act
                HttpResponseMessage response = await client.GetAsync($"{BasePath}/sbl/instances/1337?state=active&language=en");
                string content = await response.Content.ReadAsStringAsync();
                List<MessageBoxInstance> messageBoxInstances = JsonConvert.DeserializeObject<List<MessageBoxInstance>>(content);

                int actualCount = messageBoxInstances.Count;
                string actualTitle = messageBoxInstances.First().Title;

                // Assert
                int expectedCount = 8;
                string expectedTitle = "Name change";
                Assert.Equal(expectedCount, actualCount);
                Assert.Equal(expectedTitle, actualTitle);
            }

            /// <summary>
            /// Scenario:
            ///   Request list of archived instances.
            /// Expected:
            ///   A list of instances is returned regardless.
            /// Success:
            ///   A single instance is returned.
            /// </summary>
            [Fact]
            public async void GetMessageBoxInstanceList_RequestArchivedInstancesForGivenOwner_ReturnsCorrectListOfInstances()
            {
                // Arrange
                MessageBoxTestData testData = new MessageBoxTestData();

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, 3));

                // Act
                HttpResponseMessage responseMessage = await client.GetAsync($"{BasePath}/sbl/instances/{1337}?state=archived");

                // Assert
                Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);

                string responseContent = await responseMessage.Content.ReadAsStringAsync();
                List<MessageBoxInstance> messageBoxInstances = JsonConvert.DeserializeObject<List<MessageBoxInstance>>(responseContent);

                int actualCount = messageBoxInstances.Count;
                int expectedCount = 3;
                Assert.Equal(expectedCount, actualCount);
            }

            /// <summary>
            /// Scenario:
            ///   Restore a soft deleted instance in storage.
            /// Expected result:
            ///   The instance is restored.
            /// Success criteria:
            ///   True is returned for the http request. 
            /// </summary>
            [Fact]
            public async void Undelete_RestoreSoftDeletedInstance_ReturnsTrue()
            {
                TestDataUtil.DeleteInstanceAndData(1337, new Guid("da1f620f-1764-4f98-9f03-74e5e20f10fe"));
                TestDataUtil.PrepareInstance(1337, new Guid("da1f620f-1764-4f98-9f03-74e5e20f10fe"));
                // Arrange
                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, 3));

                // Act
                HttpResponseMessage response = await client.PutAsync($"{BasePath}/sbl/instances/{1337}/da1f620f-1764-4f98-9f03-74e5e20f10fe/undelete", null);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                
                string content = await response.Content.ReadAsStringAsync();
                bool actualResult = JsonConvert.DeserializeObject<bool>(content);

                Assert.True(actualResult);
                TestDataUtil.DeleteInstanceAndData(1337, new Guid("da1f620f-1764-4f98-9f03-74e5e20f10fe"));
            }

            /// <summary>
            /// Scenario:
            ///   Restore a soft deleted instance in storage but user has too low authentication level. 
            /// Expected result:
            ///   The instance is not restored and returns status forbidden. 
            /// </summary>
            [Fact]
            public async void Undelete_UserHasTooLowAuthLv_ReturnsStatusForbidden()
            {
                // Arrange
                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetToken(1337, 1);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.PutAsync($"{BasePath}/sbl/instances/{1337}/cd41b024-f6b8-4ca7-9080-adc9eca5f0d1/undelete", null);

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                string content = await response.Content.ReadAsStringAsync();
                Assert.True(string.IsNullOrEmpty(content));
            }

            /// <summary>
            /// Scenario:
            ///   Restore a soft deleted instance in storage but response is deny.  
            /// Expected result:
            ///   The instance is not restored and returns status forbidden. 
            /// </summary>
            [Fact]
            public async void Undelete_ResponseIsDeny_ReturnsStatusForbidden()
            {
                // Arrange
                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetToken(-1);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.PutAsync($"{BasePath}/sbl/instances/{1337}/cd41b024-f6b8-4ca7-9080-adc9eca5f0d1/undelete", null);

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                string content = await response.Content.ReadAsStringAsync();
                Assert.True(string.IsNullOrEmpty(content));
            }

            /// <summary>
            /// Scenario:
            ///   Restore a hard deleted instance in storage
            /// Expected result:
            ///   It should not be possible to restore a hard deleted instance
            /// Success criteria:
            ///   Response status is BadRequest and the body contains correct reason.
            /// </summary>
            [Fact]
            public async void Undelete_AttemptToRestoreHardDeletedInstance_ReturnsBadRequest()
            {
                // Arrange
                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, 3));

                // Act
                HttpResponseMessage response = await client.PutAsync($"{BasePath}/sbl/instances/1337/f888c42b-8749-41d6-8048-8fc28c70beaa/undelete", null);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

                string content = await response.Content.ReadAsStringAsync();

                string expectedMsg = "Instance was permanently deleted and cannot be restored.";
                Assert.Equal(expectedMsg, content);
            }

            /// <summary>
            /// Scenario:
            ///   Non-existent instance to be restored
            /// Expected result:
            ///   Internal server error
            /// </summary>
            [Fact]
            public async void Undelete_RestoreNonExistentInstance_ReturnsNotFound()
            {
                // Arrange
                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, 3));

                // Act
                HttpResponseMessage response = await client.PutAsync($"{BasePath}/sbl/instances/1337/4be22ede-a16c-4a93-be7f-c529788d6a4c/undelete", null);

                // Assert
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Soft delete an active instance in storage.
            /// Expected result:
            ///   Instance is marked for soft delete.
            /// Success criteria:
            ///   True is returned for the http request.
            /// </summary>
            [Fact]
            public async void Delete_SoftDeleteActiveInstance_InstanceIsMarked_EventIsCreated_ReturnsTrue()
            {
                // Arrange
                TestDataUtil.DeleteInstanceAndData(1337, new Guid("08274f48-8313-4e2d-9788-bbdacef5a54e"));
                TestDataUtil.PrepareInstance(1337, new Guid("08274f48-8313-4e2d-9788-bbdacef5a54e"));

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337, 3));

                // Act
                HttpResponseMessage response = await client.DeleteAsync($"{BasePath}/sbl/instances/1337/08274f48-8313-4e2d-9788-bbdacef5a54e?hard=false");

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                string content = await response.Content.ReadAsStringAsync();
                bool actualResult = JsonConvert.DeserializeObject<bool>(content);
                Assert.True(actualResult);
                TestDataUtil.DeleteInstanceAndData(1337, new Guid("08274f48-8313-4e2d-9788-bbdacef5a54e"));
            }

            /// <summary>
            /// Scenario:
            ///   Soft delete an active instance in storage but user has too low authentication level.
            /// Expected result:
            ///   Returns status forbidden. 
            /// </summary>
            [Fact]
            public async void Delete_UserHasTooLowAuthLv_ReturnsStatusForbidden()
            {
                // Arrange
                InstanceEvent instanceEvent = null;

                Mock<IInstanceEventRepository> instanceEventRepository = new Mock<IInstanceEventRepository>();
                instanceEventRepository.Setup(s => s.InsertInstanceEvent(It.IsAny<InstanceEvent>())).Callback<InstanceEvent>(p => instanceEvent = p)
                    .ReturnsAsync((InstanceEvent r) => r);

                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetToken(1337, 1);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.DeleteAsync($"{BasePath}/sbl/instances/1337/6323a337-26e7-4d40-89e8-f5bb3d80be3a?hard=false");

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                string content = await response.Content.ReadAsStringAsync();
                Assert.True(string.IsNullOrEmpty(content));
            }

            /// <summary>
            /// Scenario:
            ///   Soft delete an active instance in storage but reponse is deny.
            /// Expected result:
            ///   Returns status forbidden. 
            /// </summary>
            [Fact]
            public async void Delete_ResponseIsDeny_ReturnsStatusForbidden()
            {
                // Arrange
                MessageBoxTestData testData = new MessageBoxTestData();
                Instance instance = testData.GetActiveInstance();

                Mock<IApplicationRepository> applicationRepository = new Mock<IApplicationRepository>();

                Instance storedInstance = null;

                Mock<IInstanceRepository> instanceRepository = new Mock<IInstanceRepository>();
                instanceRepository.Setup(s => s.GetOne(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(instance);
                instanceRepository.Setup(s => s.Update(It.IsAny<Instance>())).Callback<Instance>(p => storedInstance = p).ReturnsAsync((Instance i) => i);

                InstanceEvent instanceEvent = null;

                Mock<IInstanceEventRepository> instanceEventRepository = new Mock<IInstanceEventRepository>();
                instanceEventRepository.Setup(s => s.InsertInstanceEvent(It.IsAny<InstanceEvent>())).Callback<InstanceEvent>(p => instanceEvent = p)
                    .ReturnsAsync((InstanceEvent r) => r);

                HttpClient client = GetTestClient();
                string token = PrincipalUtil.GetToken(-1);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Act
                HttpResponseMessage response = await client.DeleteAsync($"{BasePath}/sbl/instances/{testData.GetInstanceOwnerPartyId()}/{instance.Id.Split("/")[1]}?hard=false");

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                string content = await response.Content.ReadAsStringAsync();
                Assert.True(string.IsNullOrEmpty(content));
            }

            /// <summary>
            /// Scenario:
            ///   Hard delete a soft deleted instance in storage.
            /// Expected result:
            ///   Instance is marked for hard delete.
            /// Success criteria:
            ///   True is returned for the http request.
            /// </summary>
            [Fact]
            public async void Delete_HardDeleteSoftDeleted()
            {
                // Arrange
                MessageBoxTestData testData = new MessageBoxTestData();
                Instance instance = testData.GetSoftDeletedInstance();

                Mock<IApplicationRepository> applicationRepository = new Mock<IApplicationRepository>();

                Instance storedInstance = null;

                Mock<IInstanceRepository> instanceRepository = new Mock<IInstanceRepository>();
                instanceRepository.Setup(s => s.GetOne(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(instance);
                instanceRepository.Setup(s => s.Update(It.IsAny<Instance>())).Callback<Instance>(p => storedInstance = p).ReturnsAsync((Instance i) => i);

                InstanceEvent instanceEvent = null;

                Mock<IInstanceEventRepository> instanceEventRepository = new Mock<IInstanceEventRepository>();
                instanceEventRepository.Setup(s => s.InsertInstanceEvent(It.IsAny<InstanceEvent>())).Callback<InstanceEvent>(p => instanceEvent = p)
                    .ReturnsAsync((InstanceEvent r) => r);

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _validToken);

                // Act
                HttpResponseMessage response = await client.DeleteAsync($"{BasePath}/sbl/instances/{testData.GetInstanceOwnerPartyId()}/{instance.Id.Split("/")[1]}?hard=true");

                // Assert
                HttpStatusCode actualStatusCode = response.StatusCode;
                string content = await response.Content.ReadAsStringAsync();
                bool actualResult = JsonConvert.DeserializeObject<bool>(content);

                HttpStatusCode expectedStatusCode = HttpStatusCode.OK;
                bool expectedResult = true;
                Assert.Equal(expectedResult, actualResult);
                Assert.Equal(expectedStatusCode, actualStatusCode);
                Assert.True(storedInstance.Status.HardDeleted.HasValue);
            }

            /// <summary>
            /// Scenario:
            ///  Delete an active instance, user has write priviliges
            /// Expected result:
            ///   Instance is marked for hard delete.
            /// Success criteria:
            ///   True is returned for the http request.
            /// </summary>
            [Fact]
            public async void Delete_ActiveHasRole_ReturnsOk()
            {
                // Arrange
                int instanceOwnerId = 1000;
                string instanceGuid = "1916cd18-3b8e-46f8-aeaf-4bc3397ddd08";
                string json = File.ReadAllText($"data/instances/{org}/{app}/{instanceOwnerId}/{instanceGuid}.json");
                Instance instance = JsonConvert.DeserializeObject<Instance>(json);
                HttpStatusCode expectedStatusCode = HttpStatusCode.OK;
                bool expectedResult = true;


                Mock<IApplicationRepository> applicationRepository = new Mock<IApplicationRepository>();

                Instance storedInstance = null;

                Mock<IInstanceRepository> instanceRepository = new Mock<IInstanceRepository>();
                instanceRepository.Setup(s => s.GetOne(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(instance);
                instanceRepository.Setup(s => s.Update(It.IsAny<Instance>())).Callback<Instance>(p => storedInstance = p).ReturnsAsync((Instance i) => i);

                InstanceEvent instanceEvent = null;

                Mock<IInstanceEventRepository> instanceEventRepository = new Mock<IInstanceEventRepository>();
                instanceEventRepository.Setup(s => s.InsertInstanceEvent(It.IsAny<InstanceEvent>())).Callback<InstanceEvent>(p => instanceEvent = p)
                    .ReturnsAsync((InstanceEvent r) => r);

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _validTokenUsr3);

                // Act
                HttpResponseMessage response = await client.DeleteAsync($"{BasePath}/sbl/instances/{instanceOwnerId}/{instanceGuid}?hard=false");
                HttpStatusCode actualStatusCode = response.StatusCode;
                string content = await response.Content.ReadAsStringAsync();
                bool actualResult = JsonConvert.DeserializeObject<bool>(content);

                // Assert
                Assert.Equal(expectedResult, actualResult);
                Assert.Equal(expectedStatusCode, actualStatusCode);
                Assert.False(storedInstance.Status.HardDeleted.HasValue);
                Assert.True(storedInstance.Status.SoftDeleted.HasValue);
            }

            /// <summary>
            /// Scenario:
            ///  Delete an active instance, user does not have priviliges
            /// Expected result:
            ///  No changes are made to the instance
            /// Success criteria:
            ///   Forbidden is returned for the http request.
            /// </summary>
            [Fact]
            public async void Delete_ActiveMissingRole_ReturnsForbidden()
            {
                // Arrange
                int instanceOwnerId = 1600;
                string instanceGuid = "1916cd18-3b8e-46f8-aeaf-4bc3397ddd08";
                string json = File.ReadAllText($"data/instances/{org}/{app}/{instanceOwnerId}/{instanceGuid}.json");
                Instance instance = JsonConvert.DeserializeObject<Instance>(json);
                HttpStatusCode expectedStatusCode = HttpStatusCode.Forbidden;

                Mock<IApplicationRepository> applicationRepository = new Mock<IApplicationRepository>();

                Instance storedInstance = null;

                Mock<IInstanceRepository> instanceRepository = new Mock<IInstanceRepository>();
                instanceRepository.Setup(s => s.GetOne(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(instance);
                instanceRepository.Setup(s => s.Update(It.IsAny<Instance>())).Callback<Instance>(p => storedInstance = p).ReturnsAsync((Instance i) => i);

                InstanceEvent instanceEvent = null;

                Mock<IInstanceEventRepository> instanceEventRepository = new Mock<IInstanceEventRepository>();
                instanceEventRepository.Setup(s => s.InsertInstanceEvent(It.IsAny<InstanceEvent>())).Callback<InstanceEvent>(p => instanceEvent = p)
                    .ReturnsAsync((InstanceEvent r) => r);

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _validTokenUsr3);

                // Act
                HttpResponseMessage response = await client.DeleteAsync($"{BasePath}/sbl/instances/{instanceOwnerId}/{instanceGuid}?hard=false");
                HttpStatusCode actualStatusCode = response.StatusCode;

                // Assert
                Assert.Equal(expectedStatusCode, actualStatusCode);
            }

            /// <summary>
            /// Scenario:
            ///  Delete an archived instance, user has delete priviliges
            /// Expected result:
            ///   Instance is marked for hard delete.
            /// Success criteria:
            ///   True is returned for the http request.
            /// </summary>
            [Fact]
            public async void Delete_ArchivedHasRole_ReturnsOk()
            {
                // Arrange
                int instanceOwnerId = 1000;
                string instanceGuid = "1916cd18-3b8e-46f8-aeaf-4bc3397ddd12";
                string json = File.ReadAllText($"data/instances/{org}/{app}/{instanceOwnerId}/{instanceGuid}.json");
                Instance instance = JsonConvert.DeserializeObject<Instance>(json);
                HttpStatusCode expectedStatusCode = HttpStatusCode.OK;
                bool expectedResult = true;


                Mock<IApplicationRepository> applicationRepository = new Mock<IApplicationRepository>();

                Instance storedInstance = null;

                InstanceEvent instanceEvent = null;

               

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _validTokenUsr3);

                // Act
                HttpResponseMessage response = await client.DeleteAsync($"{BasePath}/sbl/instances/{instanceOwnerId}/{instanceGuid}?hard=false");
                HttpStatusCode actualStatusCode = response.StatusCode;
                string content = await response.Content.ReadAsStringAsync();
                bool actualResult = JsonConvert.DeserializeObject<bool>(content);

                // Assert
                Assert.Equal(expectedResult, actualResult);
                Assert.Equal(expectedStatusCode, actualStatusCode);
                Assert.False(storedInstance.Status.HardDeleted.HasValue);
                Assert.True(storedInstance.Status.SoftDeleted.HasValue);
            }

            /// <summary>
            /// Scenario:
            ///  Delete an archived instance, user does not have priviliges
            /// Expected result:
            ///  No changes are made to the instance
            /// Success criteria:
            ///   Forbidden is returned for the http request.
            /// </summary>
            [Fact]
            public async void Delete_ArchivedMissingRole_ReturnsForbidden()
            {
                // Arrange
                int instanceOwnerId = 1600;
                string instanceGuid = "1916cd18-3b8e-46f8-aeaf-4bc3397ddd12";
                string json = File.ReadAllText($"data/instances/{org}/{app}/{instanceOwnerId}/{instanceGuid}.json");
                Instance instance = JsonConvert.DeserializeObject<Instance>(json);
                HttpStatusCode expectedStatusCode = HttpStatusCode.Forbidden;

                Mock<IApplicationRepository> applicationRepository = new Mock<IApplicationRepository>();

                Instance storedInstance = null;

                Mock<IInstanceRepository> instanceRepository = new Mock<IInstanceRepository>();
                instanceRepository.Setup(s => s.GetOne(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(instance);
                instanceRepository.Setup(s => s.Update(It.IsAny<Instance>())).Callback<Instance>(p => storedInstance = p).ReturnsAsync((Instance i) => i);

                InstanceEvent instanceEvent = null;

                Mock<IInstanceEventRepository> instanceEventRepository = new Mock<IInstanceEventRepository>();
                instanceEventRepository.Setup(s => s.InsertInstanceEvent(It.IsAny<InstanceEvent>())).Callback<InstanceEvent>(p => instanceEvent = p)
                    .ReturnsAsync((InstanceEvent r) => r);

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _validTokenUsr3);

                // Act
                HttpResponseMessage response = await client.DeleteAsync($"{BasePath}/sbl/instances/{instanceOwnerId}/{instanceGuid}?hard=false");
                HttpStatusCode actualStatusCode = response.StatusCode;

                // Assert
                Assert.Equal(expectedStatusCode, actualStatusCode);
            }

            private HttpClient GetTestClient()
            {
                // No setup required for these services. They are not in use by the MessageBoxInstancesController
                Mock<IDataRepository> dataRepository = new Mock<IDataRepository>();
                Mock<ISasTokenProvider> sasTokenProvider = new Mock<ISasTokenProvider>();
                Mock<IKeyVaultClientWrapper> keyVaultWrapper = new Mock<IKeyVaultClientWrapper>();
                Program.ConfigureSetupLogging();
                HttpClient client = _factory.WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        services.AddSingleton(dataRepository.Object);
                        services.AddSingleton<IApplicationRepository, ApplicationRepositoryMock>();
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

            /// <summary>
            /// Create a DocumentClientException using reflection because all constructors are internal.
            /// </summary>
            /// <param name="message">Exception message</param>
            /// <param name="httpStatusCode">The HttpStatus code.</param>
            /// <returns></returns>
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
