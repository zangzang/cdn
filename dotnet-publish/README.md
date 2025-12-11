# Donet Publish & Luanch


## Publish

- 명령줄 옵션

```shell
# 기본 단일 파일
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 단일 파일 + 모든 것 포함 (Native 라이브러리도 포함)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# 단일 파일 + Trimming (용량 최소화)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true

# Linux
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true

# macOS
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true
```

### 프로필

#### 파일 생성

- SingleFile-Windows.pubxml:

```xml
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<PublishDir>bin\publish\windows\</PublishDir>
```

- SingleFile-Linux.pubxml:

```xml
<RuntimeIdentifier>linux-x64</RuntimeIdentifier>
<PublishDir>bin\publish\linux\</PublishDir>
```

- 단일파일 + Runtime필요 + 원도우64용

```xml
<PropertyGroup>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>false</SelfContained>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>
```

```shell
# Self-contained (런타임 포함)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
# → 약 60-150MB

# Framework-dependent (런타임 미포함) - 당신이 원하는 것!
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
# → 약 5-15MB
```


#### 실행

```shell
dotnet publish /p:PublishProfile=SingleFile-Windows
dotnet publish /p:PublishProfile=SingleFile-Linux
```



### ReadyToRun

```xml
<!-- 시작 속도가 중요한 경우 (CLI 도구, 짧은 실행 앱) -->
<PublishReadyToRun>true</PublishReadyToRun>

<!-- 파일 크기가 중요하거나 장시간 실행되는 앱 -->
<PublishReadyToRun>false</PublishReadyToRun>
```


## launchSettings.json

- 파일위치

```
프로젝트루트/
└── Properties/
    └── launchSettings.json
```

- 설명

```json
{
  "commandName": "Project",           // Project, Executable, Docker, WSL2
  "commandLineArgs": "arg1 arg2",     // 명령줄 인수
  "workingDirectory": "path",         // 작업 디렉토리
  "dotnetRunMessages": true,          // dotnet run 메시지 표시
  "launchBrowser": false,             // 브라우저 실행 (웹앱용)
  "environmentVariables": {           // 환경변수
    "KEY": "VALUE"
  }
}
```

- VSCode용 [launch.json]

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Core Launch (console)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/bin/Debug/net9.0/ConsoleApp.dll",
      "args": ["arg1", "arg2"],
      "cwd": "${workspaceFolder}",
      "console": "internalConsole",
      "stopAtEntry": false,
      "env": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  ]
}
```
