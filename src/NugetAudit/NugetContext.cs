using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace DependencyModel;

internal sealed class NugetContext : IDisposable
{
    private readonly TaskCompletionSource<NugetResources> _nugetResources = new ();

    public SourceRepository SourceRepository { get; }

    public SourceCacheContext CacheContext { get; } = new();

    public NugetContext()
    {
        var settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory());
        var packageSourceProvider = new PackageSourceProvider(settings);
        var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, Repository.Provider.GetCoreV3());
        var sourceRepositories = sourceRepositoryProvider.GetRepositories();
    
        SourceRepository = sourceRepositories.First();

        GetResourcesInternal(SourceRepository);
    }

    public Task<NugetResources> Resources => _nugetResources.Task;

    public async Task<Package?> GetPackageInfo(PackageIdentity packageIdentity)
    {
        var resources = await Resources.ConfigureAwait(false);

        var versions = await resources.FindPackageById.GetAllVersionsAsync(packageIdentity.Id, CacheContext, NullLogger.Instance, CancellationToken.None).ConfigureAwait(false);
        if (versions?.Any() != true)
            return null;

        var metadata = await resources.PackageMetadata.GetMetadataAsync(packageIdentity, CacheContext, NullLogger.Instance, CancellationToken.None).ConfigureAwait(false);

        if (metadata is not { Vulnerabilities: { } vulnerabilityMetadata })
            return null;

        var vulnerabilities = vulnerabilityMetadata.Select(v => new Vulnerability(v.Severity, v.AdvisoryUrl)).ToArray();

        if (vulnerabilities.Length == 0)
            return null;

        var package = new Package(
            packageIdentity.Id,
            packageIdentity.Version.Version,
            vulnerabilities);

        return package;
    }

    private async void GetResourcesInternal(SourceRepository sourceRepository)
    {
        try
        {
            var packageResource = await sourceRepository
                .GetResourceAsync<FindPackageByIdResource>(CancellationToken.None).ConfigureAwait(false);
            var packageMetadataResource = await sourceRepository
                .GetResourceAsync<PackageMetadataResource>(CancellationToken.None).ConfigureAwait(false);

            _nugetResources.SetResult(new NugetResources(packageResource, packageMetadataResource));
        }
        catch (Exception ex)
        {
            _nugetResources.SetException(ex);
        }
    }

    public void Dispose()
    {
        CacheContext.Dispose();
    }
}

internal sealed record NugetResources(FindPackageByIdResource FindPackageById, PackageMetadataResource PackageMetadata);