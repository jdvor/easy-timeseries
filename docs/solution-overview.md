# Solution Overview

| path                             | type                   | contains                                                               |
| -------------------------------- | ---------------------- | ---------------------------------------------------------------------- |
| src/Easy.TimeSeries.Abstractions | C# NuGet package       | Contracts for TimeSeries, so extending it is easy or at least possible |
| src/Easy.TimeSeries              | C# NuGet package       | Core library with most of the implementations                          |
| src/Easy.TimeSeries.SrcGen       | C# NuGet package       | Library source-generating reader and/or writer based on a DTO class    |
| src/Easy.TimeSeries.AzureBlobs   | C# NuGet package       | Library providing storage to Azure Blobs                               |
| src/Easy.TimeSeries.Parquet      | C# NuGet package       | Library with conversion support for TimeSeries -> Apache Parquet       |
| src/Easy.TimeSeries.CmdLine      | C# console             | Developer's swiss knife tool around TimeSeries                         |
| src/Easy.TimeSeries.Benchmarks   | C# console             | Application that runs performance benchmarks                           |
| src/Easy.Sample                  | C# console             | Example application to demonstrate TimeSeries usage                    |
| tests/Easy.TimeSeries.Tests      | C# Xunit tests         | Unit and integration tests                                             |
| docs/                            | mdbook, markdown files | Documentation for both TimeSeries users and contributors               |
| scripts/                         | Bash scripts           | Scripts used in GitHub Actions and/or locally from interactive shell   |
| .github/                         | GitHub yaml files      | CI instructions for GitHub Actions                                     |

## Easy.TimeSeries.Abstractions

## Easy.TimeSeries

## Easy.TimeSeries.AzureBlobs

## Easy.TimeSeries.Parquet

## Easy.TimeSeries.CmdLine

## Easy.TimeSeries.Benchmarks