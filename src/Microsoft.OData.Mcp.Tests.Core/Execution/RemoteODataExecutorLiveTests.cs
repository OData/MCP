using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Execution
{

    /// <summary>
    /// Live remote executor tests against public OData services.
    /// </summary>
    [TestClass]
    public class RemoteODataExecutorLiveTests
    {

        #region Public Methods

        /// <summary>
        /// Northwind Products top 1 returns JSON.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_NorthwindProductsTop1_ReturnsJson()
        {
            var services = new ServiceCollection();
            services.AddHttpClient("OData", client =>
            {
                client.BaseAddress = new Uri(LiveOData.Northwind.TrimEnd('/') + "/");
            });
            using var provider = services.BuildServiceProvider();
            var executor = new RemoteODataExecutor(provider.GetRequiredService<IHttpClientFactory>());
            var result = await executor.ExecuteAsync(
                new ODataExecuteRequest
                {
                    Method = HttpMethod.Get,
                    QueryOptions = { ["top"] = "1" },
                    RelativePath = "Products"
                },
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Body.Should().Contain("Product");
        }

        /// <summary>
        /// Query keys that already have $ are not doubled.
        /// </summary>
        [TestMethod]
        public void BuildRelativeUri_AddsSingleDollarPrefix()
        {
            RemoteODataExecutor.BuildRelativeUri(new ODataExecuteRequest
            {
                QueryOptions =
                {
                    ["$filter"] = "true",
                    ["top"] = "1"
                },
                RelativePath = "Products"
            }).Should().Contain("$filter=true").And.Contain("$top=1").And.NotContain("$$");
        }

        /// <summary>
        /// A missing entity returns a failed result.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_NorthwindMissing_IsNotSuccess()
        {
            var services = new ServiceCollection();
            services.AddHttpClient("OData", client =>
            {
                client.BaseAddress = new Uri(LiveOData.Northwind.TrimEnd('/') + "/");
            });
            using var provider = services.BuildServiceProvider();
            var executor = new RemoteODataExecutor(provider.GetRequiredService<IHttpClientFactory>());
            var result = await executor.ExecuteAsync(
                new ODataExecuteRequest
                {
                    JsonBody = "{}",
                    Method = HttpMethod.Get,
                    RelativePath = "DoesNotExist"
                },
                CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
        }

        /// <summary>
        /// Requests without query options return the path.
        /// </summary>
        [TestMethod]
        public void BuildRelativeUri_NoQuery_ReturnsPath()
        {
            RemoteODataExecutor.BuildRelativeUri(new ODataExecuteRequest
            {
                RelativePath = "Products"
            }).Should().Be("Products");
        }

        /// <summary>
        /// Null requests are rejected.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_NullRequest_Throws()
        {
            var act = async () => await CreateExecutor().ExecuteAsync(null!, CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        /// <summary>
        /// Empty relative paths are rejected.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_EmptyPath_Throws()
        {
            var act = async () => await CreateExecutor().ExecuteAsync(new ODataExecuteRequest
            {
                Method = HttpMethod.Get,
                RelativePath = " "
            }, CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>();
        }

        /// <summary>
        /// Null factories are rejected.
        /// </summary>
        [TestMethod]
        public void Constructor_NullFactory_Throws()
        {
            var act = () => new RemoteODataExecutor(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// POST with a JSON body is sent to a live service and returns a non-success for Northwind.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_NorthwindPost_IsNotSuccess()
        {
            var result = await CreateExecutor().ExecuteAsync(
                new ODataExecuteRequest
                {
                    JsonBody = """{"ProductName":"Test"}""",
                    Method = HttpMethod.Post,
                    RelativePath = "Products"
                },
                CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a remote executor against live Northwind.
        /// </summary>
        /// <returns>
        /// The executor.
        /// </returns>
        internal static RemoteODataExecutor CreateExecutor()
        {
            var services = new ServiceCollection();
            services.AddHttpClient("OData", client =>
            {
                client.BaseAddress = new Uri(LiveOData.Northwind.TrimEnd('/') + "/");
            });
            var provider = services.BuildServiceProvider();

            return new RemoteODataExecutor(provider.GetRequiredService<IHttpClientFactory>());
        }

        #endregion

    }

}
