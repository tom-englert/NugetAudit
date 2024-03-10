# NugetAudit
[![Build status](https://ci.appveyor.com/api/projects/status/k6rw51lvxdeicwq9/branch/main?svg=true)](https://ci.appveyor.com/project/tom-englert/nugetaudit/branch/main)
[![NuGet Status](https://img.shields.io/nuget/v/TomsToolbox.NugetAudit.svg)](https://www.nuget.org/packages/TomsToolbox.NugetAudit/)

A DotNet command line tool to create a vulnerability report from an applications *.deps.json file

## Intention of this tool
This tool can be used to create a vulnerability report based on the binary deliveries, excluding all build time dependencies.

Unlike `dotnet list package --vulnerable` it only works on the build output, not on the sources, 
so it generates a reproducible result even when projects use e.g. floating package versions.

It's e.g. useful to create a snapshot of the know vulnerabilities at release time and then compare with the current state on a periodical base,
so new vulnerabilities that come up later can be detected and customers can be informed 
to update the software if the vulnerability might affect the product.

## Installation
`dotnet tool install TomsToolbox.NugetAudit -g`

## Usage
```
NugetAudit [--package-source <String>] [--use-config] [--config-name <String>] [--help] [--version] file-or-directory

NugetAudit

Arguments:
  0: file-or-directory    Path to a dependency file or a directory with files '*.deps.json' (Default: .)

Options:
  --package-source <String>    The package source used to get the vulnerability info (Default: https://api.nuget.org/v3/index.json)
  --use-config                 Ignore 'package-source' and read package source from nuget.config
  --config-name <String>       The name of the source, if 'use-config' is true
  -h, --help                   Show help message
  --version                    Show version
```

## Sample output:
```json
{
  "reportVersion": 1,
  "sourceRepository": "https://api.nuget.org/v3/index.json",
  "packages": {
    "Microsoft.IdentityModel.JsonWebTokens.6.32.2.0": {
      "id": "Microsoft.IdentityModel.JsonWebTokens",
      "version": "6.32.2.0",
      "vulnerabilities": [
        {
          "severity": 1,
          "advisoryUrl": "https://github.com/advisories/GHSA-8g9c-28fc-mcx2"
        },
        {
          "severity": 1,
          "advisoryUrl": "https://github.com/advisories/GHSA-59j7-ghrg-fj52"
        }
      ]
    },
    "System.IdentityModel.Tokens.Jwt.6.32.2.0": {
      "id": "System.IdentityModel.Tokens.Jwt",
      "version": "6.32.2.0",
      "vulnerabilities": [
        {
          "severity": 1,
          "advisoryUrl": "https://github.com/advisories/GHSA-8g9c-28fc-mcx2"
        },
        {
          "severity": 1,
          "advisoryUrl": "https://github.com/advisories/GHSA-59j7-ghrg-fj52"
        }
      ]
    }
  }
}
```

## Nuget configuration

To be able to retrieve vulnerabilities, a source repository that supports the package metadata is needed, see e.g. [Where do CVE/GHSA come from?](https://devblogs.microsoft.com/nuget/how-to-scan-nuget-packages-for-security-vulnerabilities/).

This tool uses `https://api.nuget.org/v3/index.json` by default, which supports the package metadata endpoint for vulnerabilities.

An alternate repository can be specified in the command line or via nuget.config [Config file locations and uses](https://learn.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior#config-file-locations-and-uses).
