using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Solutions;

namespace ferafiks.Stride.Cli.Core;

/// <summary>Provides utilities for managing .sdpkg project package files.</summary>
public sealed class PackageFileManager
{
    /// <summary>Looks for a path for the .sdpkg file.</summary>
    /// <param name="path">The provided path. If null, uses <see cref="Environment.CurrentDirectory"/>.</param>
    /// <param name="packageLocation">Full path to the project package file if the file was found, otherwise null.</param>
    /// <param name="silent">When true, doesn't log messages.</param>
    /// <returns>Returns true if it managed to find the project package file.</returns>
    public bool TryFindFile(string? path, out string? packageLocation, bool silent = false)
    {
        path ??= Environment.CurrentDirectory;
        path = Path.GetFullPath(path);

        // Path leads to directory
        if (Directory.Exists(path))
        {
            foreach (var extension in new[] { Package.PackageFileExtension, ".csproj" }.Concat(Solution.SolutionExtensions))
            {
                var items = Directory.GetFiles(path).Where(x => x.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase));
                if (items.Count() == 1 &&
                    TryFindFile(items.First(), out packageLocation, silent: true))
                    return true;
            }
        }

        // Path leads to .sdpkg
        if (path.EndsWith(Package.PackageFileExtension, StringComparison.CurrentCultureIgnoreCase) && Directory.Exists(Path.GetDirectoryName(path)))
        {
            packageLocation = path;
            return true;
        }

        // Path leads to .csproj
        if (path.EndsWith(".csproj", StringComparison.InvariantCultureIgnoreCase) && File.Exists(path))
        {
            packageLocation = Path.ChangeExtension(path, Package.PackageFileExtension);
            return true;
        }

        // Path leads to solution
        if (Solution.SolutionExtensions.Any(x => path.EndsWith(x, StringComparison.InvariantCultureIgnoreCase)) && File.Exists(path))
        {
            // Try finding main project package folder, use old name as a fallback
            var projectName = Path.GetFileNameWithoutExtension(path);
            foreach (var item in new[] { $"{projectName}.Game", projectName })
            {
                var mainPath = Path.Combine(Path.GetDirectoryName(path) ?? "", item);
                if (!File.Exists(mainPath)) continue;
                packageLocation = Path.Combine(mainPath, $"{item}{Package.PackageFileExtension}");
                return true;
            }
        }

        if (!silent)
            Console.Error.WriteLine($"The provided path {path} to project package was invalid!");
        
        packageLocation = null;
        return false;
    }

    /// <summary>Tries to load a project package file.</summary>
    /// <param name="path">Path to the project package.</param>
    /// <param name="package">The project package if it was successfully loaded, otherwise null.</param>
    /// <returns>Returns true if the package was successfully loaded.</returns>
    public bool TryLoadPackage(string path, out Package? package)
    {
        // Create a new package if file doesn't exist
        if (!File.Exists(path))
        {
            package = new Package()
            {
                Meta = new()
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    Version = new PackageVersion(new Version(1, 0, 0)),
                },
                FullPath = Path.GetFullPath(path),
            };
            return true;
        }

        try
        {
            var result = AssetFileSerializer.Load<Package>(path);

            package = result.Asset;
            package.FullPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"An exception has occured while loading package file: {e}");
            package = null;
            return false;
        }
    }

    /// <summary>Tries to save a package to disk.</summary>
    /// <param name="package">The project package to save.</param>
    /// <returns>Returns true if the project package was successfully saved.</returns>
    /// <remarks>The method only logs error messages.</remarks>
    public bool TrySavePackage(Package package)
    {
        try
        {
            AssetFileSerializer.Save(package.FullPath, package, null, new CliLogger());
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"There was an exception while saving package file: {e}");
            return false;
        }
    }
}
