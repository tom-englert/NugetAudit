// ReSharper disable AccessToDisposedClosure

using NuGet.Packaging.Core;
using NuGet.Versioning;

using System.Text.Json;

using Cocona;

using TomsToolbox.Essentials;

namespace NugetAudit;

internal static class App
{
    private const string NugetOrg = "https://api.nuget.org/v3/index.json";

    public static async Task<int> Run(
        [Argument(Description = "Path to a dependency file or directory with files; default is the current directory")]
        string? fileOrDirectory,

        [Option(Description = "The package source used to get the vulnerability info")]
        string packageSource = NugetOrg)
    {
        var files = GetFiles(fileOrDirectory);

        if (files.Length == 0)
        {
            Console.WriteLine($"No dependency files found in '{fileOrDirectory}'");
            return 1;
        }

        using var context = new NugetContext(packageSource);

        // Check the source repo: Verify that some known vulnerable package reports vulnerabilities

        var probe = await context.GetPackageInfo(new PackageIdentity("System.Security.Cryptography.Xml", NuGetVersion.Parse("6.0.0"))).ConfigureAwait(true);

        if (probe is null || probe.Vulnerabilities.Length == 0)
        {
            Console.WriteLine($"Probe failed, {context.SourceRepository.PackageSource.SourceUri} did not return any vulnerabilities for known vulnerable package 'System.Security.Cryptography.Xml_6.0.0'");
            return 1;
        }

        // Now load all the packages listed in the dependency files

        using var reader = new Microsoft.Extensions.DependencyModel.DependencyContextJsonReader();

        var models = files.Select(file =>
        {
            using var stream = File.OpenRead(file);
            return reader.Read(stream);
        });

        var packageIdentities = models
            .SelectMany(m => m.RuntimeLibraries)
            .Where(l => l.Type == "package")
            .Select(l => new PackageIdentity(l.Name, NuGetVersion.Parse(l.Version)))
            .Distinct()
            .ToArray();

        // Now load the package info for all the packages

        var packages = (await Task.WhenAll(packageIdentities.Select(id => context.GetPackageInfo(id))).ConfigureAwait(true))
            .ExceptNullItems()
            .OrderBy(package => package.Id)
            .ThenBy(package => package.Version)
            .ToDictionary(package => $"{package.Id}.{package.Version}");

        // Now create a report

        var payload = new Payload(1, context.SourceRepository.PackageSource.SourceUri.ToString(), packages);

        var jsonSerializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

        var json = JsonSerializer.Serialize(payload, jsonSerializerOptions);

        Console.WriteLine(json);

        return 0;
    }

    private static string[] GetFiles(string? fileOrDirectory)
    {
        fileOrDirectory ??= Directory.GetCurrentDirectory();

        if (Directory.Exists(fileOrDirectory))
        {
            return Directory.GetFiles(fileOrDirectory, "*.deps.json", SearchOption.TopDirectoryOnly);
        }

        if (File.Exists(fileOrDirectory))
        {
            return new[] { fileOrDirectory };
        }

        return Array.Empty<string>();
    }
}

internal sealed record Payload(int ReportVersion, string SourceRepository, Dictionary<string, Package> Packages);

internal sealed record Vulnerability(int Severity, Uri AdvisoryUrl);

internal sealed record Package(string Id, Version Version, Vulnerability[] Vulnerabilities);
