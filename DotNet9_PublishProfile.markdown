# .NET 9 SDK 빌드 및 배포를 위한 Publish Profile 가이드

이 문서는 .NET 9 SDK를 사용한 빌드 및 배포를 위한 Publish Profile(.pubxml) 설정 방법을 설명합니다. Publish Profile은 `Properties\PublishProfiles` 폴더에 저장되며, `dotnet publish` 명령어로 사용됩니다. 아래는 모든 주요 속성을 포함한 포괄적인 예제와 사용 방법, 그리고 `RuntimeIdentifier` 지정이 성능에 미치는 영향을 포함합니다.

## Publish Profile 예제 (Production.pubxml)

다음은 ASP.NET Core 웹 애플리케이션(또는 콘솔 앱)을 위한 완전한 `.pubxml` 파일 예제입니다. 이 파일은 파일 시스템 배포를 기본으로 하며, .NET 9의 새로운 기능(예: TieredPGO, AppHostDotNetSearch)과 컨테이너 지원을 포함합니다.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <!-- 기본 빌드 설정 -->
    <Configuration>Release</Configuration>
    <Platform>Any CPU</Platform>
    <TargetFramework>net9.0</TargetFramework>
    
    <!-- 출력 디렉토리 (CLI와 GUI 호환) -->
    <PublishDir>bin\publish\</PublishDir>
    <PublishUrl>bin\publish\</PublishUrl>
    
    <!-- 배포 프로토콜 및 메서드 -->
    <PublishProtocol>FileSystem</PublishProtocol>
    <WebPublishMethod>FileSystem</WebPublishMethod>
    <LastUsedBuildConfiguration>Release</LastUsedBuildConfiguration>
    <LastUsedPlatform>Any CPU</LastUsedPlatform>
    
    <!-- 배포 모드 -->
    <DeploymentMode>SelfContained</DeploymentMode>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishSelfContained>true</PublishSelfContained>
    
    <!-- 최적화 속성 -->
    <PublishReadyToRun>true</PublishReadyToRun>
    <TieredPGO>true</TieredPGO>
    <PublishTrimmed>true</PublishTrimmed>
    <TrimMode>full</TrimMode>
    
    <!-- 싱글 파일 및 압축 -->
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
    <ZipCompressed>true</ZipCompressed>
    
    <!-- .NET 9 신규 속성 -->
    <AppHostDotNetSearch>Global</AppHostDotNetSearch>
    <AppHostRelativeDotNet>false</AppHostRelativeDotNet>
    
    <!-- 컨테이너 지원 -->
    <EnableSdkContainerSupport>true</EnableSdkContainerSupport>
    <ContainerFamily>ubuntu.22.04</ContainerFamily>
    <ContainerImageName>myapp</ContainerImageName>
    <ContainerImageTags>latest</ContainerImageTags>
    
    <!-- 기타 배포 옵션 -->
    <DeleteExistingFiles>true</DeleteExistingFiles>
    <ExcludeApp_Data>false</ExcludeApp_Data>
    <UseMerge>false</UseMerge>
    <LaunchSiteAfterPublish>true</LaunchSiteAfterPublish>
    <DeployOnBuild>true</DeployOnBuild>
  </PropertyGroup>
  
  <!-- 포함/제외 파일 설정 -->
  <ItemGroup>
    <DotNetPublishFiles Include="**\*.config" CopyToPublishDirectory="PreserveNewest" />
    <ResolvedFileToPublish Include="bin\Release\net9.0\**\*" Exclude="bin\Release\net9.0\publish\**" CopyToPublishDirectory="PreserveNewest" />
    <Content Update="wwwroot\**\*.*" CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

## 주요 속성 설명

| 속성                  | 설명                                                                 | 기본값/예시 값          |
|-----------------------|---------------------------------------------------------------------|-------------------------|
| Configuration       | 빌드 구성 (Debug/Release).                                         | Release                |
| PublishDir          | 출력 디렉토리 (CLI에서 사용).                                      | bin\publish\           |
| PublishUrl          | 출력 URL (GUI에서 사용).                                           | bin\publish\           |
| DeploymentMode      | 배포 모드 (FrameworkDependent/SelfContained).                      | SelfContained          |
| RuntimeIdentifier   | 대상 런타임 (win-x64, linux-x64 등).                               | win-x64                |
| PublishReadyToRun   | ReadyToRun 컴파일 활성화 (시작 시간 단축).                         | true                   |
| TieredPGO           | Profile-Guided Optimization 활성화 (.NET 9 동적 최적화).            | true                   |
| PublishTrimmed      | 트리밍 활성화 (배포 크기 감소).                                    | true                   |
| PublishSingleFile   | 싱글 파일 배포.                                                    | true                   |
| AppHostDotNetSearch | .NET 런타임 검색 경로 (.NET 9 신규).                               | Global                 |
| DeployOnBuild       | 빌드 시 배포 자동 실행.                                            | true                   |

## `RuntimeIdentifier`와 성능

### `RuntimeIdentifier`란?
- `RuntimeIdentifier` (RID)는 애플리케이션이 실행될 대상 플랫폼(예: `win-x64`, `linux-x64`, `osx-x64`)을 지정합니다. Self-contained 배포에서는 필수이며, Framework-dependent 배포에서는 선택적입니다.
- 설정 예: `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` 또는 `dotnet publish -r win-x64`.

### 성능에 미치는 영향
`RuntimeIdentifier`를 지정하면 다음과 같은 방식으로 성능에 영향을 줄 수 있습니다:

#### (1) Self-contained 배포
- **영향**:
  - 특정 플랫폼에 최적화된 네이티브 코드 생성으로 JIT 컴파일러가 플랫폼별 최적화를 수행.
  - `PublishReadyToRun=true`와 함께 사용 시 미리 컴파일된 코드로 시작 시간 단축.
  - `TieredPGO=true`와 결합 시 동적 PGO가 플랫폼별 하드웨어 특성(예: CPU 캐시, 명령어 세트)에 맞춘 최적화를 수행하여 런타임 성능을 10-20% 향상.
- **단점**:
  - 배포 파일 크기 증가.
  - 플랫폼별로 별도 빌드 필요.

#### (2) Framework-dependent 배포
- **영향**:
  - `RuntimeIdentifier`를 지정하지 않으면 .NET 런타임이 설치된 환경에 의존.
  - 배포 크기가 작고, 여러 플랫폼에서 동일한 빌드 사용 가능.
  - **성능 단점**: 플랫폼별 최적화가 제한되며, JIT 컴파일러가 일반화된 코드를 생성하므로 시작 시간과 런타임 성능이 Self-contained 배포보다 약간 느릴 수 있음.
- **적합한 경우**: 대상 시스템에 .NET 9 런타임이 설치되어 있는 환경(예: Azure App Service).

#### (3) ReadyToRun과 트리밍
- `PublishReadyToRun=true`: `RuntimeIdentifier`와 함께 사용 시 네이티브 코드가 포함되어 JIT 컴파일 부담 감소, 시작 시간 단축.
- `PublishTrimmed=true`: 사용되지 않는 런타임 코드 제거로 메모리 사용량 최적화, 간접적인 성능 향상.

#### (4) .NET 9의 새로운 기능
- **TieredPGO**: 특정 RID에 맞춘 동적 최적화로 성능 향상.
- **AppHostDotNetSearch**: `RuntimeIdentifier` 지정 시 런타임 검색 오버헤드 감소.

### 언제 성능이 더 좋아질까?
`RuntimeIdentifier`를 지정하면 다음과 같은 경우 성능이 향상됩니다:
- **플랫폼별 최적화가 중요한 경우**: 서버 애플리케이션, CLI 도구, 데스크톱 앱에서 특정 OS/CPU 아키텍처(예: `linux-x64` on ARM)에 맞춘 최적화 필요.
- **시작 시간 최적화**: `PublishReadyToRun=true`로 시작 시간 단축.
- **독립 실행 환경**: .NET 런타임이 없는 환경에서 Self-contained 배포 필요.
- **PGO 활용**: `TieredPGO=true`로 런타임 성능 극대화.

### `RuntimeIdentifier`를 지정하지 않을 때
- **장점**: 배포 크기 작음, 여러 플랫폼에서 동일 빌드 사용 가능.
- **단점**: 플랫폼별 최적화 제한, 런타임 설치 필요.
- **적합한 경우**: 배포 크기 최소화, .NET 런타임이 설치된 환경.

### 권장 사항
- **성능 우선**: `RuntimeIdentifier`를 지정하여 Self-contained 배포 사용. `PublishReadyToRun=true`, `TieredPGO=true`, `PublishTrimmed=true`와 함께 설정 시 시작 시간과 런타임 성능 최적화.
  - 예: `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` 또는 `dotnet publish -r win-x64`.
- **배포 크기 우선**: Framework-dependent 배포를 위해 `RuntimeIdentifier` 미지정, 단 런타임 설치 확인.
- **테스트**: `BenchmarkDotNet`으로 성능 비교(예: `dotnet publish -r win-x64` vs. Framework-dependent).

## 사용 방법

### 1. 프로필 생성 및 적용
- **Visual Studio**: 프로젝트 우클릭 > Publish > Folder (또는 Azure 등) > Settings > Save as publish profile.
- **파일 수정**: `Properties\PublishProfiles\Production.pubxml`에 위 내용을 붙여넣기.
- **CLI 명령어**:
  ```bash
  dotnet publish -c Release /p:PublishProfile=Production
  ```
  - `DeployOnBuild=true`가 프로필에 포함되어 있으므로 별도 지정 불필요.
  - Self-contained 배포 시 RID 명시: `dotnet publish -r win-x64`.

### 2. DeployOnBuild 옵션
- **프로필에 포함**: `<DeployOnBuild>true</DeployOnBuild>`를 `.pubxml`에 추가하면, Visual Studio나 CLI에서 프로필 사용 시 자동 배포.
- **명령줄에서 지정**: 프로필에 포함하지 않고, 필요 시 `dotnet publish /p:DeployOnBuild=true`로 지정.
  - 예: `dotnet publish -c Release /p:PublishProfile=Production /p:DeployOnBuild=true`

### 3. 컨테이너 배포
- `Microsoft.NET.Build.Containers` NuGet 패키지 설치.
- 명령어: `dotnet publish -p:PublishProfile=DefaultContainer`.

### 4. 디버깅
- 빌드/배포 문제 발생 시: `dotnet publish --verbosity detailed`로 로그 확인.

## 추가 참고
- **TieredPGO**: .NET 9에서 동적 PGO를 활성화하여 런타임 성능을 10-20% 향상. 별도 프로파일링 없이 작동.
- **AppHostDotNetSearch**: .NET 9에서 런타임 검색 경로 설정 (Global 또는 Relative).
- **CI/CD**: 파이프라인에서 `DeployOnBuild=true`를 명시적으로 지정하거나 프로필에 포함.