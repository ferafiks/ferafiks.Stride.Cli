# How it works

Here's an explanation in case someone would be looking to recreate it.

## TLDR

Everything you want is located in `Stride.Assets` (contains most things) and `Stride.Assets.Models` (contains model importers).

## Opening a solution

1. Locate msbuild using `MSBuildLocator.RegisterDefaults()`.

    ```csharp
    using Microsoft.Build.Locator;

    MSBuildLocator.RegisterDefaults();
    ```

2. Load the solution using `PackageSession.Load`.

    ```csharp
    PackageSessionResult sessionResult = new();
    PackageSession.Load("path/to/solution", results);
    ```

    > [!TIP]
    > Customize the `sessionResult` to log loading messages (always useful for debugging).
    > 
    > ```csharp
    > PackageSessionResult sessionResult = new();
    > sessionResult.MessageLogged += (obj, args) => Console.WriteLine(args.Message.Text);
    > 
    > // To make all logs appear
    > sessionResult.ActivateLog(LogMessageType.Verbose, LogMessageType.Fatal);
    > ```

3. Profit.

    ```csharp
    var solution = sessionResult.Solution;
    ```

For examples, take a look at beginning of [`Program.cs`](../ferafiks.Stride.Cli/Program.cs) and the loading method present in [`SolutionManager`](../ferafiks.Stride.Cli/Core/SolutionManager.cs).

## Projects

Your local project packages are located in `solution.LocalPackages`.

External packages (such as engine libraries) are located in `solution.Packages` along with packages from `LocalPackages`.

### Current package

Is a LIE! From my testing, `solution.CurrentPackage` seems to just pick the first local package in alphabetical order. Not really working like in Game Studio, where `.Windows` would be the "current package".

### Saving

I mean you should probably just save the entire solution, but I wanted to avoid using that as much as it is possible (I don't trust it), so I just serialized those manually.

`AssetFileSerializer.Save` will work just fine for `Package` instances.

## Assets

> [!NOTE]
> Please keep in mind that Stride treats scripts and shaders as assets. You can filter them out by checking if an asset `is not IProjectAsset`.

### Getting assets from IDs

Use `solution.FindAsset`.

### Get root assets

You might expect to find root assets in `package.RootAssets` and you will... except for assets that are mandated as root (e.g. Game Settings). There is no pretty way of getting them, even Game Studio view models just use reflections:

```csharp
var mandatedRoot = Asset.GetType().GetCustomAttribute<AssetDescriptionAttribute>()?.AlwaysMarkAsRoot ?? false;
```

### Dependencies

Use `solution.DependencyManager` for this. You can see an example of this in [`AssetDependencyMap`](../ferafiks.Stride.Cli/Core/AssetDependencyMap.cs).

### Importing

All importers are stored in `AssetRegistry`. See an example on how to use it in [`AssetsCommand`](../ferafiks.Stride.Cli/AssetsCommand.cs) (look for the `CreateImport` method).
