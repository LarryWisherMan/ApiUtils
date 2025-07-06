# Generic .NET build tasks for PowerShell modules following Sampler patterns
# Supports multiple binary projects and flexible configuration

param
(
    [Parameter()]
    [System.String]
    $ProjectName = (property ProjectName ''),

    [Parameter()]
    [System.String]
    $SourcePath = (property SourcePath ''),

    [Parameter()]
    [System.String]
    $OutputDirectory = (property OutputDirectory (Join-Path $BuildRoot 'output')),

    [Parameter()]
    [System.String]
    $BuiltModuleSubdirectory = (property BuiltModuleSubdirectory ''),

    [Parameter()]
    [System.Management.Automation.SwitchParameter]
    $VersionedOutputDirectory = (property VersionedOutputDirectory $true),

    [Parameter()]
    [System.String]
    $BuildModuleOutput = (property BuildModuleOutput (Join-Path $OutputDirectory $BuiltModuleSubdirectory)),

    [Parameter()]
    [System.String]
    $ModuleVersion = (property ModuleVersion ''),

    [Parameter()]
    [System.String]
    $Configuration = (property Configuration 'Release'),

    [Parameter()]
    [System.String]
    $DotNetVerbosity = (property DotNetVerbosity 'minimal'),

    [Parameter()]
    [System.Collections.Hashtable]
    $BuildInfo = (property BuildInfo @{ })
)

# Helper function to get .NET projects from configuration
# Helper function to get .NET projects from configuration
function Get-DotNetProjects {
    param($BuildInfo, $SourcePath, $BuildRoot)

    $projects = @()

    # Check for DotNetProjects configuration in BuildInfo
    if ($BuildInfo.DotNetProjects) {
        foreach ($projectConfig in $BuildInfo.DotNetProjects) {
            # Handle path resolution more robustly
            $projectSourcePath = $projectConfig.Source

            # If it's already an absolute path, use it as-is
            if (-not [System.IO.Path]::IsPathRooted($projectSourcePath)) {
                # It's a relative path, resolve it relative to BuildRoot
                $projectSourcePath = Join-Path $BuildRoot $projectConfig.Source
            }

            # Resolve to absolute path
            $projectSourcePath = [System.IO.Path]::GetFullPath($projectSourcePath)
            $projectFile = Join-Path $projectSourcePath "$($projectConfig.Name).csproj"

            $projects += [PSCustomObject]@{
                Name             = $projectConfig.Name
                Path             = $projectSourcePath
                ProjectFile      = $projectFile
                OutputName       = if ($projectConfig.OutputName) {
                    $projectConfig.OutputName
                }
                else {
                    $projectConfig.Name
                }
                CopyToRoot       = if ($null -ne $projectConfig.CopyToRoot) {
                    $projectConfig.CopyToRoot
                }
                else {
                    $true
                }
                IncludePdb       = if ($null -ne $projectConfig.IncludePdb) {
                    $projectConfig.IncludePdb
                }
                else {
                    $true
                }
                IncludeXml       = if ($null -ne $projectConfig.IncludeXml) {
                    $projectConfig.IncludeXml
                }
                else {
                    $true
                }
                TargetFrameworks = if ($projectConfig.TargetFrameworks) {
                    $projectConfig.TargetFrameworks
                }
                elseif ($projectConfig.TargetFramework) {
                    @($projectConfig.TargetFramework)
                }
                else {
                    @()    # default: let copy task decide
                }
            }
        }
    }

    # Auto-discover .csproj files if no explicit configuration
    if ($projects.Count -eq 0) {
        if (Test-Path $SourcePath) {
            $csprojFiles = Get-ChildItem -Path $SourcePath -Recurse -Filter "*.csproj" -ErrorAction SilentlyContinue

            foreach ($csproj in $csprojFiles) {
                $projectName = $csproj.BaseName
                $projects += [PSCustomObject]@{
                    Name            = $projectName
                    Path            = $csproj.Directory.FullName
                    ProjectFile     = $csproj.FullName
                    OutputName      = $projectName
                    CopyToRoot      = $true
                    IncludePdb      = $true
                    IncludeXml      = $true
                    TargetFramework = $null
                }
            }
        }
    }

    return $projects
}

# Helper function to get best target framework for copying
function Get-PreferredTargetFramework {
    param($ProjectPath, $Configuration)

    $binPath = Join-Path $ProjectPath 'bin' $Configuration

    if (-not (Test-Path $binPath)) {
        return $null
    }

    $frameworks = Get-ChildItem -Path $binPath -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending

    # Prefer .NET 6.0+ over .NET Framework
    $preferred = $frameworks | Where-Object { $_.Name -match '^net\d+\.\d+$' } | Select-Object -First 1
    if (-not $preferred) {
        $preferred = $frameworks | Where-Object { $_.Name -match '^net\d+$' } | Select-Object -First 1
    }
    if (-not $preferred) {
        $preferred = $frameworks | Where-Object { $_.Name -match '^net\d+' } | Select-Object -First 1
    }
    if (-not $preferred) {
        $preferred = $frameworks | Select-Object -First 1
    }

    return $preferred.Name
}

# Task to check .NET prerequisites
task Check_DotNet_Prerequisites {
    # Get the values for task variables, see https://github.com/gaelcolas/Sampler#task-variables.
    . Set-SamplerTaskVariable

    Write-Build -Color 'Cyan' -Text "Checking .NET prerequisites..."

    # Check if dotnet CLI is available
    try {
        $dotnetVersion = & dotnet --version 2>$null
        Write-Build -Color 'Green' -Text "✓ .NET SDK found: $dotnetVersion"
    }
    catch {
        throw ".NET SDK not found. Please install .NET SDK 6.0 or later."
    }

    # Get .NET projects
    $projects = Get-DotNetProjects -BuildInfo $BuildInfo -SourcePath $SourcePath -BuildRoot $BuildRoot

    if ($projects.Count -eq 0) {
        Write-Build -Color 'Yellow' -Text "⚠ No .NET projects found to build"
        return
    }

    $global:checkProj = $projects


    Write-Build -Color 'White' -Text "Found $($projects.Count) .NET project(s):"
    foreach ($project in $projects) {

        if ($project.ProjectFile -and (Test-Path $project.ProjectFile)) {
            Write-Build -Color 'DarkGray' -Text "  ✓ $($project.Name) ($($project.ProjectFile))"
        }
        else {
            Write-Build -Color 'Red' -Text "  ✗ $($project.Name) - Project file not found: $($project.ProjectFile)"
        }
    }
}

task Set_GitVersion_Variables {
    # Get the values for task variables, see https://github.com/gaelcolas/Sampler#task-variables.
    . Set-SamplerTaskVariable

    Write-Build -Color 'Cyan' -Text "Setting GitVersion environment variables..."

    try {
        # Check if GitVersion is available
        $gitVersionCheck = & dotnet gitversion --help 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Build -Color 'Yellow' -Text "GitVersion tool not found, skipping GitVersion setup"
            return
        }

        # Get GitVersion output
        $gitVersionJson = & dotnet gitversion 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Build -Color 'Yellow' -Text "GitVersion failed to execute, skipping GitVersion setup"
            return
        }

        $gitVersionOutput = $gitVersionJson | ConvertFrom-Json

        # Set environment variables for .NET build
        $env:GitVersion_SemVer = $gitVersionOutput.SemVer
        $env:GitVersion_AssemblySemVer = $gitVersionOutput.AssemblySemVer
        $env:GitVersion_AssemblySemFileVer = $gitVersionOutput.AssemblySemFileVer
        $env:GitVersion_InformationalVersion = $gitVersionOutput.InformationalVersion

        Write-Build -Color 'Green' -Text "✓ GitVersion variables set:"
        Write-Build -Color 'DarkGray' -Text "  SemVer: $($gitVersionOutput.SemVer)"
        Write-Build -Color 'DarkGray' -Text "  AssemblySemVer: $($gitVersionOutput.AssemblySemVer)"
        Write-Build -Color 'DarkGray' -Text "  AssemblySemFileVer: $($gitVersionOutput.AssemblySemFileVer)"
        Write-Build -Color 'DarkGray' -Text "  InformationalVersion: $($gitVersionOutput.InformationalVersion)"
    }
    catch {
        Write-Build -Color 'Yellow' -Text "GitVersion setup failed: $($_.Exception.Message)"
        Write-Build -Color 'Yellow' -Text "Continuing with build using default versioning..."
    }
}
# Task to restore .NET packages
task Restore_DotNet_Packages Check_DotNet_Prerequisites, {
    # Get the values for task variables, see https://github.com/gaelcolas/Sampler#task-variables.
    . Set-SamplerTaskVariable

    $projects = Get-DotNetProjects -BuildInfo $BuildInfo -SourcePath $SourcePath -BuildRoot $BuildRoot

    if ($projects.Count -eq 0) {
        Write-Build -Color 'Yellow' -Text "No .NET projects to restore"
        return
    }

    Write-Build -Color 'Green' -Text "Restoring .NET packages..."

    foreach ($project in $projects) {
        if (-not (Test-Path $project.ProjectFile)) {
            Write-Build -Color 'Yellow' -Text "Skipping restore for $($project.Name) - project file not found"
            continue
        }

        Write-Build -Color 'DarkGray' -Text "Restoring packages for $($project.Name)..."

        $restoreArgs = @(
            'restore'
            $project.ProjectFile
            '--verbosity', $DotNetVerbosity
        )

        & dotnet @restoreArgs

        if ($LASTEXITCODE -ne 0) {
            throw "Package restore failed for $($project.Name) with exit code $LASTEXITCODE"
        }
    }

    Write-Build -Color 'Green' -Text "✓ .NET package restore completed"
}

# Task to build .NET projects
task Build_DotNet_Projects Set_GitVersion_Variables, Restore_DotNet_Packages, {
    # Get the values for task variables, see https://github.com/gaelcolas/Sampler#task-variables.
    . Set-SamplerTaskVariable

    $projects = Get-DotNetProjects -BuildInfo $BuildInfo -SourcePath $SourcePath -BuildRoot $BuildRoot

    if ($projects.Count -eq 0) {
        Write-Build -Color 'Yellow' -Text "No .NET projects to build"
        return
    }

    Write-Build -Color 'Green' -Text "Building .NET projects..."

    foreach ($project in $projects) {
        if (-not (Test-Path $project.ProjectFile)) {
            Write-Build -Color 'Yellow' -Text "Skipping build for $($project.Name) - project file not found"
            continue
        }

        Write-Build -Color 'Yellow' -Text "Building $($project.Name)..."

        $buildArgs = @(
            'build'
            $project.ProjectFile
            '--configuration', $Configuration
            '--no-restore'
            '--verbosity', $DotNetVerbosity
        )

        # Let the .csproj handle versioning via GitVersion environment variables
        # No need to pass version properties manually

        Write-Build -Color 'DarkGray' -Text "  dotnet $($buildArgs -join ' ')"
        Write-Build -Color 'DarkGray' -Text "  (Versions handled by .csproj GitVersion configuration)"

        & dotnet @buildArgs

        if ($LASTEXITCODE -ne 0) {
            throw "Build failed for $($project.Name) with exit code $LASTEXITCODE"
        }

        Write-Build -Color 'Green' -Text "  ✓ $($project.Name) built successfully"
    }

    Write-Build -Color 'Green' -Text "✓ All .NET projects built successfully"
}

# Task to copy .NET assemblies to module output
task Copy_DotNet_Assemblies Build_DotNet_Projects, {
    . Set-SamplerTaskVariable

    $projects = Get-DotNetProjects -BuildInfo $BuildInfo -SourcePath $SourcePath -BuildRoot $BuildRoot

    if ($projects.Count -eq 0) {
        Write-Build -Color 'Yellow' -Text "No .NET assemblies to copy"
        return
    }

    if (-not (Test-Path $builtModuleBase)) {
        Write-Build -Color 'Red' -Text "Built module base path not found: $builtModuleBase"
        Write-Build -Color 'Yellow' -Text "Make sure Build_Module_ModuleBuilder runs before this task"
        throw "Module output directory not found. Ensure PowerShell module is built first."
    }

    foreach ($project in $projects) {
        Write-Build -Color 'Yellow' -Text "Processing $($project.Name)..."

        # ── 1.  Pick list of frameworks in order of precedence ────────────────
        $targetFrameworks =
        if ($project.PSObject.Properties.Name -contains 'TargetFrameworks' -and
            $project.TargetFrameworks.Count) {

            $project.TargetFrameworks
        }
        elseif ($buildInfo.DotNetTargetFrameworks) {
            $buildInfo.DotNetTargetFrameworks
        }
        else {
            @()   # let preferred-framework logic decide
        }

        # ── 2.  If still empty, choose preferred single framework ─────────────
        if (-not $targetFrameworks.Count) {
            $preferredFramework = Get-PreferredTargetFramework -ProjectPath $project.Path -Configuration $Configuration
            if ($preferredFramework) {
                $targetFrameworks = @($preferredFramework)
                Write-Build -Color 'Cyan' -Text "Copying preferred framework '$preferredFramework' only."
            }
            else {
                Write-Build -Color 'Yellow' -Text "No framework found for '$($project.Name)', skipping copy."
                continue
            }
        }
        else {
            Write-Build -Color 'Cyan' -Text "Copying target frameworks: $($targetFrameworks -join ', ')"
        }

        # ── 3.  Copy each selected framework (unchanged code below) ───────────
        foreach ($framework in $targetFrameworks) {

            $sourcePath = Join-Path $project.Path 'bin' $Configuration $framework
            if (-not (Test-Path $sourcePath)) {
                Write-Build -Color 'DarkGray' -Text "Framework $framework not found, skipping..."
                continue
            }

            $frameworkDestination = Join-Path $builtModuleBase $framework
            if (-not (Test-Path $frameworkDestination)) {
                $null = New-Item -ItemType Directory -Path $frameworkDestination -Force -ErrorAction Stop
            }

            # ── Build an -Exclude list based on project flags ─────────────
            $excludes = @()
            if (-not $project.IncludePdb) { $excludes += '*.pdb' }
            if (-not $project.IncludeXml) { $excludes += '*.xml' }

            Write-Build -Color 'DarkGray' -Text "Copying $framework (excludes: $($excludes -join ',') )"

            Copy-Item -Path (Join-Path $sourcePath '*') `
                -Destination $frameworkDestination `
                -Recurse -Force -ErrorAction Stop `
                -Exclude $excludes

            Write-Build -Color 'Green' -Text "✓ Copied $framework folder"

            # ── CopyToRoot: drop main DLL (+optional pdb/xml) in module root ──
            if ($project.CopyToRoot) {
                $rootDest = $builtModuleBase      # e.g. ...\ApiUtils\0.2.0
                $rootFiles = @("$($project.OutputName).dll")

                if ($project.IncludePdb) { $rootFiles += "$($project.OutputName).pdb" }
                if ($project.IncludeXml) { $rootFiles += "$($project.OutputName).xml" }

                foreach ($file in $rootFiles) {
                    $src = Join-Path $sourcePath $file
                    if (Test-Path $src) {
                        Copy-Item $src $rootDest -Force -ErrorAction SilentlyContinue
                    }
                }
            }
        }

    }

    Write-Build -Color 'Green' -Text "✓ .NET assembly copy completed"
}

task Clean_DotNet_Output {
    . Set-SamplerTaskVariable

    if (-not (Test-Path $BuildModuleOutput)) {
        Write-Build -Color 'DarkGray' -Text "Build output path does not exist: $BuildModuleOutput"
        return
    }

    Write-Build -Color 'Cyan' -Text "Cleaning previous .NET assemblies from output module..."

    Get-ChildItem -Path $BuildModuleOutput -Directory -Filter 'net*' -ErrorAction SilentlyContinue | ForEach-Object {
        $frameworkDir = $_.FullName
        Write-Build -Color 'DarkGray' -Text "  Removing: $frameworkDir"
        Remove-Item -Path $frameworkDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Build -Color 'Green' -Text "✓ Cleaned output module .NET folders"
}


# Task to clean .NET build outputs
task Clean_DotNet_Projects {
    . Set-SamplerTaskVariable

    $projects = Get-DotNetProjects -BuildInfo $BuildInfo -SourcePath $SourcePath -BuildRoot $BuildRoot

    if ($projects.Count -eq 0) {
        Write-Build -Color 'Yellow' -Text "No .NET projects to clean"
        return
    }

    Write-Build -Color 'Green' -Text "Cleaning .NET project folders..."

    foreach ($project in $projects) {
        if (-not (Test-Path $project.Path)) {
            continue
        }

        Write-Build -Color 'DarkGray' -Text "  Cleaning bin/ and obj/ for $($project.Name)..."

        $binPath = Join-Path $project.Path 'bin'
        $objPath = Join-Path $project.Path 'obj'

        foreach ($path in @($binPath, $objPath)) {
            if (Test-Path $path) {
                Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue
                Write-Build -Color 'DarkGray' -Text "    ✓ Removed $path"
            }
        }
    }

    Write-Build -Color 'Green' -Text "✓ Project directories cleaned"

    # ─── Clean output module net* folders ─────────────────────────────────────────
    if ($null -ne $builtModuleBase -and (Test-Path $builtModuleBase)) {
        Write-Build -Color 'Cyan' -Text "Cleaning output module: $builtModuleBase"

        # Remove every net* folder (net48, net6.0, net7.0, …) anywhere under the module
        Get-ChildItem -Path $builtModuleBase -Directory -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^net\d' } |
        ForEach-Object {
            Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
            Write-Build -Color 'DarkGray' -Text "    ✓ Removed $($_.FullName)"
        }

        # Remove loose DLL/XML/PDBs that might sit at module root
        Get-ChildItem -Path $builtModuleBase -File -Include '*.dll', '*.pdb', '*.xml' -ErrorAction SilentlyContinue |
        ForEach-Object {
            Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
            Write-Build -Color 'DarkGray' -Text "    ✓ Removed $($_.Name)"
        }

        Write-Build -Color 'Green' -Text "✓ Output module cleaned"
    }
    else {
        Write-Build -Color 'Yellow' -Text "builtModuleBase not found or does not exist: $builtModuleBase"
    }

}


# Composite tasks following Sampler patterns
task Build_DotNet_ModuleBuilder Build_DotNet_Projects, Copy_DotNet_Assemblies
