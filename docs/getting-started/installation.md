# Installation

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later

## Install from NuGet

```bash
dotnet tool install --global RoslynLens
```

## Install from source

```bash
git clone https://github.com/jfmeyers/roslyn-lens.git
cd roslyn-lens
dotnet pack -c Release -o ./nupkgs
dotnet tool install --global --add-source ./nupkgs RoslynLens
```

## Verify installation

```bash
roslyn-lens --help
```

## Update

```bash
dotnet tool update --global RoslynLens
```

## Uninstall

```bash
dotnet tool uninstall --global RoslynLens
```
