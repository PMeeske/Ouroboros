# Script to add [Trait("Category", "Unit")] to all unit test classes
$testFiles = Get-ChildItem -Path "D:\projects\Ouroboros\src\Ouroboros.Tests\Tests" -Recurse -Filter "*.cs" |
    Where-Object { $_.Name -notmatch 'Integration' -and $_.Directory.Name -ne 'IntegrationTests' }

$count = 0
foreach ($file in $testFiles) {
    $content = Get-Content $file.FullName -Raw
    if ($content -notmatch '\[Trait\("Category"') {
        # Add the trait before any class declaration that contains "Test" in the name
        # Match: public class, public sealed class, public abstract class, public static class, internal class
        $updated = $content -replace '((?:public|internal)\s+(?:sealed\s+|abstract\s+|static\s+)?class\s+\w*Test\w*)', "[Trait(`"Category`", `"Unit`")]`n`$1"

        if ($updated -ne $content) {
            Set-Content -Path $file.FullName -Value $updated -NoNewline
            $count++
            Write-Host "Updated: $($file.Name)"
        }
    }
}
Write-Host "`nTotal files updated: $count"
