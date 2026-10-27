$ErrorActionPreference = 'Stop'

$workspacePath = Split-Path -Parent $MyInvocation.MyCommand.Path
$frameworkPath = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$wpfPath = Join-Path $frameworkPath 'WPF'
$compilerPath = Join-Path $frameworkPath 'csc.exe'

if (-not (Test-Path -LiteralPath $compilerPath)) {
    throw '找不到 Windows 自带的 .NET Framework 编译器。'
}

$references = @(
    "/r:$wpfPath\PresentationCore.dll",
    "/r:$wpfPath\PresentationFramework.dll",
    "/r:$wpfPath\WindowsBase.dll",
    "/r:$frameworkPath\System.Xaml.dll",
    "/r:$frameworkPath\System.Runtime.Serialization.dll",
    "/r:$frameworkPath\System.Drawing.dll",
    "/r:$frameworkPath\System.Windows.Forms.dll",
    "/r:$frameworkPath\System.Core.dll"
)

Push-Location $workspacePath
try {
    & $compilerPath /nologo /target:exe /optimize+ /r:System.Runtime.Serialization.dll `
        /out:native-tests\SchedulerTests.exe `
        native-tests\SchedulerTests.cs `
        native\Reminder.cs native\ReminderScheduler.cs native\ReminderValidator.cs native\ReminderStore.cs
    if ($LASTEXITCODE -ne 0) { throw '测试编译失败。' }

    & .\native-tests\SchedulerTests.exe
    if ($LASTEXITCODE -ne 0) { throw '自动测试失败。' }

    & $compilerPath /nologo /target:exe /platform:anycpu /optimize+ /main:UiSmoke /win32manifest:build\app.manifest `
        $references /out:native-tests\UiSmoke.exe native-tests\UiSmoke.cs native\*.cs
    if ($LASTEXITCODE -ne 0) { throw '界面冒烟测试编译失败。' }

    & .\native-tests\UiSmoke.exe
    if ($LASTEXITCODE -ne 0) { throw '界面冒烟测试失败。' }

    & $compilerPath /nologo /target:winexe /platform:anycpu /optimize+ /win32icon:build\icon.ico /win32manifest:build\app.manifest `
        $references /out:定时提醒.exe native\*.cs
    if ($LASTEXITCODE -ne 0) { throw '应用编译失败。' }

    $output = Get-Item -LiteralPath '.\定时提醒.exe'
    Write-Host ("构建完成：{0}（{1} KB）" -f $output.FullName, [math]::Round($output.Length / 1KB, 1))
}
finally {
    Pop-Location
}
