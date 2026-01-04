# Script to add [Trait("Category", "Integration")] to integration test classes
$integrationFiles = @(
    "D:\projects\Ouroboros\src\Ouroboros.Tests\IntegrationTests\CliIntegrationTests.cs",
    "D:\projects\Ouroboros\src\Ouroboros.Tests\IntegrationTests\GitHubModelsTests.cs",
    "D:\projects\Ouroboros\src\Ouroboros.Tests\IntegrationTests\SelfCritiqueIntegrationTests.cs",
    "D:\projects\Ouroboros\src\Ouroboros.Tests\Tests\DagMeTTaIntegrationTests.cs",
    "D:\projects\Ouroboros\src\Ouroboros.Tests\Tests\GitHubModelsIntegrationTests.cs",
    "D:\projects\Ouroboros\src\Ouroboros.Tests\Tests\OllamaCloudIntegrationTests.cs",
    "D:\projects\Ouroboros\src\Ouroboros.Tests\Tests\UnifiedOrchestrationIntegrationTests.cs",
    "D:\projects\Ouroboros\src\Ouroboros.Tests\Tests\Genetic\GeneticPipelineIntegrationTests.cs",
    "D:\projects\Ouroboros\src\Ouroboros.Tests\Tests\LawsOfForm\LawsOfFormIntegrationTests.cs"
)

$count = 0
foreach ($filePath in $integrationFiles) {
    if (Test-Path $filePath) {
        $content = Get-Content $filePath -Raw
        if ($content -notmatch '\[Trait\("Category"') {
            # Add the trait before any class declaration that contains "Test" or "Integration" in the name
            $updated = $content -replace '((?:public|internal)\s+(?:sealed\s+|abstract\s+|static\s+)?class\s+\w*(?:Test|Integration)\w*)', "[Trait(`"Category`", `"Integration`")]`n`$1"

            if ($updated -ne $content) {
                Set-Content -Path $filePath -Value $updated -NoNewline
                $count++
                Write-Host "Updated: $filePath"
            }
        }
    }
}
Write-Host "`nTotal files updated: $count"
