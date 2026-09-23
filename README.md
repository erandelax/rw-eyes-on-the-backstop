# Eyes On The Backstop

Makes bullet trajectories longer so that missed shots vanish way beyond weapon max range, add optional obstacle ricochet and penetration, as well as non-targeted attack gizmo.

All changes are optional, somewhat configurable and can be switched on and off in settings.

## Compatibility

If you have Combat Extended you already have better version of it.

# AI disclosure

Mod was built by me micromanaging OpenAI GPT-5.6 Luna with [RimSage MCP](https://rimsage.com/). Open source, MIT, made for my personal use and that is all there is to it.

# How to build

Is built upon [Mod template](https://github.com/Rimworld-Mods/Template) so just like there it is meant to be built with VS Code. Below is a direct quote from original template readme.

### Windows
1. Download and install [.NET Core SDK](https://dotnet.microsoft.com/download/dotnet-core) and [.Net Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48). This step can be skipped if you already have required C# packages from Visual Studio IDE.
2. Install [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp).
3. Clone, pull or download this mod into your Rimworld `Mods` folder.

### Linux
1. Linux `dotnet` setup may vary depending on how you install Rimworld and what distro is being used. Follow [Microsoft's instructions](https://learn.microsoft.com/en-us/dotnet/core/install/linux) to install `dotnet`.
2. Install [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp).
3. Clone, pull or download this mod into your Rimworld `Mods` folder.

## Additional Notes
* By pressing `F5` key VS Code will perform 2 operations: build assembly file and launch Rimworld executable. 
* All intermediate files are kept inside `.vscode` folder.
* For XML only modders remove preLaunchTask line from `.vscode/launch.json` file.
* Modify `.vscode/mod.csproj` and `About/About.xml` according to your needs.
