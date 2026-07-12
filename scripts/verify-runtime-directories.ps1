param(
    [string]$PublishDirectory = "",
    [switch]$KeepPublishOutput
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "Kototsubo\Kototsubo.csproj"
$sourceRoot = Join-Path $repositoryRoot "Kototsubo"
$ownsPublishDirectory = [string]::IsNullOrWhiteSpace($PublishDirectory)

if ($ownsPublishDirectory) {
    $PublishDirectory = Join-Path ([System.IO.Path]::GetTempPath()) (
        "kototsubo-runtime-directories-" + [Guid]::NewGuid().ToString("N"))
}

function Normalize-RelativePath {
    param([string]$Path)

    return ($Path -replace "\\", "/").Trim("/")
}

try {
    [xml]$project = Get-Content -Raw -LiteralPath $projectPath
    $declaredDirectories = @(
        $project.Project.ItemGroup.RequiredRuntimeDirectory |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_.Include) } |
            ForEach-Object { Normalize-RelativePath $_.Include } |
            Sort-Object -Unique
    )

    $sourcePattern =
        'Path\.Combine\(\s*\w+\.ContentRootPath\s*,\s*"App_Data"\s*,\s*"([^"]+)"\s*\)'
    $sourceDirectories = @(
        Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter "*.cs" |
            Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
            ForEach-Object {
                $content = Get-Content -Raw -LiteralPath $_.FullName
                foreach ($match in [regex]::Matches($content, $sourcePattern)) {
                    Normalize-RelativePath ("App_Data/" + $match.Groups[1].Value)
                }
            } |
            Sort-Object -Unique
    )

    [array]$missingDeclarations = @(
        $sourceDirectories | Where-Object { $_ -notin $declaredDirectories }
    )
    [array]$staleDeclarations = @(
        $declaredDirectories | Where-Object { $_ -notin $sourceDirectories }
    )

    if ($missingDeclarations.Count -gt 0) {
        throw "Runtime directory declarations are missing: $($missingDeclarations -join ', ')"
    }

    if ($staleDeclarations.Count -gt 0) {
        throw "Runtime directory declarations are stale: $($staleDeclarations -join ', ')"
    }

    [array]$missingProjectDirectories = @(
        $declaredDirectories |
            Where-Object {
                -not (Test-Path -LiteralPath (
                    Join-Path $sourceRoot ($_ -replace "/", "\")) -PathType Container)
            }
    )
    if ($missingProjectDirectories.Count -gt 0) {
        throw "Project runtime directories are missing: $($missingProjectDirectories -join ', ')"
    }

    [array]$missingMarkers = @(
        $declaredDirectories |
            Where-Object {
                -not (Test-Path -LiteralPath (
                    Join-Path $sourceRoot (
                        (($_ -replace "/", "\") + "\.gitkeep"))) -PathType Leaf)
            }
    )
    if ($missingMarkers.Count -gt 0) {
        throw "Runtime directory .gitkeep markers are missing: $($missingMarkers -join ', ')"
    }

    & dotnet publish $projectPath `
        --configuration Release `
        --output $PublishDirectory `
        --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    [array]$missingPublishedDirectories = @(
        $declaredDirectories |
            Where-Object {
                -not (Test-Path -LiteralPath (
                    Join-Path $PublishDirectory ($_ -replace "/", "\")) -PathType Container)
            }
    )

    if ($missingPublishedDirectories.Count -gt 0) {
        throw "Published runtime directories are missing: $($missingPublishedDirectories -join ', ')"
    }

    Write-Output (
        "Runtime directory verification passed: " +
        ($declaredDirectories -join ", "))
}
finally {
    if ($ownsPublishDirectory -and
        -not $KeepPublishOutput -and
        (Test-Path -LiteralPath $PublishDirectory)) {
        Remove-Item -LiteralPath $PublishDirectory -Recurse -Force
    }
}
