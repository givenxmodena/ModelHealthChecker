using System.Net;
using System.Net.Http;
using System.Reflection;
using Modena.RevitAddin.Services;
using Modena.RevitAddin.ViewModels;
using Modena.RevitAddin.Views;
using Modena.RevitAddin.RevitApi;
using Modena.Shared.DTOs;
using Modena.Shared.Enums;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Modena.RevitAddin.Tests.Properties;

public sealed class CloudIntegrationTests
{
    private const string Project = "11111111-1111-1111-1111-111111111111";
    private const string Model = "22222222-2222-2222-2222-222222222222";
    private static ModelIdentity Identity() => new()
        { IsCloudModel = true, ProjectGuid = Project, ModelGuid = Model, ModelSource = ModelSource.ACCCloud };

    private static object Version(string model = Model, bool derivative = true) => new
    {
        id = "urn:version:1", type = "versions",
        attributes = new { extension = new { data = new { modelGuid = model, projectGuid = Project } } },
        relationships = new
        {
            item = new { data = new { id = "urn:item:one" } },
            derivatives = new { data = derivative ? new { id = "actual-derivative-urn" } : null }
        }
    };

    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(JsonConvert.SerializeObject(value)) };

    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Paths { get; } = new();
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Reply { get; set; }
        public Func<string, HttpResponseMessage?>? Override { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Paths.Add(request.RequestUri!.AbsoluteUri);
            Assert.Equal("developer.api.autodesk.com", request.RequestUri.Host);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
            if (Reply is not null) return Reply(request, ct);
            var path = request.RequestUri.AbsolutePath;
            var result = Override?.Invoke(request.RequestUri.AbsoluteUri);
            if (result is not null) return Task.FromResult(result);
            if (path == "/project/v1/hubs") result = Json(new { data = new[] { new { id = "b.hub" } } });
            else if (path.Contains("/projects/") && path.StartsWith("/project/"))
                result = Json(new { data = new { relationships = new { rootFolder = new { data = new { id = "urn:root" } } } } });
            else if (path.EndsWith("/search")) result = Json(new { data = new[] { Version() } });
            else if (path.EndsWith("/tip")) result = Json(new { data = Version() });
            else if (path.EndsWith("/metadata")) result = Json(new { data = new { metadata = new[] { new { guid = "view", name = "3D published", role = "3d" } } } });
            else if (path.EndsWith("/properties")) result = Json(new { data = new { collection = new[] { new { objectid = 1 }, new { objectid = 2 } } } });
            else throw new InvalidOperationException("Unexpected test request.");
            return Task.FromResult(result);
        }
    }

    private static ApsCloudModelService Service(Handler handler, TimeSpan? timeout = null) =>
        new(new HttpClient(handler), (_, _) => Task.FromResult("test-token"), timeout ?? TimeSpan.FromSeconds(5));

    [Fact]
    public async Task Cloud_ResolvesExactIdentityAndReadsActualDerivativeProperties()
    {
        var handler = new Handler();
        var statuses = new List<string>();
        var result = await Service(handler).LoadAsync(Identity(), true, statuses.Add, default);
        Assert.Equal("urn:item:one", result.ItemId);
        Assert.Equal("urn:version:1", result.VersionUrn);
        Assert.Equal(2, result.ObjectCount);
        Assert.Equal("3D published", result.ViewName);
        Assert.Equal(new[] { "Authenticating…", "Loading published cloud data…" }, statuses);
        Assert.Contains(handler.Paths, p => p.Contains("actual-derivative-urn/metadata/view/properties"));
    }

    [Fact]
    public async Task Cloud_FollowsSearchPagesAndRejectsFilenameOnlyMatches()
    {
        var handler = new Handler();
        handler.Override = url => url.Contains("/search")
            ? url.Contains("page=2") ? Json(new { data = new[] { Version() } })
                : Json(new { data = new[] { Version(Guid.NewGuid().ToString()) }, links = new { next = new { href = url + "&page=2" } } })
            : null;
        var info = await Service(handler).LoadAsync(Identity(), true, _ => { }, default);
        Assert.Equal(2, info.ObjectCount);
        Assert.Equal(2, handler.Paths.Count(p => p.Contains("/search")));
    }

    [Theory]
    [InlineData("https://attacker.invalid/data/v1/page")]
    [InlineData("http://developer.api.autodesk.com/data/v1/page")]
    [InlineData("https://developer.api.autodesk.com:444/data/v1/page")]
    [InlineData("https://developer.api.autodesk.com/authentication/v2/token")]
    public async Task Cloud_DoesNotForwardTokensToUntrustedPagination(string next)
    {
        var handler = new Handler { Override = url => url.Contains("/search")
            ? Json(new { data = Array.Empty<object>(), links = new { next = new { href = next } } }) : null };
        var ex = await Assert.ThrowsAsync<CloudModelException>(() => Service(handler).LoadAsync(Identity(), true, _ => { }, default));
        Assert.Contains("untrusted", ex.Message);
        Assert.DoesNotContain(handler.Paths, p => p == next);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task Cloud_HttpFailuresDoNotExposeResponseBodies(int status)
    {
        var handler = new Handler { Override = _ => Json(new { error = "sensitive-server-response test-token" }, (HttpStatusCode)status) };
        var ex = await Assert.ThrowsAsync<CloudModelException>(() => Service(handler).LoadAsync(Identity(), true, _ => { }, default));
        Assert.DoesNotContain("sensitive-server", ex.Message);
        Assert.DoesNotContain("test-token", ex.Message);
    }

    [Fact]
    public async Task Cloud_MissingDerivativeRetainsResolvedIdentityForFallback()
    {
        var handler = new Handler { Override = url => url.Contains("/search") ? Json(new { data = new[] { Version(derivative: false) } }) : null };
        var ex = await Assert.ThrowsAsync<CloudModelException>(() => Service(handler).LoadAsync(Identity(), true, _ => { }, default));
        Assert.Equal("urn:item:one", ex.Identity!.ItemId);
        Assert.Equal("urn:version:1", ex.Identity.VersionUrn);
    }

    [Fact]
    public async Task Cloud_ProcessingDerivativeDoesNotClaimSuccess()
    {
        var handler = new Handler { Override = url => url.Contains("/properties") ? Json(new { }, HttpStatusCode.Accepted) : null };
        var ex = await Assert.ThrowsAsync<CloudModelException>(() => Service(handler).LoadAsync(Identity(), true, _ => { }, default));
        Assert.Contains("still processing", ex.Message);
        Assert.NotNull(ex.Identity);
    }

    [Fact]
    public async Task Cloud_MalformedPropertiesRetainIdentityWithoutRawPayloads()
    {
        var handler = new Handler { Override = url => url.Contains("/properties")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not-json sensitive-server-value") } : null };
        var ex = await Assert.ThrowsAsync<CloudModelException>(() => Service(handler).LoadAsync(Identity(), true, _ => { }, default));
        Assert.Equal("urn:item:one", ex.Identity!.ItemId);
        Assert.DoesNotContain("sensitive-server-value", ex.Message);
    }

    [Fact]
    public async Task Cloud_UsesKnownItemTipButStillValidatesItsModelGuid()
    {
        var identity = Identity();
        identity.ItemId = "urn:item:one";
        var handler = new Handler();
        await Service(handler).LoadAsync(identity, false, _ => { }, default);
        Assert.DoesNotContain(handler.Paths, p => p.Contains("/hubs"));
        handler.Override = url => url.Contains("/tip") ? Json(new { data = Version(Guid.NewGuid().ToString()) }) : null;
        await Assert.ThrowsAsync<CloudModelException>(() => Service(handler).LoadAsync(identity, false, _ => { }, default));
    }

    [Fact]
    public async Task Cloud_AmbiguousModelsAndMissingGuidsFailBeforeGuessing()
    {
        var handler = new Handler { Override = url => url.Contains("/search") ? Json(new { data = new[] { Version(), Version() } }) : null };
        await Assert.ThrowsAsync<CloudModelException>(() => Service(handler).LoadAsync(Identity(), true, _ => { }, default));
        var invalid = Identity(); invalid.ModelGuid = null;
        var count = handler.Paths.Count;
        await Assert.ThrowsAsync<CloudModelException>(() => Service(handler).LoadAsync(invalid, true, _ => { }, default));
        Assert.Equal(count, handler.Paths.Count);
    }

    [Fact]
    public async Task Cloud_TimeoutAndCancellationStopNetworkWork()
    {
        var handler = new Handler { Reply = async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Json(new { }); } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(handler, TimeSpan.FromMilliseconds(25)).LoadAsync(Identity(), true, _ => { }, default));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(handler).LoadAsync(Identity(), true, _ => { }, cancelled.Token));
    }

    [Theory]
    [InlineData("http://localhost:8080/?code=ok&state=expected", true)]
    [InlineData("http://localhost:8080/?code=ok&state=wrong", false)]
    [InlineData("http://localhost:8080/?code=ok", false)]
    [InlineData("http://localhost:8080.evil.invalid/?code=ok&state=expected", false)]
    [InlineData("http://localhost:8081/?code=ok&state=expected", false)]
    [InlineData("http://localhost:8080/other?code=ok&state=expected", false)]
    [InlineData("http://localhost:8080/?code=ok&state=expected&state=wrong", false)]
    [InlineData("http://localhost:8080/?error=denied&code=ok&state=expected", false)]
    public void Cloud_OAuthCallbackRequiresExactOriginPathAndState(string callback, bool expected)
    {
        const string auth = "https://developer.api.autodesk.com/authentication/v2/authorize?state=expected&redirect_uri=http%3A%2F%2Flocalhost%3A8080%2F";
        Assert.Equal(expected, AuthWindow.TryGetAuthorizationCode(auth, callback, out var code));
        Assert.Equal(expected ? "ok" : null, code);
    }

    [Fact]
    public void Cloud_PublicClientUsesPkceWithoutEmbeddingASecret()
    {
        var auth = new AuthService("public-client", "");
        var url = (string)typeof(AuthService).GetMethod("BuildAuthUrl", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(auth, null)!;
        Assert.Contains("code_challenge=", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.DoesNotContain("client_secret", url);
    }

    [Fact]
    public async Task Cloud_SilentAuthenticationDoesNotOpenASignInWindow()
    {
        var auth = new AuthService("not-the-current-client-" + Guid.NewGuid().ToString("N"), "");
        var ex = await Assert.ThrowsAsync<CloudModelException>(() => auth.GetCloudAccessTokenAsync(false, default));
        Assert.Contains("Use Refresh", ex.Message);
    }

    [Theory]
    [InlineData("RSN://server/project/model.rvt", ModelSource.RevitServer)]
    [InlineData(@"\\server\shared\central.rvt", ModelSource.Local)]
    [InlineData(@"C:\models\central.rvt", ModelSource.Local)]
    public void Cloud_SourceLabelDoesNotConfuseFileWorksharingWithRevitServer(string central, ModelSource expected)
    {
        var context = new Mock<IRevitDocumentContext>();
        context.SetupGet(c => c.HasActiveDocument).Returns(true);
        context.SetupGet(c => c.IsDocumentSaved).Returns(true);
        context.SetupGet(c => c.ModelPath).Returns(@"C:\models\local.rvt");
        context.SetupGet(c => c.CentralModelPath).Returns(central);
        Assert.Equal(expected, new RevitContextService(context.Object).BuildModelIdentity()!.ModelSource);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task Cloud_ViewModelPreservesLocalScoreOnSuccessOrFailure(bool success) => HealthHistoryTests.OnDispatcher(async () =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "PulseCloudTests", Guid.NewGuid().ToString("N"));
        var modelKey = "cloud-test-" + Guid.NewGuid().ToString("N");
        var identity = Identity();
        identity.DocumentTitle = "Cloud test";
        var context = new Mock<IRevitDocumentContext>();
        context.SetupGet(c => c.DocumentTitle).Returns("Cloud test");
        var extractor = new Mock<IModelHealthExtractor>();
        extractor.Setup(e => e.ExtractFastAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardResponse { ModelKey = modelKey, LastUpdatedUtc = DateTime.UtcNow, Summary = new SummaryDto { PassRate = 40, TotalChecks = 15, PassedChecks = 6 } });
        extractor.Setup(e => e.ExtractFamilySizesAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<FamilyDto>());
        var cloud = new Mock<ICloudModelService>();
        if (success) cloud.Setup(c => c.LoadAsync(identity, true, It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CloudModelInfo { ItemId = "item", VersionUrn = "version", ViewName = "3D", ObjectCount = 999 });
        else cloud.Setup(c => c.LoadAsync(identity, true, It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("sensitive-error test-token"));
        var vm = new ModelHealthViewModel(identity, extractor.Object, context.Object,
            new PluginConfig { AutoRefreshEnabled = false }, historyService: new HealthHistoryService(directory), cloudService: cloud.Object);
        try
        {
            await vm.RefreshCommand.ExecuteAsync();
            Assert.True(vm.HasData);
            Assert.Equal(40, vm.Summary.PassRate);
            Assert.Equal("ACC Cloud", vm.ModelSourceText);
            var window = new ModelHealthWindow(vm);
            try
            {
                var root = (System.Windows.FrameworkElement)window.Content;
                root.Measure(new System.Windows.Size(780, 740));
                root.Arrange(new System.Windows.Rect(0, 0, 780, 740));
                root.UpdateLayout();
                var labels = TextLabels(root).ToList();
                Assert.Contains("ACC Cloud", labels);
                Assert.Contains(vm.CloudStatus, labels);
                if (success) Assert.Contains(vm.CloudIdentityText, labels);
            }
            finally { window.Close(); }
            if (success)
            {
                Assert.Contains("999 objects", vm.CloudStatus);
                Assert.Contains("version", vm.CloudIdentityText);
                Assert.Equal("item", identity.ItemId);
            }
            else
            {
                Assert.StartsWith("Cloud extraction unavailable", vm.CloudStatus);
                Assert.DoesNotContain("sensitive-error", vm.CloudStatus);
                Assert.DoesNotContain("test-token", vm.CloudStatus);
            }
        }
        finally { vm.Cleanup(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    });

    [Fact]
    public Task Cloud_LocalModelsNeverCallCloudService() => HealthHistoryTests.OnDispatcher(async () =>
    {
        var cloud = new Mock<ICloudModelService>(MockBehavior.Strict);
        var extractor = new Mock<IModelHealthExtractor>();
        extractor.Setup(e => e.ExtractFastAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardResponse());
        extractor.Setup(e => e.ExtractFamilySizesAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FamilyDto>());
        var vm = new ModelHealthViewModel(new ModelIdentity(), extractor.Object,
            Mock.Of<IRevitDocumentContext>(), new PluginConfig { AutoRefreshEnabled = false }, cloudService: cloud.Object);
        try
        {
            await vm.RefreshCommand.ExecuteAsync();
            Assert.Equal("Local", vm.ModelSourceText);
            Assert.Empty(vm.CloudStatus);
            cloud.VerifyNoOtherCalls();
        }
        finally { vm.Cleanup(); }
    });

    [Fact]
    public Task Cloud_ClosingDashboardDiscardsLateCloudResults() => HealthHistoryTests.OnDispatcher(async () =>
    {
        var identity = Identity();
        var response = new TaskCompletionSource<CloudModelInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cloud = new Mock<ICloudModelService>();
        cloud.Setup(c => c.LoadAsync(identity, true, It.IsAny<Action<string>>(), It.IsAny<CancellationToken>())).Returns(response.Task);
        var extractor = new Mock<IModelHealthExtractor>();
        extractor.Setup(e => e.ExtractFastAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new DashboardResponse());
        extractor.Setup(e => e.ExtractFamilySizesAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<FamilyDto>());
        var vm = new ModelHealthViewModel(identity, extractor.Object, Mock.Of<IRevitDocumentContext>(),
            new PluginConfig { AutoRefreshEnabled = false }, cloudService: cloud.Object);
        var refresh = vm.RefreshCommand.ExecuteAsync();
        await Task.Yield();
        vm.Cleanup();
        response.SetResult(new CloudModelInfo { ItemId = "late-item", VersionUrn = "late-version", ObjectCount = 1 });
        await refresh;
        Assert.Empty(vm.CloudIdentityText);
        Assert.Null(identity.ItemId);
        Assert.False(vm.IsLoading);
    });

    private static IEnumerable<string> TextLabels(System.Windows.DependencyObject root)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is System.Windows.Controls.TextBlock text) yield return text.Text;
            foreach (var label in TextLabels(child)) yield return label;
        }
    }
}
