
$files = Get-ChildItem -Recurse src/Ouroboros.Application -Include *.cs

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $newContent = $content -replace "namespace LangChainPipeline.CLI", "namespace Ouroboros.Application"
    $newContent = $newContent -replace "namespace Ouroboros.CLI", "namespace Ouroboros.Application"
    $newContent = $newContent -replace "namespace LangChainPipeline.Tools", "namespace Ouroboros.Application.Tools"
    
    if ($content -ne $newContent) {
        Set-Content -Path $file.FullName -Value $newContent
        Write-Host "Updated $($file.Name)"
    }
}
