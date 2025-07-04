# Determine which binary module to load based on PowerShell version
$binaryModulePath = $null

if ($PSVersionTable.PSEdition -eq 'Core')
{
    # PowerShell 7+ - prefer .NET 6.0
    $net6Path = Join-Path $PSScriptRoot 'net6.0\ApiUtils.Binary.dll'
    $net48Path = Join-Path $PSScriptRoot 'net48\ApiUtils.Binary.dll'

    if (Test-Path $net6Path)
    {
        $binaryModulePath = $net6Path
        Write-Verbose "Loading .NET 6.0 binary module for PowerShell Core"
    }
    elseif (Test-Path $net48Path)
    {
        $binaryModulePath = $net48Path
        Write-Verbose "Loading .NET Framework 4.8 binary module for PowerShell Core (fallback)"
    }
}
else
{
    # PowerShell 5.1 - use .NET Framework 4.8
    $net48Path = Join-Path $PSScriptRoot 'net48\ApiUtils.Binary.dll'

    if (Test-Path $net48Path)
    {
        $binaryModulePath = $net48Path
        Write-Verbose "Loading .NET Framework 4.8 binary module for Windows PowerShell"
    }
}

# Import the appropriate binary module
if ($binaryModulePath)
{
    Import-Module $binaryModulePath -Force
    Write-Verbose "Successfully loaded binary module: $binaryModulePath"
}
else
{
    Write-Warning "No compatible binary module found for this PowerShell version"
}
