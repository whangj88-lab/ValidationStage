# 업체 배포본을 만든다: Release x64 빌드 -> dist\ValidationStage_SDK_v<버전>\ (Bin + Sample + README) -> 검증 빌드 -> zip
# 실행: powershell -ExecutionPolicy Bypass -File Package\Build-Package.ps1
# 버전은 ValidationStage.Devices\Properties\AssemblyInfo.cs 의 AssemblyVersion 네 자리(예: 1.0.0.0)를 그대로 쓴다 (배포 전에 올릴 것).

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$distDir = Join-Path $root 'dist'

# DLL 과 함께 넘기는 파일 (README 의 "Bin" 목록과 같아야 한다)
$binFiles = @(
    'ValidationStage.Devices.dll',
    'ValidationStage.Devices.xml',
    'PI_GCS2_DLL_x64.dll',
    'OrbitLibrary.dll',
    'InTheHand.Net.Personal.dll'
)

# 샘플 소스에서 빼는 것: 빌드 결과물, VS 사용자 설정, 프로젝트에 포함되지 않은 옛 코드(Motion\SK_Motion.cs)
$sampleExcludeDirs = @('bin', 'obj', 'Motion')
$sampleExcludeExtensions = @('.user')

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\amd64\MSBuild.exe' | Select-Object -First 1
        if ($found) { return $found }
    }
    throw 'MSBuild.exe 를 찾지 못했습니다 (Visual Studio 설치 확인)'
}

function Invoke-Build([string]$msbuild, [string]$solution) {
    & $msbuild $solution -t:Rebuild -p:Configuration=Release -p:Platform=x64 -v:minimal -m -nologo
    if ($LASTEXITCODE -ne 0) {
        throw "빌드 실패: $solution"
    }
}

function Write-Utf8Bom([string]$path, [string]$text) {
    [IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding $true))
}

$msbuild = Find-MSBuild
Write-Host "MSBuild: $msbuild"

# 1. 사내 솔루션 Release 빌드
Write-Host '== Release x64 빌드'
Invoke-Build $msbuild (Join-Path $root 'ValidationStage.sln')

$devicesOut = Join-Path $root 'ValidationStage.Devices\bin\x64\Release'
$v = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $devicesOut 'ValidationStage.Devices.dll')).Version
$version = $v.ToString()
$packageName = "ValidationStage_SDK_v$version"
$packageDir = Join-Path $distDir $packageName
$zipPath = Join-Path $distDir "$packageName.zip"
Write-Host "== 패키지: $packageName"

try {
    if (Test-Path $packageDir) { Remove-Item $packageDir -Recurse -Force }
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
}
catch {
    throw "이전 배포본을 지우지 못했습니다. dist 의 샘플 솔루션이 Visual Studio 에 열려 있거나 탐색기/압축 프로그램이 사용 중이면 닫고 다시 실행하세요. ($($_.Exception.Message))"
}
New-Item -ItemType Directory -Force $packageDir | Out-Null

# 2. Bin
$binDir = Join-Path $packageDir 'Bin'
New-Item -ItemType Directory -Force $binDir | Out-Null
foreach ($file in $binFiles) {
    $source = Join-Path $devicesOut $file
    if (-not (Test-Path $source)) { throw "배포 파일이 없습니다: $source" }
    Copy-Item $source $binDir
}

# 3. Sample 소스
$sampleSource = Join-Path $root 'ValidationStage'
$sampleDir = Join-Path $packageDir 'Sample'
$sampleProjectDir = Join-Path $sampleDir 'ValidationStage'
Get-ChildItem $sampleSource -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($sampleSource.Length + 1)
    $topDir = $relative.Split('\')[0]
    if ($relative.Contains('\') -and $sampleExcludeDirs -contains $topDir) { return }
    if ($sampleExcludeExtensions -contains $_.Extension) { return }
    $target = Join-Path $sampleProjectDir $relative
    New-Item -ItemType Directory -Force (Split-Path $target -Parent) | Out-Null
    Copy-Item $_.FullName $target
}

# 사내 원격 디버그 구성(Remote|x64, 사내 공유 폴더 경로)은 배포본에서 뺀다
$csprojPath = Join-Path $sampleProjectDir 'ValidationStage.csproj'
$csproj = [IO.File]::ReadAllText($csprojPath)
$csproj = [regex]::Replace($csproj, "(?s)\r?\n\s*<PropertyGroup Condition=""'\`$\(Configuration\)\|\`$\(Platform\)' == 'Remote\|x64'"">.*?</PropertyGroup>", '')
Write-Utf8Bom $csprojPath $csproj

# 샘플 프로젝트만 들어 있는 솔루션 (프로젝트 GUID 는 사내 솔루션과 같다)
$sln = @'

Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 16
VisualStudioVersion = 16.0.28701.123
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ValidationStage", "ValidationStage\ValidationStage.csproj", "{7C2C6C2E-8C9D-4E6B-9E3D-1F9F6C6B9A11}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|x64 = Debug|x64
		Release|x64 = Release|x64
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7C2C6C2E-8C9D-4E6B-9E3D-1F9F6C6B9A11}.Debug|x64.ActiveCfg = Debug|x64
		{7C2C6C2E-8C9D-4E6B-9E3D-1F9F6C6B9A11}.Debug|x64.Build.0 = Debug|x64
		{7C2C6C2E-8C9D-4E6B-9E3D-1F9F6C6B9A11}.Release|x64.ActiveCfg = Release|x64
		{7C2C6C2E-8C9D-4E6B-9E3D-1F9F6C6B9A11}.Release|x64.Build.0 = Release|x64
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
'@
Write-Utf8Bom (Join-Path $sampleDir 'ValidationStage.sln') ($sln -replace "`r?`n", "`r`n")

# 4. README
$readme = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'README.txt')).Replace('{VERSION}', $version)
Write-Utf8Bom (Join-Path $packageDir 'README.txt') ($readme -replace "`r?`n", "`r`n")

# 5. 검증: 패키지만 따로 복사해 업체와 같은 조건(DLL 소스 없음)으로 샘플을 빌드한다
Write-Host '== 배포본 단독 빌드 확인'
$testDir = Join-Path ([IO.Path]::GetTempPath()) "ValidationStage_PackageTest\$packageName"
if (Test-Path $testDir) { Remove-Item $testDir -Recurse -Force }
Copy-Item $packageDir $testDir -Recurse
Invoke-Build $msbuild (Join-Path $testDir 'Sample\ValidationStage.sln')
$testOut = Join-Path $testDir 'Sample\ValidationStage\bin\x64\Release'
foreach ($file in @('ValidationStage.exe') + $binFiles) {
    if (-not (Test-Path (Join-Path $testOut $file))) { throw "배포본 빌드 결과에 파일이 없습니다: $file" }
}
Remove-Item (Split-Path $testDir -Parent) -Recurse -Force

# 6. zip
Compress-Archive -Path $packageDir -DestinationPath $zipPath
Write-Host "== 완료: $zipPath"
