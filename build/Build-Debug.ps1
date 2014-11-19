param(
    [string]$packageVersion = $null
)

$here = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)";
& $here\Build.ps1 -config 'Debug' -packageVersion $packageVersion;
