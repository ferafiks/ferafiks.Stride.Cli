using Microsoft.CodeAnalysis;
using Stride.Core.Assets;
using Stride.Core.Assets.Yaml;
using Stride.Core.IO;
using ferafiks.Stride.Cli.Core;
using System.CommandLine;

namespace ferafiks.Stride.Cli;

internal static class AssetsCommand
{
    /// <summary>Creates the root solution command.</summary>
    /// <param name="manager">Solution manager used by the command.</param>
    /// <returns>Returns the solution command.</returns>
    public static Command Create(SolutionManager manager)
    {
        var command = new Command("assets", "Commands related to assets")
        {
            Subcommands =
            {
                CreateImport(),
                CreateList(manager),
                CreateListImporters(),
                CreateWhy(manager),
                CreateRoot(manager),
            }
        };

        return command;
    }

    /// <summary>Creates the import subcommand.</summary>
    /// <returns>Returns the import subcommand.</returns>
    public static Command CreateImport()
    {
        var path = new Argument<string>("sourcePath")
        {
            Arity = ArgumentArity.ExactlyOne,
            Description = "Path to the resource.",
        };

        var output = new Option<string>("--output", "-o") { Description = "The output location where the asset should be located." };
        var importer = new Option<string>("--importer", "-i") { Description = "The importer that should be used." };
        var assetTypes = new Option<string>("--asset-types") { Description = "Asset types that are allowed to be created, separated by ','. Special values include: all, first. Default value is: first." };
        var force = new Option<bool>("--force", "-f") { Description = "Forces the creation of asset files, even if they already exist." };
        var absoluteLinks = new Option<bool>("--absolute-links") { Description = "Makes paths to resource files in the assets absolute instead of relative." };

        var command = new Command("import", "Imports a resource as an asset and saves it as a file.")
        {
            Arguments = { path },
            Options = { CommonArguments.OnlyResults, output, importer, assetTypes, force, absoluteLinks }
        };

        command.SetAction(result =>
        {
            CliLogger.BlockStandardWrite = result.GetValue(CommonArguments.OnlyResults);

            var file = result.GetValue(path)!;
            var importers = AssetRegistry.FindImporterForFile(file);

            var importParams = new AssetImporterParameters()
            {
                Logger = new CliLogger(),
            };
            
            if (!TryChooseImporter(importers, result.GetValue(importer), out var targetImporter)) return;
            ChooseAssetTypes(importParams, targetImporter!, result.GetValue(assetTypes) ?? "all");

            // Import
            var assetItems = targetImporter!.Import(new UFile(Path.GetFullPath(file)), importParams);

            // Error out if no assets will be imported
            if (!assetItems.Any())
            {
                Console.Error.WriteLine("Importer didn't import any assets. Try changing the value of --asset-types and try again.");
                return;
            }

            // Importing single asset
            if (assetItems.Count() == 1)
            {
                var firstItem = assetItems.First();
                var extension = Path.GetExtension(firstItem.FullPath);
                var itemOutput = result.GetValue(output) ?? Environment.CurrentDirectory;

                if (Directory.Exists(itemOutput))
                    itemOutput = Path.Combine(itemOutput, Path.GetFileName(firstItem.FullPath));
                
                if (!itemOutput.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase))
                    itemOutput = $"{itemOutput}{extension}";

                TrySaveAsset(itemOutput, firstItem.Asset, firstItem.YamlMetadata, result.GetValue(absoluteLinks), result.GetValue(force));
                return;
            }

            // Import multiple assets
            var outputFolder = result.GetValue(output);
            if (!Directory.Exists(outputFolder))
            {
                Console.Error.WriteLine($"Couldn't find output folder at {outputFolder}.");
                return;
            }

            foreach (var item in assetItems)
                TrySaveAsset(Path.Combine(outputFolder, Path.GetFileName(item.FullPath)), item.Asset, item.YamlMetadata, result.GetValue(absoluteLinks), result.GetValue(force));
        });

        return command;
    }

    /// <summary>Tries to choose an importer.</summary>
    /// <param name="importers">List of available importers for the resource.</param>
    /// <param name="importerName">Name of the importer or null for default.</param>
    /// <param name="result">The target importer when chosen, otherwise null.</param>
    /// <returns>Returns true if the importer was successfully chosen.</returns>
    private static bool TryChooseImporter(IEnumerable<IAssetImporter> importers, string? importerName, out IAssetImporter? result)
    {
        // Try getting from name, if one was provided
        if (importerName != null)
        {
            result = importers.FirstOrDefault(x => x.Name == importerName);
            
            if (result == null)
                Console.Error.WriteLine($"No importer named '{importerName}' matches the resource.");

            return result != null;
        }

        // If no importers are available
        if (!importers.Any())
        {
            Console.Error.WriteLine("There are no importers available for that type of resource.");
            result = null;
            return false;
        }

        // If only one importer is found, not counting RawAssetImporter
        if (importers.Count(x => x is not RawAssetImporter) < 2)
        {
            result = importers.First();
            return true;
        }

        Console.Error.WriteLine("There are multiple importers available for this resource. You have to choose from one of the following using the --importer parameter:");

        var width = importers.Max(x => x.Name.Length);
        foreach (var item in importers)
            Console.Error.WriteLine($"  {item.Name.PadRight(width)}  {item.Description}");

        result = importers.Last();
        return false;
    }

    /// <summary>Chooses importer asset types.</summary>
    /// <param name="parameters">Asset importer parameters.</param>
    /// <param name="importer">The importer.</param>
    /// <param name="assetTypesString">String containing the asset types that the user wants to use.</param>
    private static void ChooseAssetTypes(AssetImporterParameters parameters, IAssetImporter importer, string assetTypesString)
    {
        var availableAssetTypes = importer.AllAssetTypes().ToList();
        var assetTypesValue = assetTypesString.Split(",");
        var includeAllAssets = assetTypesValue.Contains("all");
        
        for (int i = 0; i < availableAssetTypes.Count; i++)
        {
            bool include = includeAllAssets;
            include |=  i == 0 && assetTypesValue.Contains("first");
            include |= assetTypesValue.Contains(availableAssetTypes[i].Name);
            
            parameters.SelectedOutputTypes.Add(availableAssetTypes[i], include);
        }
    }

    /// <summary>Tries to save an asset to disk.</summary>
    /// <param name="path">Path where the asset should be saved.</param>
    /// <param name="asset">Asset to serialize and save.</param>
    /// <param name="assetMetadata">Attached asset metadata.</param>
    /// <param name="absoluteLinks">When true, source links are converted to absolute paths.</param>
    /// <param name="force">When true, the asset will be saved, even if it already exists.</param>
    /// <returns>Returns true if the asset was successfully saved.</returns>
    private static bool TrySaveAsset(string path, Asset asset, AttachedYamlAssetMetadata assetMetadata, bool absoluteLinks, bool force)
    {
        if (File.Exists(path) && !force)
        {
            Console.Error.WriteLine($"Cannot save imported asset, file already exists at {path}.");
            return false;
        }

        if (Directory.Exists(Path.GetDirectoryName(path)))
        {
            Console.Error.WriteLine($"Cannot save imported asset, parent directory does not exist!");
            return false;
        }
        
        // Make path link relative
        if (!absoluteLinks && asset is IAssetWithSource assetWithSource)
            assetWithSource.Source = new UFile(Path.GetRelativePath(Path.GetDirectoryName(path)!, assetWithSource.Source.FullPath));
        
        AssetFileSerializer.Save(path, asset, assetMetadata);
        Console.WriteLine(CliLogger.BlockStandardWrite ? path : $"Created a new asset at {path}.");
        return true;
    }

    /// <summary>Creates the listimporters subcommand.</summary>
    /// <returns>Returns the listimporters subcommand.</returns>
    public static Command CreateListImporters()
    {
        var command = new Command("listimporters", "Lists all importers and types of assets that they can create.");
        command.SetAction(result =>
        {
            foreach (var importer in AssetRegistry.RegisteredImporters)
            {
                Console.WriteLine($"{importer.Name}  {importer.Description}");
                foreach (var type in importer.RootAssetTypes.Concat(importer.AdditionalAssetTypes))
                    Console.WriteLine($"  {type.Name}");
                Console.WriteLine();
            }
        });

        return command;
    }

    /// <summary>Creates the list subcommand.</summary>
    /// <param name="manager">Solution manager used by the command.</param>
    /// <returns>Returns the list subcommand.</returns>
    public static Command CreateList(SolutionManager manager)
    {
        var includeExternal = new Option<bool>("--include-external") { Description = "Includes assets from external packages." };
        var ignoreProject = new Option<bool>("--ignore-project") { Description = "Excludes non-external project packages." };
        var package = new Option<string>("-p", "--package") { Description = "Path to a project package from which to build the dependency tree." };

        var command = new Command("list", "Shows a list of all assets in the project")
        {
            Arguments = { CommonArguments.Solution },
            Options = { CommonArguments.SolutionIgnoreErrors, includeExternal, ignoreProject, package }
        };
        command.SetAction(result =>
        {
            // Get data from arguments
            string? packageLocation = null;
            if (result.GetValue(package) is { } packageValue &&
                !manager.PackageFileManager.TryFindFile(packageValue, out packageLocation))
                return;

            // Solution code
            if (manager.TryFindAndLoadSolution(result.GetValue(CommonArguments.Solution), out var session, result.GetValue(CommonArguments.SolutionIgnoreErrors)))
            {
                // Get the packages that should be displayed
                List<Package> packages = [];
                if (!result.GetValue(ignoreProject)) packages.AddRange(session!.LocalPackages);
                if (result.GetValue(includeExternal)) packages.AddRange(session!.Packages.Except(session.LocalPackages));

                // Create dependency tree
                AssetDependencyMap map = new();
                if (packageLocation != null)
                {
                    if (!manager.TryFindPackageFromLocation(session!, packageLocation, out var targetPackage))
                        return;

                    map = manager.CreateAssetDependencyMap(session!, targetPackage!);
                }

                foreach (var project in packages)
                {
                    Console.WriteLine($"{project.Meta.Name}");
                    foreach (var asset in project.Assets.Where(x => x.Asset is not IProjectAsset))
                    {
                        var status = "";

                        if (map.IncludedInBuildAssets.Contains(asset.Id))
                            status = " (included in build)";
                        if (map.RootAssets.Contains(asset.Id))
                            status = " (root)";

                        Console.WriteLine($"  {asset.Location}{status}");
                    }
                    
                    Console.WriteLine();
                }
            }
        });

        return command;
    }

    /// <summary>Creates the why subcommand.</summary>
    /// <param name="manager">Solution manager used by the command.</param>
    /// <returns>Returns the why subcommand.</returns>
    public static Command CreateWhy(SolutionManager manager)
    {
        var asset = new Argument<string>("asset") { Description = "Path of the asset in the content system." };
        var solution = new Option<string>("--solution") { Description = "Path of the solution file or a directory containing one." };
        var package = new Option<string>("-p", "--package") { Description = "Project package from which to start the search." };

        var command = new Command("why", "Shows the dependency graph for a particular asset.")
        {
            Arguments = { asset },
            Options = { CommonArguments.SolutionIgnoreErrors, solution, package }
        };
        command.SetAction(result =>
        {
            // Get data from arguments
            string? packageLocation = null;
            if (result.GetValue(package) is { } packageValue &&
                !manager.PackageFileManager.TryFindFile(packageValue, out packageLocation))
                return;
            
            var assetValue = result.GetValue(asset);

            if (manager.TryFindAndLoadSolution(result.GetValue(solution), out var session, result.GetValue(CommonArguments.SolutionIgnoreErrors)))
            {
                if (manager.TryFindPackageFromLocation(session!, packageLocation, out var targetPackage))
                    return;
                
                var map = manager.CreateAssetDependencyMap(session!, targetPackage!);

                var target = session!.Packages.SelectMany(x => x.Assets)
                    .FirstOrDefault(x => x.Location == assetValue || x.UnqualifiedUrl == assetValue);
                
                if (target == null)
                {
                    Console.Error.WriteLine($"Couldn't find the asset at path {assetValue}");
                    return;
                }

                Console.WriteLine();
                if (!map.IncludedInBuildAssets.Contains(target.Id))
                {
                    Console.WriteLine("The asset isn't referenced by any other asset that is included in the build.");
                    return;
                }

                ConstructDependencyTree(session!, targetPackage!.Meta.Name ?? "", map, target.Id).WriteToConsole();
            }
        });

        return command;
    }

    /// <summary>Creates a dependency tree used by the why command.</summary>
    /// <param name="session">The loaded package session.</param>
    /// <param name="header">The header text from which the tree will start.</param>
    /// <param name="map">An asset dependency map to use for constructing the tree.</param>
    /// <param name="targetAsset">The asset at which to create stubs.</param>
    /// <returns>Returns the root of the constructed dependency tree.</returns>
    private static TextTreeItem ConstructDependencyTree(PackageSession session, string header, AssetDependencyMap map, AssetId? targetAsset)
    {
        TextTreeItem textTreeRoot = new(header);

        foreach (var item in map.Nodes)
            if (Do(item, out var treeItem))
                textTreeRoot.Add(treeItem!);

        return textTreeRoot;


        bool Do(AssetDependencyMap.Node node, out TextTreeItem? result)
        {
            // Get asset
            var asset = session.FindAsset(node.Asset);
            if (asset == null)
            {
                result = null;
                return false;
            }

            // If the node is the target, stop here and return true
            if (node.Asset == targetAsset)
            {
                result = new TextTreeItem($"{asset.Location}", ConsoleColor.Cyan);
                return true;
            }

            // Try finding the target in children
            result = new TextTreeItem($"{asset.Location}");
            var foundInChild = false;
            foreach (var item in node.Nodes)
            {
                if (node.IsParentedTo(item.Asset))
                {
                    result.Children.Add(new TextTreeItem("..."));
                    continue;
                }

                if (Do(item, out var childItem))
                {
                    foundInChild = true;
                    result.Children.Add(childItem!);
                }
            }

            return foundInChild;
        }
    }

    public static Command CreateRoot(SolutionManager manager)
    {
        var solution = new Option<string?>("--solution", "-s") { Description = "Path to the solution file or directory containing one." };
        var package = new Option<string?>("--package", "-p") { Description = "Path to the project package from which to get root assets." };
        var asset = new Argument<IEnumerable<string>>("asset") { Description = "Path of the asset or assets in the content system or on disk.", Arity = ArgumentArity.OneOrMore };

        var list = new Command("list")
        {
            Options = { solution, package, CommonArguments.SolutionIgnoreErrors, CommonArguments.OnlyResults }
        };
        list.SetAction(result =>
        {
            CliLogger.BlockStandardWrite = result.GetValue(CommonArguments.OnlyResults);

            string? packagePath = null;
            if (result.GetValue(package) is { } resultValue &&
                !manager.PackageFileManager.TryFindFile(resultValue, out packagePath))
                return;
            
            if (manager.TryFindAndLoadSolution(result.GetValue(solution), out var session, result.GetValue(CommonArguments.SolutionIgnoreErrors)) &&
                manager.TryFindPackageFromLocation(session!, packagePath, out var startPackage))
            {
                var root = manager.GetRootAssets(session!, startPackage!);

                if (CliLogger.BlockStandardWrite)
                {
                    foreach (var item in root)
                        if (session!.FindAsset(item) is { } asset)
                            Console.WriteLine(asset.Location);
                    return;
                }

                Console.WriteLine();
                Console.WriteLine("List of root assets:");
                foreach (var item in root)
                    if (session!.FindAsset(item) is { } asset)
                        Console.WriteLine($"  {asset.Location}");

            }
        });

        var add = new Command("add", "Marks assets as root.")
        {
            Arguments = { asset },
            Options = { solution, package, CommonArguments.SolutionIgnoreErrors }
        };
        add.SetAction(result =>
        {
            string? packagePath = null;
            if (result.GetValue(package) is { } resultValue &&
                !manager.PackageFileManager.TryFindFile(resultValue, out packagePath))
                return;

            if (manager.TryFindAndLoadSolution(result.GetValue(solution), out var session, result.GetValue(CommonArguments.SolutionIgnoreErrors)) &&
                manager.TryFindPackageFromLocation(session!, packagePath, out var targetPackage))
            {
                var assets = manager.GetMultipleAssetsFromPaths(session!, result.GetRequiredValue(asset));
                if (assets.Any())
                    manager.AddRootAssets(targetPackage!, assets.Select(x => x.ToReference()));
            }
        });

        var remove = new Command("remove", "Removes assets from root.")
        {
            Arguments = { asset },
            Options = { solution, package, CommonArguments.SolutionIgnoreErrors }
        };
        remove.SetAction(result =>
        {
            string? packagePath = null;
            if (result.GetValue(package) is { } resultValue &&
                !manager.PackageFileManager.TryFindFile(resultValue, out packagePath))
                return;

            if (manager.TryFindAndLoadSolution(result.GetValue(solution), out var session, result.GetValue(CommonArguments.SolutionIgnoreErrors)) &&
                manager.TryFindPackageFromLocation(session!, packagePath, out var targetPackage))
            {
                var assets = manager.GetMultipleAssetsFromPaths(session!, result.GetRequiredValue(asset));
                if (assets.Any())
                    manager.RemoveRootAssets(targetPackage!, assets.Select(x => x.ToReference()));
            }
        });

        return new Command("root", "Commands related to root assets")
        {
            Subcommands =
            {
                list,
                add,
                remove
            }
        };
    }
}
