# Verification

Verified on 2026-10-14 using the .NET 8.0.426 SDK and .NET 8.0.32 runtime.

- The library source compiled with the SDK's Roslyn compiler, nullable analysis
  enabled, warnings treated as errors, and the regular-expression source generator.
- All 31 included test cases passed. The xUnit assertion library was used, with a
  small direct invocation host executing every test method and each theory case.
- Both project files were checked as well-formed XML, and all project references
  and packaged README paths were checked for existence.

This execution environment prevents the .NET CLI and MSBuild from reading their
own process information. Therefore normal solution build, xUnit runner discovery,
NuGet restore through MSBuild, and dotnet pack could not be verified here.
Run the build/test/pack commands in README.md on your development machine.

No NuGet package has been published.
