using FoodSupply.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace FoodSupply.Tests;

public sealed class ProfilePhotoTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "FoodSupply-photo-tests-" + Guid.NewGuid());
    private ProfilePhotoStore Store => new(new TestEnvironment { ContentRootPath = root });
    private static FormFile Upload(byte[] bytes) => new(new MemoryStream(bytes), 0, bytes.Length, "photo", "photo.png");

    [Fact]
    public async Task PhotoCanBeReplacedAndRemovedWithoutAffectingAnotherUser()
    {
        var store = Store;
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
        await store.SaveAsync(1, Upload(png));
        await store.SaveAsync(2, Upload(png));
        Assert.Equal(png, await store.ReadAsync(1));
        Assert.NotNull(store.Version(1));
        await store.SaveAsync(1, Upload(png));
        store.Remove(1);
        Assert.Null(await store.ReadAsync(1));
        Assert.Null(store.Version(1));
        Assert.Equal(png, await store.ReadAsync(2));
    }

    [Fact]
    public async Task RejectsEmptyOversizedAndNonImageUploads()
    {
        var store = Store;
        foreach (var bytes in new[] { Array.Empty<byte>(), new byte[ProfilePhotoStore.MaxBytes + 1], System.Text.Encoding.UTF8.GetBytes("<svg onload='alert(1)'></svg>") })
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(1, Upload(bytes)));
        Assert.Null(await store.ReadAsync(1));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    internal sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "FoodSupply.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public string WebRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
