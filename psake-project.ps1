Framework 4.5.1
Include "packages\Hangfire.Build.0.2.6\tools\psake-common.ps1"

Task Default -Depends Collect

Task Build -Depends Clean -Description "Restore all the packages and build the whole solution." {
    Exec { dotnet build -c Release }
}

Task Merge -Depends Build -Description "Run ILRepack /internalize to merge required assemblies." {
    Repack-Assembly @("Hangfire.SqlServer.RabbitMq", "net452")
    Repack-Assembly @("Hangfire.SqlServer.RabbitMq", "net472")
    
    # Referenced packages aren't copied to the output folder in .NET Core <= 2.X. To make ILRepack run,
    # we need to copy them using the `dotnet publish` command prior to merging them. In .NET Core 3.0
    # everything should be working without this extra step.
    Publish-Assembly "Hangfire.SqlServer.RabbitMq" "netstandard2.0"

    Repack-Assembly @("Hangfire.SqlServer.RabbitMq", "netstandard2.0")
}

Task Test -Depends Merge -Description "Run unit and integration tests against merged assemblies." {
    # Dependencies shouldn't be re-built, because we need to run tests against merged assemblies to test
    # the same assemblies that are distributed to users. Since the `dotnet test` command doesn't support
    # the `--no-dependencies` command directly, we need to re-build tests themselves first.
    Exec { ls "tests\**\*.csproj" | % { dotnet build -c Release --no-dependencies $_.FullName } }
    Exec { ls "tests\**\*.csproj" | % { dotnet test -c Release --no-build $_.FullName } }
}

Task Collect -Depends Merge -Description "Copy all artifacts to the build folder." {
    Collect-Assembly "Hangfire.SqlServer.RabbitMq" "net452"
    Collect-Assembly "Hangfire.SqlServer.RabbitMq" "net472"

    Collect-Assembly "Hangfire.SqlServer.RabbitMq" "netstandard2.0"

    Collect-File "LICENSE.md"
    Collect-File "README.md"
}

Task Pack -Depends Collect -Description "Create NuGet packages and archive files." {
    $version = Get-PackageVersion
    
    Create-Archive "Hangfire.SqlServer.RabbitMq-$version"
    Create-Package "Hangfire.SqlServer.RabbitMq" $version
}

function Publish-Assembly($project, $target) {
    $output = Get-SrcOutputDir $project $target
    Write-Host "Publishing '$project'/$target to '$output'..." -ForegroundColor "Green"
    Exec { dotnet publish --no-build -c Release -o $output -f $target "$base_dir\src\$project" }
    Remove-Item "$output\System.*"
}

function Repack-Assembly($projectWithOptionalTarget, $internalizeAssemblies, $target) {
    $project = $projectWithOptionalTarget
    $target = $null

    $base_dir = resolve-path .
    $ilrepack = "$base_dir\packages\ilrepack.*\tools\ilrepack.exe"

    if ($projectWithOptionalTarget -Is [System.Array]) {
        $project = $projectWithOptionalTarget[0]
        $target = $projectWithOptionalTarget[1]
    }

    Write-Host "Merging '$project'/$target with $internalizeAssemblies..." -ForegroundColor "Green"

    $internalizePaths = @()

    $projectOutput = Get-SrcOutputDir $project $target

    foreach ($assembly in $internalizeAssemblies) {
        $internalizePaths += "$assembly.dll"
    }

    $primaryAssemblyPath = "$project.dll"
    $temp_dir = "$base_dir\temp"

    Create-Directory $temp_dir

    Push-Location
    Set-Location -Path $projectOutput

    Exec { .$ilrepack `
        /out:"$temp_dir\$project.dll" `
        /target:library `
        /internalize `
        $primaryAssemblyPath `
        $internalizePaths `
    }

    Pop-Location

    Move-Files "$temp_dir\$project.*" $projectOutput
}
