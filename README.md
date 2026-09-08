# ferafiks' Stride CLI tool

A command-line program for managing [Stride 3D](https://stride3d.net) projects without the need for Game Studio. Intended to be used for code-only projects created with the community toolkit. Notable features:

* Works on all platforms
* More control over packages than in Game Studio
* Importing resources
* Managing root assets
* Listing assets and their paths
* Displaying asset dependency trees

## How it works

This tool uses Stride's assemblies to imitate some of Game Studio's functionality, most notably being able to load a solution file.

## Stride compatibility

Tested with 4.4 and 4.3. Should work for older versions as long as they have a similar project architecture.

## Installation

Just run the following command:

```bash
dotnet tool install -g ferafiks.stride.cli
```

## How to use

The following are a few examples on how to use the tool. You can find more information by executing commands directly with the `--help` flag.

### Importing assets

```bash
# Basic importing (output file extension will be automatically added)
stride-frfks assets import ./MyGame.Game/Resources/MyTexture.png -o ./MyGame.Game/Assets/MyTexture

# List all importers and their asset types
stride-frfks assets listimporters

# Import 3D model and its materials
stride-frfks ./MyGame.Game/Resources/MyModel.obj -o ./MyGame.Game/Assets/ --asset-types ModelAsset,MaterialAsset

# Import a raw asset
stride-frfks ./MyGame.Game/Resources/OtherTexture.jpg -o ./MyGame.Game/Assets/RawTextureData -i RawAssetImporter
```

### Root assets

Adding a root asset:

```bash
# Add asset to the list of root assets for a specific project package
stride-frfks assets root add ./MyGame.Game/Assets/Model.sdm3d -p ./MyGame.Game

# Do it even if there are errors loading the solution
stride-frfks assets root add ./MyGame.Game/Assets/Model.sdm3d -p ./MyGame.Game --ignore-errors

# Make the asset root for the default project package
stride-frfks assets root add ./MyGame.Game/Assets/Model.sdm3d
```

Remove a root asset:

```bash
# Make asset not root
stride-frfks assets root remove ./MyGame.Game/Assets/Model.sdm3d -p ./MyGame.Game

# Make asset not root (using the path in the content system)
stride-frfks assets root remove /MyGame.Game/Model.sdm3d -p ./MyGame.Game
```

Listing root assets:

```bash
stride-frfks assets root list -p ./MyGame.Game
```

### Listing assets and their compilation status

```bash
# List assets from project packages, along with their compilation status when building MyGame.Linux
stride-frfks assets list -p ./MyGame.Linux

# List assets from project and external packages, along with their compilation status when building MyGame.Linux
stride-frfks assets list --include-external -p ./MyGame.Linux
```

### Check why an asset is being included

This command is similar to `dotnet nuget why`, creating a tree listing dependencies from the root (specified in the `-p` parameter) to the target asset.

```bash
stride-frfks assets why ./MyGame.Game/Assets/SomeAsset.sdm3d -p ./MyGame.Linux
```

### Creating the `.sdpkg` project package file

```bash
stride-frfks project ./MyGame.Game regenerate
```

### Managing asset and resource folders in a `.sdpkg` project package file

```bash
# Add, remove and list asset folders
stride-frfks project ./MyGame.Game assetfolders add ./MyGame.Game/Assets
stride-frfks project ./MyGame.Game assetfolders remove ./MyGame.Game/Assets
stride-frfks project ./MyGame.Game assetfolders list

# Add, remove and list resource folders
stride-frfks project ./MyGame.Game resourcefolders add ./MyGame.Game/Assets
stride-frfks project ./MyGame.Game resourcefolders remove ./MyGame.Game/Assets
stride-frfks project ./MyGame.Game resourcefolders list
```

## Project structure

I am trying to structure the project similarly to the official [Stride CLI](https://github.com/stride3d/stride/tree/master/sources/launcher/Stride.Cli) tool. One C# project, root containing methods and classes that create commands, shared logic goes into `Core/`.

## Future

For now this tool is available here, but once it will be ready, I will start looking into making it a part of the community toolkit or even the official Stride CLI tool.
