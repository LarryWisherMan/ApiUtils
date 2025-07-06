#------------------------------------------------------------
# Pick the framework-specific folder (Desktop vs Core)
#------------------------------------------------------------
$framework = if ($PSVersionTable.PSEdition -eq 'Desktop') { 'net48' } else { 'net6.0' }

# Build "…\<module>\net48\ApiUtils.Cmdlets.dll"  (works everywhere)
$frameworkDir = Join-Path -Path $PSScriptRoot -ChildPath $framework
$dllPath = Join-Path -Path $frameworkDir   -ChildPath 'ApiUtils.Cmdlets.dll'

#------------------------------------------------------------
# Import the binary sub-module and capture its metadata
#------------------------------------------------------------
$nestedModule = Import-Module -Name $dllPath -PassThru -Force 
