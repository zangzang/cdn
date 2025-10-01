# .NET Framework 4.8 빌드 및 배포를 위한 Publish Profile 가이드

이 문서는 .NET Framework 4.8을 사용한 빌드 및 배포를 위한 Publish Profile(.pubxml) 설정 방법을 설명합니다. Publish Profile은 `Properties\PublishProfiles` 폴더에 저장되며, Visual Studio 또는 MSBuild 명령어로 사용됩니다. 또한, Jenkins의 Generic Webhook Trigger Plugin을 사용한 CI/CD 파이프라인 트리거 예제(심플 버전 및 디테일 버전)를 포함합니다.

## Publish Profile 예제 (Production.pubxml)

다음은 ASP.NET 웹 애플리케이션 또는 Windows 애플리케이션을 위한 `.pubxml` 파일 예제입니다. 파일 시스템 배포와 MSDeploy를 지원하며, .NET Framework 4.8의 주요 속성을 포함합니다.

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
    <WebPublishMethod>FileSystem</WebPublishMethod>
    <PublishProtocol>FileSystem</PublishProtocol>
    <LastUsedBuildConfiguration>Release</LastUsedBuildConfiguration>
    <LastUsedPlatform>Any CPU</LastUsedPlatform>
    
    <!-- 배포 옵션 -->
    <DeployOnBuild>true</DeployOnBuild>
    <DeleteExistingFiles>true</DeleteExistingFiles>
    <ExcludeApp_Data>false</ExcludeApp_Data>
    <PrecompileBeforePublish>true</PrecompileBeforePublish>
    <EnableUpdateable>false</EnableUpdateable>
    <UseMerge>true</UseMerge>
    <SingleAssemblyName>YourAppName</SingleAssemblyName>
    
    <!-- 기타 옵션 -->
    <LaunchSiteAfterPublish>true</LaunchSiteAfterPublish>
    <ExcludeGeneratedDebugSymbols>true</ExcludeGeneratedDebugSymbols>
    <NoWarn>1591</NoWarn>
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
- **지원 여부**: .NET Framework 4.8은 `RuntimeIdentifier`를 지원하지 않습니다. 이는 .NET Core/5+에서 도입된 개념입니다.
- **대신 사용되는 설정**: `Platform` (예: `Any CPU`, `x86`, `x64`)으로 CPU 아키텍처 지정.
- **성능 영향**: `x64`로 설정 시 64비트 최적화 가능, 하지만 `Any CPU`가 더 유연하며 성능 차이는 미미.

## 사용 방법

### 1. 프로필 생성 및 적용
- **Visual Studio**: 프로젝트 우클릭 > Publish > New Profile > FileSystem 또는 MSDeploy 선택.
- **파일 수정**: `Properties\PublishProfiles\Production.pubxml`에 위 내용을 붙여넣기.
- **MSBuild 명령어**:
  ```bash
  msbuild YourProject.csproj /p:Configuration=Release /p:PublishProfile=Production /p:DeployOnBuild=true
  ```

### 2. DeployOnBuild 옵션
- **프로필에 포함**: `<DeployOnBuild>true</DeployOnBuild>`로 자동 배포.
- **명령줄에서 지정**:
  ```bash
  msbuild YourProject.csproj /p:Configuration=Release /p:PublishProfile=Production /p:DeployOnBuild=true
  ```

### 3. MSDeploy 사용
- `<WebPublishMethod>MSDeploy</WebPublishMethod>`와 `MSDeployServiceURL`, `DeployIisAppPath` 등을 설정.
- IIS 서버에 Web Deploy 설치 필요.

### 4. 디버깅
- 문제 발생 시: `msbuild /v:diag`로 로그 확인.

## Jenkins Pipeline 예제

아래는 .NET Framework 4.8 프로젝트를 위한 Jenkinsfile 예제입니다. Windows 에이전트와 MSBuild 설치 필요.

### 심플 버전 (Jenkinsfile)
소스 체크아웃, 빌드, 퍼블리시 수행.

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
NuGet 복원, 테스트, 아티팩트 저장, 에러 처리 포함.

```groovy
pipeline {
    agent { label 'windows' }
    environment {
        MSBUILD_PATH = 'C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\BuildTools\\MSBuild\\Current\\Bin\\msbuild.exe'
        PUBLISH_DIR = 'bin\\publish\\'
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
                bat "\"${MSBUILD_PATH}\" YourProject.csproj /p:Configuration=Release /t:VSTest"
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
            cleanWs()
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

## Jenkins Generic Webhook Trigger Plugin 설정

Generic Webhook Trigger Plugin은 GitHub, Bitbucket 등의 웹훅으로 Pipeline을 트리거합니다. .NET Framework 4.8 프로젝트에 맞춘 설정입니다.

### 공통 설정
- **Webhook URL**: `http://your-jenkins-url/job/YourJobName/generic-webhook-trigger/invoke?token=YOUR_TOKEN`
- **설치**: Jenkins > Manage Plugins > Generic Webhook Trigger Plugin 설치.
- **테스트**: `curl -X POST -H "Content-Type: application/json" -d '{"ref":"refs/heads/main"}' http://jenkins-url/job/YourJob/generic-webhook-trigger/invoke?token=TOKEN`

### 심플한 방법: 토큰만 사용
토큰 인증만으로 트리거. 모든 웹훅 요청 처리.

#### Job 설정
1. Jenkins Job > Configure > **Build Triggers** > "Generic Webhook Trigger" 체크.
2. **Token**: `my-simple-token` 입력.
3. **Optional Filter**: 비활성화.
4. **Variables**: 비활성화.
5. Webhook URL: `http://jenkins-url/job/YourJob/generic-webhook-trigger/invoke?token=my-simple-token`

#### Jenkinsfile
```groovy
pipeline {
    agent any
    environment {
        BUILD_ENV = 'production'
    }
    stages {
        stage('Checkout') {
            steps {
                echo 'Webhook triggered by simple token!'
                checkout scm
            }
        }
        stage('Build') {
            steps {
                echo "Building in ${BUILD_ENV} environment..."
                bat 'msbuild YourProject.csproj /p:Configuration=Release'
            }
        }
        stage('Test') {
            steps {
                echo 'Running tests...'
                bat 'msbuild YourProject.csproj /p:Configuration=Release /t:VSTest'
            }
        }
        stage('Deploy') {
            steps {
                echo 'Deploying...'
                bat 'msbuild YourProject.csproj /p:Configuration=Release /p:PublishProfile=Production /p:DeployOnBuild=true'
            }
        }
    }
    post {
        always {
            echo 'Pipeline completed!'
            cleanWs()
        }
        success {
            echo 'Build succeeded!'
        }
        failure {
            echo 'Build failed!'
        }
    }
}
```

#### 사용 예시
- **GitHub 설정**: Webhooks > Payload URL 입력, Content type: `application/json`.
- **트리거**: 모든 푸시/PR 이벤트에서 토큰이 맞으면 빌드 시작.
- **장점**: 설정 간단.
- **단점**: 조건 없이 모든 요청 트리거.

### 디테일한 방법: 토큰 + JSONPath Expression
토큰 인증 후 JSON 페이로드에서 변수 추출, 특정 브랜치(예: `main`, `develop`)에서만 트리거.

#### Job 설정
1. Jenkins Job > Configure > **Build Triggers** > "Generic Webhook Trigger" 체크.
2. **Token**: `my-detailed-token` 입력.
3. **JSONPath Expressions**:
   - Variable: `branch` | Expression: `$.ref`
   - Variable: `commit_sha` | Expression: `$.after`
4. **Optional Filter**:
   - Text: `${branch}`
   - RegEx: `refs/heads/(main|develop)`
5. **Cause**: "Webhook: ${branch}"
6. Webhook URL: `http://jenkins-url/job/YourJob/generic-webhook-trigger/invoke?token=my-detailed-token`

#### Jenkinsfile
```groovy
pipeline {
    agent { label 'windows' }
    parameters {
        string(name: 'BRANCH_NAME', defaultValue: env.branch ?: 'default', description: 'Triggered branch')
        string(name: 'COMMIT_SHA', defaultValue: env.commit_sha ?: 'unknown', description: 'Commit SHA')
    }
    environment {
        MSBUILD_PATH = 'C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\BuildTools\\MSBuild\\Current\\Bin\\msbuild.exe'
        TARGET_BRANCH = "${params.BRANCH_NAME}"
        BUILD_ENV = "${params.BRANCH_NAME == 'refs/heads/main' ? 'production' : 'staging'}"
    }
    stages {
        stage('Validate Trigger') {
            steps {
                echo "Webhook triggered by branch: ${env.branch}, Commit: ${env.commit_sha}"
                echo "Target Environment: ${BUILD_ENV}"
            }
        }
        stage('Checkout') {
            steps {
                checkout scm
                echo "Checked out commit ${params.COMMIT_SHA}"
            }
        }
        stage('Restore NuGet') {
            steps {
                bat 'nuget restore YourProject.sln'
            }
        }
        stage('Build') {
            when {
                expression { params.BRANCH_NAME == 'refs/heads/main' }
            }
            steps {
                echo "Building in ${BUILD_ENV}..."
                bat "\"${MSBUILD_PATH}\" YourProject.csproj /p:Configuration=Release /t:Build"
            }
        }
        stage('Test') {
            steps {
                echo 'Running tests...'
                bat "\"${MSBUILD_PATH}\" YourProject.csproj /p:Configuration=Release /t:VSTest"
            }
        }
        stage('Security Scan') {
            when {
                expression { params.BRANCH_NAME == 'refs/heads/develop' }
            }
            steps {
                echo 'Running security scan (e.g., SonarQube)...'
                bat 'sonar-scanner -Dsonar.projectKey=MyProject -Dsonar.host.url=http://sonar-url'
            }
        }
        stage('Deploy') {
            steps {
                echo "Deploying to ${BUILD_ENV}..."
                bat "\"${MSBUILD_PATH}\" YourProject.csproj /p:Configuration=Release /p:PublishProfile=Production /p:DeployOnBuild=true"
                archiveArtifacts artifacts: 'bin\\publish\\**', fingerprint: true
            }
        }
    }
    post {
        always {
            echo 'Pipeline completed! Branch: ${params.BRANCH_NAME}'
            cleanWs()
        }
        success {
            echo 'Build succeeded! Ready for ${BUILD_ENV} deployment.'
            emailext to: 'team@example.com', subject: "Success: ${JOB_NAME} #${BUILD_NUMBER}", body: "Branch: ${params.BRANCH_NAME}"
        }
        failure {
            echo 'Build failed! Check logs for ${params.BRANCH_NAME}.'
            emailext to: 'team@example.com', subject: "Failure: ${JOB_NAME} #${BUILD_NUMBER}", body: "Branch: ${params.BRANCH_NAME}"
        }
    }
}
```

#### 사용 예시
- **GitHub 설정**: Webhooks > Pushes 선택, Payload URL 입력.
- **트리거**: `main` 또는 `develop` 브랜치 푸시 시 실행 (e.g., `{"ref": "refs/heads/main", "after": "abc123"}`).
- **장점**: 조건 필터링, 동적 로직 구현.
- **단점**: JSONPath 설정 복잡.

## 추가 참고
- **사전 컴파일**: `<PrecompileBeforePublish>true</PrecompileBeforePublish>`로 런타임 컴파일 오버헤드 감소.
- **어셈블리 병합**: `<UseMerge>true</UseMerge>`로 배포 크기 및 로드 시간 최적화.
- **제약 사항**: .NET Framework 4.8은 Windows 전용, 최신 최적화 기능 미지원.
- **Jenkins 플러그인**: MSBuild, Email Extension, SonarQube Scanner 설치 권장.
- **디버깅**: Jenkins 로그에서 "Generic Webhook Trigger" 이벤트 확인.