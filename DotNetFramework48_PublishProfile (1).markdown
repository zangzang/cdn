# .NET Framework 4.8 빌드 및 배포를 위한 Publish Profile 가이드

이 문서는 .NET Framework 4.8을 사용한 빌드 및 배포를 위한 Publish Profile(.pubxml) 설정 방법을 설명합니다. Publish Profile은 `Properties\PublishProfiles` 폴더에 저장되며, Visual Studio 또는 MSBuild 명령어로 사용됩니다. .NET Framework 4.8은 최신 .NET Core/5+ 기능(예: TieredPGO, PublishTrimmed, SelfContained)을 지원하지 않으므로, 이에 맞는 속성들로 구성된 예제와 사용 방법을 제공합니다. 추가로 Jenkins Pipeline 예제(심플 버전과 디테일 버전)를 포함합니다.

## Publish Profile 예제 (Production.pubxml)

다음은 ASP.NET 웹 애플리케이션 또는 Windows 애플리케이션을 위한 `.pubxml` 파일 예제입니다. 파일 시스템 배포와 MSDeploy(Web Deploy)를 지원하며, .NET Framework 4.8의 주요 배포 속성을 포함합니다.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <!-- 기본 빌드 설정 -->
    <Configuration>Release</Configuration>
    <Platform>Any CPU</Platform>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    
    <!-- 출력 디렉토리 -->
    <PublishUrl>bin\publish\</PublishUrl>
    <PublishDir>bin\publish\</PublishDir>
    
    <!-- 배포 프로토콜 및 메서드 -->
    <WebPublishMethod>FileSystem</WebPublishMethod> <!-- 또는 MSDeploy -->
    <PublishProtocol>FileSystem</PublishProtocol>
    <LastUsedBuildConfiguration>Release</LastUsedBuildConfiguration>
    <LastUsedPlatform>Any CPU</LastUsedPlatform>
    
    <!-- MSDeploy 설정 (FileSystem 대신 MSDeploy 사용 시) -->
    <!--
    <WebPublishMethod>MSDeploy</WebPublishMethod>
    <MSDeployServiceURL>https://your-server:8172/MsDeploy.axd</MSDeployServiceURL>
    <DeployIisAppPath>YourSiteName</DeployIisAppPath>
    <UserName>YourUsername</UserName>
    <Password>YourPassword</Password>
    <AllowUntrustedCertificate>true</AllowUntrustedCertificate>
    -->
    
    <!-- 배포 옵션 -->
    <DeployOnBuild>true</DeployOnBuild>
    <DeleteExistingFiles>true</DeleteExistingFiles> <!-- 기존 파일 삭제 -->
    <ExcludeApp_Data>false</ExcludeApp_Data> <!-- App_Data 폴더 포함 -->
    <PrecompileBeforePublish>true</PrecompileBeforePublish> <!-- 웹 앱 사전 컴파일 -->
    <EnableUpdateable>false</EnableUpdateable> <!-- 업데이트 가능 여부 -->
    <UseMerge>true</UseMerge> <!-- 어셈블리 병합 -->
    <SingleAssemblyName>YourAppName</SingleAssemblyName> <!-- 병합된 어셈블리 이름 -->
    
    <!-- 기타 옵션 -->
    <LaunchSiteAfterPublish>true</LaunchSiteAfterPublish> <!-- 배포 후 사이트 실행 -->
    <ExcludeGeneratedDebugSymbols>true</ExcludeGeneratedDebugSymbols> <!-- 디버그 심볼 제외 -->
    <NoWarn>1591</NoWarn> <!-- 경고 억제 (예: XML 주석 경고) -->
  </PropertyGroup>
  
  <!-- 포함/제외 파일 설정 -->
  <ItemGroup>
    <Content Include="**\*.config" CopyToPublishDirectory="PreserveNewest" />
    <Content Include="bin\**\*.dll" CopyToPublishDirectory="PreserveNewest" />
    <Content Include="wwwroot\**\*.*" CopyToPublishDirectory="PreserveNewest" />
    <Content Update="App_Data\**\*.*" CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

## 주요 속성 설명

| 속성                       | 설명                                                                 | 기본값/예시 값          |
|----------------------------|---------------------------------------------------------------------|-------------------------|
| Configuration            | 빌드 구성 (Debug/Release).                                         | Release                |
| Platform                 | 대상 플랫폼 (Any CPU, x86, x64).                                   | Any CPU                |
| TargetFrameworkVersion   | .NET Framework 버전.                                               | v4.8                   |
| PublishUrl               | 출력 URL (GUI에서 사용).                                           | bin\publish\           |
| PublishDir               | 출력 디렉토리 (CLI에서 사용).                                      | bin\publish\           |
| WebPublishMethod         | 배포 방식 (FileSystem, MSDeploy 등).                               | FileSystem             |
| DeployOnBuild            | 빌드 시 배포 자동 실행.                                            | true                   |
| DeleteExistingFiles      | 대상 디렉토리의 기존 파일 삭제.                                     | true                   |
| PrecompileBeforePublish  | 웹 앱 사전 컴파일 (ASP.NET에 유용).                                | true                   |
| UseMerge                 | 어셈블리 병합 활성화 (ILMerge와 유사).                             | true                   |
| SingleAssemblyName       | 병합된 어셈블리 이름.                                              | YourAppName            |

## `RuntimeIdentifier`와 .NET Framework 4.8
- **지원 여부**: .NET Framework 4.8은 `RuntimeIdentifier`를 지원하지 않습니다. 이는 .NET Core/5+에서 도입된 개념으로, Self-contained 배포와 플랫폼별 최적화(예: `win-x64`, `linux-x64`)를 위해 사용됩니다.
- **대신 사용되는 설정**:
  - `Platform` (예: `Any CPU`, `x86`, `x64`): 컴파일된 어셈블리의 대상 CPU 아키텍처를 지정.
  - .NET Framework 4.8은 Framework-dependent 배포만 지원하며, 대상 시스템에 .NET Framework 4.8이 설치되어 있어야 합니다.
- **성능 영향**:
  - `Platform`을 `x64`로 설정하면 64비트 전용 최적화가 가능하지만, `Any CPU`는 더 유연하며 대부분의 경우 성능 차이가 미미합니다.
  - .NET Framework 4.8은 .NET 9의 `TieredPGO`, `PublishReadyToRun`, `PublishTrimmed` 같은 최적화 옵션을 지원하지 않으므로, 성능 최적화는 주로 사전 컴파일(`PrecompileBeforePublish`)과 어셈블리 병합(`UseMerge`)에 의존합니다.

## 사용 방법

### 1. 프로필 생성 및 적용
- **Visual Studio**:
  1. 프로젝트 우클릭 > Publish > New Profile > FileSystem 또는 MSDeploy 선택.
  2. `Properties\PublishProfiles\Production.pubxml`에 위 내용을 붙여넣기.
- **MSBuild 명령어**:
  ```bash
  msbuild YourProject.csproj /p:Configuration=Release /p:PublishProfile=Production /p:DeployOnBuild=true
  ```
  - .NET Framework 4.8 프로젝트는 `dotnet publish` 대신 `msbuild`를 사용합니다.

### 2. DeployOnBuild 옵션
- **프로필에 포함**: `<DeployOnBuild>true</DeployOnBuild>`를 `.pubxml`에 추가하면, Visual Studio 또는 MSBuild에서 프로필 사용 시 자동 배포.
- **명령줄에서 지정**:
  ```bash
  msbuild YourProject.csproj /p:Configuration=Release /p:PublishProfile=Production /p:DeployOnBuild=true
  ```

### 3. MSDeploy 사용
- MSDeploy를 사용하려면 `<WebPublishMethod>MSDeploy</WebPublishMethod>`와 관련 속성(`MSDeployServiceURL`, `DeployIisAppPath` 등)을 설정하세요.
- 예: IIS 서버에 배포하려면, 서버에 Web Deploy가 설치되어 있어야 하며, 인증 정보와 경로를 정확히 지정해야 합니다.

### 4. 디버깅
- 빌드/배포 문제 발생 시: `msbuild /v:diag`로 상세 로그 확인.
- 예: `msbuild YourProject.csproj /p:Configuration=Release /p:PublishProfile=Production /v:diag`.

## Jenkins Pipeline 예제

Jenkins Pipeline은 CI/CD를 자동화하기 위해 사용됩니다. 아래는 .NET Framework 4.8 프로젝트를 위한 Groovy 기반 Jenkinsfile 예제입니다. Jenkins 에이전트에 MSBuild(Visual Studio Build Tools)가 설치되어 있어야 합니다. (Windows 에이전트 추천)

### 심플 버전 (Jenkinsfile)
이 버전은 기본적으로 소스 체크아웃, 빌드, 퍼블리시를 수행합니다.

```groovy
pipeline {
    agent any
    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }
        stage('Build') {
            steps {
                bat 'msbuild YourProject.csproj /p:Configuration=Release'
            }
        }
        stage('Publish') {
            steps {
                bat 'msbuild YourProject.csproj /p:Configuration=Release /p:PublishProfile=Production /p:DeployOnBuild=true'
            }
        }
    }
}
```

### 디테일 버전 (Jenkinsfile)
이 버전은 NuGet 복원, 테스트, 아티팩트 저장, 에러 처리, 환경 변수 설정을 추가합니다. MSDeploy를 위한 옵션을 포함합니다.

```groovy
pipeline {
    agent { label 'windows' }  // Windows 에이전트 사용
    environment {
        MSBUILD_PATH = 'C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\BuildTools\\MSBuild\\Current\\Bin\\msbuild.exe'  // MSBuild 경로
        PUBLISH_DIR = 'bin\\publish\\'  // 출력 디렉토리
    }
    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }
        stage('Restore NuGet') {
            steps {
                bat 'nuget restore YourProject.sln'
            }
        }
        stage('Build') {
            steps {
                bat "\"${MSBUILD_PATH}\" YourProject.csproj /p:Configuration=Release /t:Build"
            }
        }
        stage('Test') {
            steps {
                bat "\"${MSBUILD_PATH}\" YourProject.csproj /p:Configuration=Release /t:VSTest"  // MSTest 또는 NUnit 사용 시 조정
            }
        }
        stage('Publish') {
            steps {
                bat "\"${MSBUILD_PATH}\" YourProject.csproj /p:Configuration=Release /p:PublishProfile=Production /p:DeployOnBuild=true /p:WebPublishMethod=FileSystem /p:PublishUrl=${PUBLISH_DIR}"
            }
        }
        stage('Archive Artifacts') {
            steps {
                archiveArtifacts artifacts: "${PUBLISH_DIR}**/*", fingerprint: true
            }
        }
    }
    post {
        always {
            cleanWs()  // 워크스페이스 정리
        }
        success {
            echo 'Build and Publish succeeded!'
        }
        failure {
            echo 'Build failed! Check logs.'
        }
    }
}
```

## 추가 참고
- **사전 컴파일**: ASP.NET 웹 애플리케이션의 경우 `<PrecompileBeforePublish>true</PrecompileBeforePublish>`를 사용하여 런타임 컴파일 오버헤드를 줄일 수 있습니다.
- **어셈블리 병합**: `<UseMerge>true</UseMerge>`는 다수의 DLL을 단일 어셈블리로 병합하여 배포 크기를 줄이고 로드 시간을 단축할 수 있습니다.
- **제약 사항**: .NET Framework 4.8은 .NET 9의 최신 최적화 기능(TieredPGO, ReadyToRun, 트리밍)을 지원하지 않으므로, 성능 최적화는 제한적입니다.
- **플랫폼 호환성**: 대상 시스템에 .NET Framework 4.8이 설치되어 있어야 하며, Windows 환경에서만 실행 가능합니다.
- **Jenkins 플러그인**: MSBuild 플러그인을 설치하면 더 쉽게 MSBuild 스텝을 사용할 수 있습니다.