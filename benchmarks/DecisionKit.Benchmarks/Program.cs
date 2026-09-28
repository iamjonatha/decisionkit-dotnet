using System.Reflection;
using BenchmarkDotNet.Running;

// Run every benchmark:            dotnet run -c Release -- --filter *
// Run one class:                  dotnet run -c Release -- --filter *JevProtocolBenchmarks*
// List what is available:         dotnet run -c Release -- --list flat
BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);
