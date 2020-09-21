# Configuring OmniSharp

OmniSharp exposes a set of configuration options that can be used to modify the behavior of OmniSharp regarding:

 - project system options (MSBuild projects, CSX projects, Cake)
 - code formatting options
 - Roslyn extensions options
 - file options (used to explicitly define which folders OmniSharp should exclude from its scanning)

At startup, OmniSharp obtains the configuration options using the following (hierarchical) order:

 - its own hardcoded defaults
 - Environment variables
 - Command line arguments
 - An `omnisharp.json` file located in `%USERPROFILE%/.omnisharp/`
 - An `omnisharp.json` file located in the working directory which OmniSharp has been pointed at

Each of the configuration sources, can overwrite any of the settings set by the previous source. Options names from all sources are case insensite, e.g.: `msbuild:MSBuildSDKsPath` is the same as `msbuild:msbuildsdkspath`.

# Sections

All the possible configuration options (with their default values) are defined below.

## Project system options

Each project system deals with different types of projects and can be disabled if you wish to only focus on specific features and don't need anything else.

### MSBuild

Used to configure MSBuild solutions and projects. Contains for example `Configuration`, in case you would want to force OmniSharp into a custom one.

```
{
  "msbuild": {
     "enabled": true,
     "ToolsVersion": null,
     "VisualStudioVersion": null,
     "Configuration": null,
     "Platform": null,
     "EnablePackageAutoRestore" : false,
     "MSBuildExtensionsPath": null,
     "TargetFrameworkRootPath" : null,
     "MSBuildSDKsPath": null,
     "RoslynTargetsPath" : null,
     "CscToolPath": null,
     "CscToolExe": null,
     "loadProjectsOnDemand": false
  }
}
```

For `msbuild:MSBuildSDKsPath` to be taken into account, `msbuild:UseLegacySdkResolver` has to be set to `true`.

### Scripting

Used to configure C# scripting (CSX files).

```
{
  "script": {
     "enabled": true,
     "defaultTargetFramework": "net461",
     "enableScriptNuGetReferences": false
  }
}
```

### Cake

Used to configure Cake (cake files).

```
{
  "cake": {
     "enabled": true,
     "BakeryPath ": null
  }
}
```

## Formatting options

Used to control C# formatting.

```
{
  "FormattingOptions": {
    "EnableEditorConfigSupport": false,
    "NewLine": "\n",
    "UseTabs": false,
    "TabSize": 4,
    "IndentationSize": 4,
    "SpacingAfterMethodDeclarationName": false,
    "SpaceWithinMethodDeclarationParenthesis": false,
    "SpaceBetweenEmptyMethodDeclarationParentheses": false,
    "SpaceAfterMethodCallName": false,
    "SpaceWithinMethodCallParentheses": false,
    "SpaceBetweenEmptyMethodCallParentheses": false,
    "SpaceAfterControlFlowStatementKeyword": true,
    "SpaceWithinExpressionParentheses": false,
    "SpaceWithinCastParentheses": false,
    "SpaceWithinOtherParentheses": false,
    "SpaceAfterCast": false,
    "SpacesIgnoreAroundVariableDeclaration": false,
    "SpaceBeforeOpenSquareBracket": false,
    "SpaceBetweenEmptySquareBrackets": false,
    "SpaceWithinSquareBrackets": false,
    "SpaceAfterColonInBaseTypeDeclaration": true,
    "SpaceAfterComma": true,
    "SpaceAfterDot": false,
    "SpaceAfterSemicolonsInForStatement": true,
    "SpaceBeforeColonInBaseTypeDeclaration": true,
    "SpaceBeforeComma": false,
    "SpaceBeforeDot": false,
    "SpaceBeforeSemicolonsInForStatement": false,
    "SpacingAroundBinaryOperator": "single",
    "IndentBraces": false,
    "IndentBlock": true,
    "IndentSwitchSection": true,
    "IndentSwitchCaseSection": true,
	"IndentSwitchCaseSectionWhenBlock": true,
    "LabelPositioning": "oneLess",
    "WrappingPreserveSingleLine": true,
    "WrappingKeepStatementsOnSingleLine": true,
    "NewLinesForBracesInTypes": true,
    "NewLinesForBracesInMethods": true,
    "NewLinesForBracesInProperties": true,
    "NewLinesForBracesInAccessors": true,
    "NewLinesForBracesInAnonymousMethods": true,
    "NewLinesForBracesInControlBlocks": true,
    "NewLinesForBracesInAnonymousTypes": true,
    "NewLinesForBracesInObjectCollectionArrayInitializers": true,
    "NewLinesForBracesInLambdaExpressionBody": true,
    "NewLineForElse": true,
    "NewLineForCatch": true,
    "NewLineForFinally": true,
    "NewLineForMembersInObjectInit": true,
    "NewLineForMembersInAnonymousTypes": true,
    "NewLineForClausesInQuery": true
  }
}
```

## Roslyn extensions options

Used to define refactorings, code actions, analyzer locations that OmniSharp should use ([i.e. Roslynator](https://github.com/JosefPihrt/Roslynator)), analysis timeout, decompilation support (via ILSpy) and unimported type completion in intellisense.

```
{
  "RoslynExtensionsOptions": {
    "documentAnalysisTimeoutMs": 10000,
    "enableDecompilationSupport": true,
    "enableImportCompletion": true,
    "enableAnalyzersSupport": true,
    "locationPaths": [
       "//path_to/code_actions.dll"
    ]
  }
}
```

If `EnableAnalyzersSupport` is not enabled only refactorings are available.

## File options

Used to define which directories and files should be included in OmniSharp's __project file / solution file__ discovery process. __Not individual `.cs` files.__

```
{
  "fileOptions": {
    "systemExcludeSearchPatterns": [
      "**/node_modules/**/*",
      "**/bin/**/*",
      "**/obj/**/*",
      "**/node_modules/**/*"
    ],
    "excludeSearchPatterns": []
  }
}
```
Technically you can use either of those to instruct OmniSharp to not use certain search paths. This is particularly useful when opening folders with lots of subfolders. The difference between `systemExcludeSearchPatterns` and `excludeSearchPatterns` is that the first would typically be used by tools such as VS Code to automatically share its exclusion settings with OmniSharp. The second one is recommended for manual editing.

# Overriding the configuration

## Environment variables

Environment variables should be passed into the OmniSharp process as flattened JSON paths. Instead of `.`, `:` should be used as a delimiter. Environment variables **must be** prefixed with `OMNISHARP_`.

### Example

To override the default `formattingOptions > tabSize` setting of 4 with 2, you should set environment variable `OMNISHARP_formattingOptions:tabSize` with the value `2`.

## Command line arguments

Command line arguments are parsed by OmniSharp after environment variables. Their format is the same as environment variables - flattened JSON paths, with `:` as a delimiter. Additionally, the do not require any prefix - neither `-`, nor `--`.

### Example

To override the default `formattingOptions > tabSize` setting of 4, or anything set by environment variables, with 2, you should launch OmniSharp with the following command:

```
OmniSharp.exe -s {folder path} {other arguments} formattingOptions:tabSize=2
```

## Global omnisharp.json

Next in order is a global `omnisharp.json` located at `%USERPROFILE%/.omnisharp/omnisharp.json`. It can contain only individual settings, and they will be merged into the settings provided by out of the box by OmniSharp, environment variables and command line args, and simply overwrite the matching ones. It's worth noting that,the file is not automatically created by OmniSharp - only its containing folder is. So if you can't find `omnisharp.json` inside `%USERPROFILE%/.omnisharp`, create it manually.

### Example

To override the default `formattingOptions > tabSize` setting of 4, or anything set by environment variables/command line args with 2, you should create the following `omnisharp.json` file at `%USERPROFILE%/.omnisharp/omnisharp.json`:

```json
{
    "formattingOptions": {
        "tabSize": 2
    }
}
```

Because the global `omnisharp.json` is picked up any time OmniSharp starts, regardless of the folder you point OmniSharp at, a global configuration file is the best option for any settings you want to automatically apply to all your projects (machine-wide settings).

## Local omnisharp.json

The highest order of precedence is given to `omnisharp.json` located in the folder which OmniSharp server is looking at. Just like it's the case for the global `omnisharp.json` - you can use it to overwrite the relevant configuration keys.

While a global `omnisharp.json` is relevant for machine-wide settings, a local `omnisharp.json` is suitable for project-specific settings.