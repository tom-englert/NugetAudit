// ReSharper disable AccessToDisposedClosure

using System.Text.Json;
using DependencyModel;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using TomsToolbox.Essentials;

// Get the dependency files from the command line, or use the current directory if no argument is provided

var arg = args.FirstOrDefault() ?? Directory.GetCurrentDirectory();

var files = Array.Empty<string>();

if (Directory.Exists(arg))
{
    files = Directory.GetFiles(arg, "*.deps.json", SearchOption.TopDirectoryOnly);
}
else if (File.Exists(arg))
{
    files = new[] { arg };
}

if (files.Length == 0)
{
    Console.WriteLine($"No dependency files found in '{arg}'");
    return 1;
}

using var context = new NugetContext();

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

internal sealed record Payload(int ReportVersion, string SourceRepository, Dictionary<string, Package> Packages);

internal sealed record Vulnerability(int Severity, Uri AdvisoryUrl);

internal sealed record Package(string Id, Version Version, Vulnerability[] Vulnerabilities);
