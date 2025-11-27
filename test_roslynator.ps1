$project = "MonadicPipeline\src\MonadicPipeline.CLI\MonadicPipeline.CLI.csproj"

Write-Host "--- Test 1: List Tokens ---"
dotnet run --project $project list

Write-Host "`n--- Test 2: Simple Pipeline ---"
dotnet run --project $project pipeline --dsl "Set('hello world') | TraceOn()" --trace

Write-Host "`n--- Test 3: Roslynator Fix ---"
$code = @"
using System;
namespace Test {
    class Program {
        void Main() {
            int x; // CS0168
        }
    }
}
"@
# Escape for CLI argument
$escapedCode = $code.Replace("'", "''").Replace("`r`n", "\n").Replace("`n", "\n")
$dsl = "Set('$escapedCode') | UseUniversalFix('id=CS0168')"
dotnet run --project $project pipeline --dsl "$dsl" --trace
