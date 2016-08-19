Framework 4.5.1
Properties {
    $solution = "Hangfire.SqlServer.RabbitMq.sln"
}

Include "packages\Hangfire.Build.0.2.5\tools\psake-common.ps1"

Task Default -Depends Collect

Task Test -Depends Compile -Description "Run unit and integration tests under OpenCover." {
    Run-XunitTests "Hangfire.SqlServer.RabbitMq.Tests"
}

Task Merge -Depends Test -Description "Run ILMerge /internalize to merge assemblies." {
    Merge-Assembly "Hangfire.SqlServer.RabbitMq" @("RabbitMq.Client")
}

Task Collect -Depends Merge -Description "Copy all artifacts to the build folder." {
    Collect-Assembly "Hangfire.SqlServer.RabbitMq" "Net45"
}

Task Pack -Depends Collect -Description "Create NuGet packages and archive files." {
    $version = Get-PackageVersion
    
    Create-Archive "Hangfire.SqlServer.RabbitMq-$version"
    Create-Package "Hangfire.SqlServer.RabbitMq" $version
}
