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
function Get-DotNetProjects
{
    param($BuildInfo, $SourcePath, $BuildRoot)

    $projects = @()

    # Check for DotNetProjects configuration in BuildInfo
    if ($BuildInfo.DotNetProjects)
    {
        foreach ($projectConfig in $BuildInfo.DotNetProjects)
        {
            # Handle path resolution more robustly
            $projectSourcePath = $projectConfig.Source

            # If it's already an absolute path, use it as-is
            if (-not [System.IO.Path]::IsPathRooted($projectSourcePath))
            {
                # It's a relative path, resolve it relative to BuildRoot
                $projectSourcePath = Join-Path $BuildRoot $projectConfig.Source
            }

            # Resolve to absolute path
            $projectSourcePath = [System.IO.Path]::GetFullPath($projectSourcePath)
            $projectFile = Join-Path $projectSourcePath "$($projectConfig.Name).csproj"

            $projects += [PSCustomObject]@{
                Name            = $projectConfig.Name
                Path            = $projectSourcePath
                ProjectFile     = $projectFile
                OutputName      = if ($projectConfig.OutputName)
                {
                    $projectConfig.OutputName
                }
                else
                {
                    $projectConfig.Name
                }
                CopyToRoot      = if ($null -ne $projectConfig.CopyToRoot)
                {
                    $projectConfig.CopyToRoot
                }
                else
                {
                    $true
                }
                IncludePdb      = if ($null -ne $projectConfig.IncludePdb)
                {
                    $projectConfig.IncludePdb
                }
                else
                {
                    $true
                }
                IncludeXml      = if ($null -ne $projectConfig.IncludeXml)
                {
                    $projectConfig.IncludeXml
                }
                else
                {
                    $true
                }
                TargetFramework = $projectConfig.TargetFramework
            }
        }
    }

    # Auto-discover .csproj files if no explicit configuration
    if ($projects.Count -eq 0)
    {
        if (Test-Path $SourcePath)
        {
            $csprojFiles = Get-ChildItem -Path $SourcePath -Recurse -Filter "*.csproj" -ErrorAction SilentlyContinue

            foreach ($csproj in $csprojFiles)
            {
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
function Get-PreferredTargetFramework
{
    param($ProjectPath, $Configuration)

    $binPath = Join-Path $ProjectPath 'bin' $Configuration

    if (-not (Test-Path $binPath))
    {
        return $null
    }

    $frameworks = Get-ChildItem -Path $binPath -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending

    # Prefer .NET 6.0+ over .NET Framework
    $preferred = $frameworks | Where-Object { $_.Name -match '^net\d+\.\d+$' } | Select-Object -First 1
    if (-not $preferred)
    {
        $preferred = $frameworks | Where-Object { $_.Name -match '^net\d+$' } | Select-Object -First 1
    }
    if (-not $preferred)
    {
        $preferred = $frameworks | Where-Object { $_.Name -match '^net\d+' } | Select-Object -First 1
    }
    if (-not $preferred)
    {
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
    try
    {
        $dotnetVersion = & dotnet --version 2>$null
        Write-Build -Color 'Green' -Text "✓ .NET SDK found: $dotnetVersion"
    }
    catch
    {
        throw ".NET SDK not found. Please install .NET SDK 6.0 or later."
    }

    # Get .NET projects
    $projects = Get-DotNetProjects -BuildInfo $BuildInfo -SourcePath $SourcePath -BuildRoot $BuildRoot

    if ($projects.Count -eq 0)
    {
        Write-Build -Color 'Yellow' -Text "⚠ No .NET projects found to build"
        return
    }

    $global:checkProj = $projects


    Write-Build -Color 'White' -Text "Found $($projects.Count) .NET project(s):"
    foreach ($project in $projects)
    {

        if ($project.ProjectFile -and (Test-Path $project.ProjectFile))
        {
            Write-Build -Color 'DarkGray' -Text "  ✓ $($project.Name) ($($project.ProjectFile))"
        }
        else
        {
            Write-Build -Color 'Red' -Text "  ✗ $($project.Name) - Project file not found: $($project.ProjectFile)"
        }
    }
}

task Set_GitVersion_Variables {
    # Get the values for task variables, see https://github.com/gaelcolas/Sampler#task-variables.
    . Set-SamplerTaskVariable

    Write-Build -Color 'Cyan' -Text "Setting GitVersion environment variables..."

    try
    {
        # Check if GitVersion is available
        $gitVersionCheck = & dotnet gitversion --help 2>$null
        if ($LASTEXITCODE -ne 0)
        {
            Write-Build -Color 'Yellow' -Text "GitVersion tool not found, skipping GitVersion setup"
            return
        }

        # Get GitVersion output
        $gitVersionJson = & dotnet gitversion 2>$null
        if ($LASTEXITCODE -ne 0)
        {
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
    catch
    {
        Write-Build -Color 'Yellow' -Text "GitVersion setup failed: $($_.Exception.Message)"
        Write-Build -Color 'Yellow' -Text "Continuing with build using default versioning..."
    }
}
# Task to restore .NET packages
task Restore_DotNet_Packages Check_DotNet_Prerequisites, {
    # Get the values for task variables, see https://github.com/gaelcolas/Sampler#task-variables.
    . Set-SamplerTaskVariable

    $projects = Get-DotNetProjects -BuildInfo $BuildInfo -SourcePath $SourcePath -BuildRoot $BuildRoot

    if ($projects.Count -eq 0)
    {
        Write-Build -Color 'Yellow' -Text "No .NET projects to restore"
        return
    }

    Write-Build -Color 'Green' -Text "Restoring .NET packages..."

    foreach ($project in $projects)
    {
        if (-not (Test-Path $project.ProjectFile))
        {
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

        if ($LASTEXITCODE -ne 0)
        {
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

    if ($projects.Count -eq 0)
    {
        Write-Build -Color 'Yellow' -Text "No .NET projects to build"
        return
    }

    Write-Build -Color 'Green' -Text "Building .NET projects..."

    foreach ($project in $projects)
    {
        if (-not (Test-Path $project.ProjectFile))
        {
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

        if ($LASTEXITCODE -ne 0)
        {
            throw "Build failed for $($project.Name) with exit code $LASTEXITCODE"
        }

        Write-Build -Color 'Green' -Text "  ✓ $($project.Name) built successfully"
    }

    Write-Build -Color 'Green' -Text "✓ All .NET projects built successfully"
}

# Task to copy .NET assemblies to module output
task Copy_DotNet_Assemblies Build_DotNet_Projects, {
    # Get the values for task variables, see https://github.com/gaelcolas/Sampler#task-variables.
    . Set-SamplerTaskVariable

    $projects = Get-DotNetProjects -BuildInfo $BuildInfo -SourcePath $SourcePath -BuildRoot $BuildRoot

    if ($projects.Count -eq 0)
    {
        Write-Build -Color 'Yellow' -Text "No .NET assemblies to copy"
        return
    }

    if (-not (Test-Path $builtModuleBase))
    {
        Write-Build -Color 'Red' -Text "Built module base path not found: $builtModuleBase"
        Write-Build -Color 'Yellow' -Text "Make sure Build_Module_ModuleBuilder runs before this task"
        throw "Module output directory not found. Ensure PowerShell module is built first."
    }

    # Define target frameworks to copy (customize as needed)
    $targetFrameworks = $buildInfo.DotNetTargetFrameworks

    Write-Build -Color 'Green' -Text "Copying .NET assemblies to module output..."
    Write-Build -Color 'DarkGray' -Text "  Target directory: $builtModuleBase"
    Write-Build -Color 'DarkGray' -Text "  Target frameworks: $($targetFrameworks -join ', ')"

    foreach ($project in $projects)
    {
        Write-Build -Color 'Yellow' -Text "Processing $($project.Name)..."

        $copiedFrameworks = @()

        # Copy all target frameworks
        foreach ($framework in $targetFrameworks)
        {
            $sourcePath = Join-Path $project.Path 'bin' $Configuration $framework

            if (-not (Test-Path $sourcePath))
            {
                Write-Build -Color 'DarkGray' -Text "  Framework $framework not found, skipping..."
                continue
            }

            # Create framework-specific destination folder
            $frameworkDestination = Join-Path $builtModuleBase $framework
            if (-not (Test-Path $frameworkDestination))
            {
                $null = New-Item -ItemType Directory -Path $frameworkDestination -Force -ErrorAction Stop
            }

            Write-Build -Color 'DarkGray' -Text "  Copying $framework framework..."

            # Copy main assembly
            $dllPath = Join-Path $sourcePath "$($project.OutputName).dll"
            if (Test-Path $dllPath)
            {
                Copy-Item $dllPath $frameworkDestination -Force -ErrorAction Stop
                Write-Build -Color 'Green' -Text "    ✓ Copied $($project.OutputName).dll to $framework/"
                $copiedFrameworks += $framework
            }
            else
            {
                Write-Build -Color 'Yellow' -Text "    ✗ DLL not found: $dllPath"
                continue
            }

            # Copy PDB if requested
            if ($project.IncludePdb)
            {
                $pdbPath = Join-Path $sourcePath "$($project.OutputName).pdb"
                if (Test-Path $pdbPath)
                {
                    Copy-Item $pdbPath $frameworkDestination -Force -ErrorAction SilentlyContinue
                    Write-Build -Color 'DarkGray' -Text "    ✓ Copied $($project.OutputName).pdb to $framework/"
                }
            }

            # Copy XML documentation if requested
            if ($project.IncludeXml)
            {
                $xmlPath = Join-Path $sourcePath "$($project.OutputName).xml"
                if (Test-Path $xmlPath)
                {
                    Copy-Item $xmlPath $frameworkDestination -Force -ErrorAction SilentlyContinue
                    Write-Build -Color 'DarkGray' -Text "    ✓ Copied $($project.OutputName).xml to $framework/"
                }
            }
        }

        if ($copiedFrameworks.Count -gt 0)
        {
            Write-Build -Color 'Green' -Text "  ✓ Successfully copied frameworks: $($copiedFrameworks -join ', ')"
        }
        else
        {
            Write-Build -Color 'Yellow' -Text "  ⚠ No frameworks were copied for $($project.Name)"
        }
    }

    Write-Build -Color 'Green' -Text "✓ .NET assembly copy completed"
}

# Task to clean .NET build outputs
task Clean_DotNet_Projects {
    # Get the values for task variables, see https://github.com/gaelcolas/Sampler#task-variables.
    . Set-SamplerTaskVariable

    $projects = Get-DotNetProjects -BuildInfo $BuildInfo -SourcePath $SourcePath -BuildRoot $BuildRoot

    if ($projects.Count -eq 0)
    {
        Write-Build -Color 'Yellow' -Text "No .NET projects to clean"
        return
    }

    Write-Build -Color 'Green' -Text "Cleaning .NET projects..."

    foreach ($project in $projects)
    {
        if (-not (Test-Path $project.ProjectFile))
        {
            continue
        }

        Write-Build -Color 'DarkGray' -Text "Cleaning $($project.Name)..."

        & dotnet clean $project.ProjectFile --configuration $Configuration --verbosity $DotNetVerbosity

        if ($LASTEXITCODE -ne 0)
        {
            Write-Build -Color 'Yellow' -Text "Clean failed for $($project.Name) (non-critical)"
        }
        else
        {
            Write-Build -Color 'DarkGray' -Text "  ✓ $($project.Name) cleaned"
        }
    }

    Write-Build -Color 'Green' -Text "✓ .NET project cleanup completed"
}

# Composite tasks following Sampler patterns
task Build_DotNet_ModuleBuilder Build_DotNet_Projects, Copy_DotNet_Assemblies
