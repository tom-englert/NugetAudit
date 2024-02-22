dotnet tool update TomsToolbox.LicenseGenerator --global
build-license -i "%~dp0src\NugetAudit.sln" -o "%~dp0Notice.txt"
