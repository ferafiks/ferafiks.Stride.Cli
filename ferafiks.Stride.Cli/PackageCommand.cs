using Stride.Core.Assets;
using Stride.Core.IO;
using ferafiks.Stride.Cli.Core;
using System.CommandLine;

namespace ferafiks.Stride.Cli;

internal static class PackageCommand
{
    public static Command Create(PackageFileManager packageFileManager)
    {
        var path = new Argument<string>("path") { Description = "Path to project package or solution file.", Arity = ArgumentArity.ZeroOrOne };

        return new Command("package", "Commands related to .sdpkg files.")
        {
            Arguments = { path },
            Subcommands =
            {
                CreateNew(packageFileManager, path),
                CreateAssets(packageFileManager, path),
                CreateResources(packageFileManager, path),
                CreateRootAssets(packageFileManager, path)
            }
        };
    }

    public static Command CreateNew(PackageFileManager packageFileManager, Argument<string> path)
    {
        var command = new Command("regenerate", "Regenerates the project package file.");

        command.SetAction(result =>
        {
            if (packageFileManager.TryFindFile(result.GetValue(path)!, out var pathValue) &&
                packageFileManager.TryLoadPackage(pathValue!, out var package) &&
                packageFileManager.TrySavePackage(package!))
            {
                Console.WriteLine($"Package file has been saved at {pathValue}");
            }
        });

        return command;
    }

    public static Command CreateAssets(PackageFileManager packageFileManager, Argument<string> path) =>
        CreateFolderCommand(packageFileManager, path, "assetfolders", "asset", x => x.AssetFolders, x => x.Path, x => new AssetFolder(x));

    public static Command CreateResources(PackageFileManager packageFileManager, Argument<string> path) =>
        CreateFolderCommand(packageFileManager, path, "resourcefolders", "resource", x => x.ResourceFolders, x => x, x => x);

    private static Command CreateFolderCommand<T>(PackageFileManager packageFileManager, Argument<string> path, string commandName, string itemType, Func<Package, IList<T>> getList, Func<T, UDirectory> toDir, Func<UDirectory, T> toItem)
    {
        var folder = new Argument<string>("folder") { Description = $"Path to the {itemType} folder." };
        
        var list = new Command("list", $"Lists {itemType} folders that are defined in the project package.");
        list.SetAction(result =>
        {
            if (packageFileManager.TryFindFile(result.GetValue(path)!, out var pathValue) &&
                packageFileManager.TryLoadPackage(pathValue!, out var package))
            {
                Console.WriteLine($"List of {itemType} folders found in project package:");
                foreach (var item in getList(package!))
                    Console.WriteLine($"  {Path.GetRelativePath(Environment.CurrentDirectory, Path.Combine(Path.GetDirectoryName(package!.FullPath)!, toDir(item).ToOSPath()))}");
                
                Console.WriteLine();
            }
        });

        var add = new Command("add", $"Adds a folder to the {itemType} folder list.")
        {
            Arguments = { folder }
        };
        add.SetAction(result =>
        {
            if (packageFileManager.TryFindFile(result.GetValue(path)!, out var pathValue) &&
                packageFileManager.TryLoadPackage(pathValue!, out var package))
            {
                var folderValue = Path.GetFullPath(result.GetValue(folder)!);
                var folderDir = new UDirectory(Path.GetRelativePath(Path.GetDirectoryName(package!.FullPath)!, folderValue));
                if (!Directory.Exists(folderValue))
                {
                    Console.Error.WriteLine($"Folder {folderValue.MakePathRelative()} doesn't exist!");
                    return;
                }

                if (getList(package!).Select(x => toDir(x)).Any(x => x == folderDir))
                {
                    Console.WriteLine("Folder is already added!");
                    return;
                }

                getList(package).Add(toItem(folderDir));
                if (packageFileManager.TrySavePackage(package))
                    Console.WriteLine($"Added {folderValue.MakePathRelative()} to a list of {itemType} folders.");
            }
        });

        var remove = new Command("remove", $"Removes a folder from the {itemType} folder list")
        {
            Arguments = { folder }
        };
        remove.SetAction(result =>
        {
            if (packageFileManager.TryFindFile(result.GetValue(path)!, out var pathValue) &&
                packageFileManager.TryLoadPackage(pathValue!, out var package))
            {
                var folderValue = Path.GetFullPath(result.GetValue(folder)!);
                if (!Directory.Exists(folderValue))
                {
                    Console.Error.WriteLine($"Folder {folderValue.MakePathRelative()} doesn't exist!");
                    return;
                }

                var itemsToRemove = getList(package!)
                    .Where(x => Path.GetFullPath(toDir(x).ToOSPath(), Path.GetDirectoryName(package!.FullPath)!) == folderValue)
                    .ToList();

                if (itemsToRemove.Count == 0)
                {
                    Console.WriteLine("Folder is already removed!");
                    return;
                }

                foreach (var item in itemsToRemove)
                    getList(package!).Remove(item);
                
                if (packageFileManager.TrySavePackage(package!))
                    Console.WriteLine($"Removed {folderValue.MakePathRelative()} from the {itemType} list.");
            }
            
        });

        return new Command(commandName, $"Commands related to {itemType} folders.")
        {
            Subcommands =
            {
                list,
                add,
                remove
            }
        };
    }

    public static Command CreateRootAssets(PackageFileManager packageFileManager, Argument<string> path)
    {
        var list = new Command("list", "Lists root assets that are defined in the project package.");
        list.SetAction(result =>
        {
            PackageSessionResult sessionResult = new();
            PackageSession.Load("/media/files/VS/tpwigBooth/strideBooth.slnx", sessionResult, new());
            Console.WriteLine(sessionResult.Session!.Packages.Count);
            
            if (packageFileManager.TryFindFile(result.GetValue(path)!, out var pathValue) &&
                packageFileManager.TryLoadPackage(pathValue!, out var package))
            {
                foreach (var item in package!.RootAssets)
                    Console.WriteLine($"{item.HasLocation()}");   
            }

        });

        return new Command("rootassets", "Commands related to the list of root assets.")
        {
            Subcommands =
            {
                list
            }
        };
    }
}
