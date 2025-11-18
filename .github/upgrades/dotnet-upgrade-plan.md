# .NET 10.0 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that an .NET 10.0 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10.0 upgrade.
3. Upgrade PrintSpoolJobService.csproj


## Settings

This section contains settings and data used by execution steps.

### Excluded projects

Table below contains projects that do belong to the dependency graph for selected projects and should not be included in the upgrade.

| Project name                                   | Description                 |
|:-----------------------------------------------|:---------------------------:|


### Aggregate NuGet packages modifications across all projects

NuGet packages used across all selected projects or their dependencies that need version update in projects that reference them.

| Package Name                                 | Current Version | New Version | Description                                   |
|:--------------------------------------------|:---------------:|:-----------:|:----------------------------------------------|
| Microsoft.AspNetCore.Authentication.JwtBearer |   9.0.10        |  10.0.0     | Recommended update for .NET 10.0              |
| Microsoft.Extensions.Hosting.WindowsServices |   9.0.10        |  10.0.0     | Recommended update for .NET 10.0              |
| System.Drawing.Common                         |   9.0.10        |  10.0.0     | Recommended update for .NET 10.0              |


### Project upgrade details

This section contains details about each project upgrade and modifications that need to be done in the project.

#### PrintSpoolJobService.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - `Microsoft.AspNetCore.Authentication.JwtBearer` should be updated from `9.0.10` to `10.0.0`
  - `Microsoft.Extensions.Hosting.WindowsServices` should be updated from `9.0.10` to `10.0.0`
  - `System.Drawing.Common` should be updated from `9.0.10` to `10.0.0`

Other changes:
  - Resolve any API or build issues reported after changing the target framework and package versions.
