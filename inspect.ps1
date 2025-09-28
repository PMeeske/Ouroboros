$dllPath = Join-Path $env:USERPROFILE '.nuget\packages\langchain.providers.openai\0.17.0\lib\net8.0\LangChain.Providers.OpenAI.dll'
$coreDir = Join-Path ${env:ProgramFiles} 'dotnet\shared\Microsoft.NETCore.App\8.0.0'
$resolver = [System.Runtime.Loader.AssemblyDependencyResolver]::new($dllPath)
$loadContext = [System.Runtime.Loader.AssemblyLoadContext]::Default
$assembly = $loadContext.LoadFromAssemblyPath($dllPath)
$assembly.GetTypes() | Where-Object { $_.Name -like '*Chat*' } | Select-Object FullName
