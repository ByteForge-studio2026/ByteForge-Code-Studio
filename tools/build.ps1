# ByteForge Code Studio 一键构建 / 发布脚本
#
#   .\tools\build.ps1              # 只构建（Debug）
#   .\tools\build.ps1 -Release     # 构建并发布到 publish\
#
# 说明：设置界面已合并进编辑器（ByteForge.Editor.Wpf），产品就是 ByteForgeCodeStudio.exe 一个 exe。
#       WinUI 3 设置程序（ByteForge.Settings.WinUI）已停止构建 / 打包，源码保留在 src\ 下未删除。

param(
    [switch]$Release,
    [string]$Output = "publish"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

function Get-VsBuildTool {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) { return $null }

    $installPath = & $vswhere -latest -property installationPath 2>$null
    if (-not $installPath) { return $null }

    $tool = Join-Path $installPath "MSBuild\Current\Bin\MSBuild.exe"
    if (Test-Path $tool) { return $tool }

    return $null
}

try {
    # 兼容 Windows PowerShell 5.1：不用 if 表达式赋值
    $config = "Debug"
    if ($Release) { $config = "Release" }
    $vsBuildTool = Get-VsBuildTool

    Write-Host "== 构建（$config）==" -ForegroundColor Cyan

    $projects = @(
        "src\ByteForge.Core\ByteForge.Core.csproj",
        "src\ByteForge.FileIO\ByteForge.FileIO.csproj",
        "src\ByteForge.Syntax\ByteForge.Syntax.csproj",
        "src\ByteForge.Markdown\ByteForge.Markdown.csproj",
        "src\ByteForge.Editor.Wpf\ByteForge.Editor.Wpf.csproj",
        "src\ByteForge.Setup\ByteForge.Setup.csproj"
    )

    foreach ($p in $projects) {
        Write-Host "  -> $p"
        dotnet build $p -c $config
        if ($LASTEXITCODE -ne 0) { throw "构建失败：$p" }
    }

    if ($Release) {
        Write-Host "== 发布（tools/publish.py）==" -ForegroundColor Cyan
        Write-Host "  编辑器 -> obj\payload 暂存 -> payload.zip -> 嵌进单文件 Setup -> publish\" -ForegroundColor DarkGray
        python "$PSScriptRoot\publish.py"
        if ($LASTEXITCODE -ne 0) { throw "发布失败" }
    }
}
finally {
    Pop-Location
}
