# HttpClientHelper 프로젝트

Flurl.Http를 사용한 HTTP 요청 헬퍼 라이브러리 및 Web API 프로젝트입니다.

## 프로젝트 구조

```
cdn/
├── HttpHelper/              # HTTP 클라이언트 헬퍼 라이브러리
│   ├── FlurlHttpHelper.cs  # Flurl.Http 래퍼 클래스
│   └── HttpClientHelper.csproj
├── WebApi/                  # ASP.NET Core Web API
│   ├── Controllers/
│   ├── Services/
│   ├── Models/
│   └── WebApi.csproj
└── Tests/                   # 테스트 프로젝트
    ├── FlurlHttpHelperTests.cs
    ├── IntegrationTests.cs
    ├── EmailServiceTests.cs
    └── Tests.csproj
```

## 기술 스택

- **.NET 8.0**
- **Flurl.Http 4.0.2** - HTTP 클라이언트 라이브러리
- **xUnit** - 테스트 프레임워크
- **FluentAssertions** - 테스트 검증 라이브러리
- **Moq** - Mocking 라이브러리

## 빌드 및 실행

### 전체 솔루션 빌드
```powershell
dotnet build cdn.sln
```

### 특정 프로젝트 빌드
```powershell
# HttpClientHelper 라이브러리
dotnet build HttpHelper/HttpClientHelper.csproj

# WebApi 프로젝트
dotnet build WebApi/WebApi.csproj

# Tests 프로젝트
dotnet build Tests/Tests.csproj
```

### WebApi 실행
```powershell
dotnet run --project WebApi/WebApi.csproj
```

## 테스트 실행

### 1. 전체 테스트 실행
```powershell
dotnet test Tests/Tests.csproj
```

### 2. 상세 로그와 함께 실행
```powershell
dotnet test Tests/Tests.csproj --verbosity normal
```

### 3. 특정 테스트 클래스만 실행

#### FlurlHttpHelper 단위 테스트 (13개)
```powershell
dotnet test --filter "FullyQualifiedName~FlurlHttpHelperTests"
```

#### 통합 테스트 - API 체인 호출 (6개)
```powershell
dotnet test --filter "FullyQualifiedName~IntegrationTests"
```

#### EmailService 테스트 (8개)
```powershell
dotnet test --filter "FullyQualifiedName~EmailServiceTests"
```

### 4. 특정 테스트 메서드만 실행
```powershell
# POST 요청 테스트
dotnet test --filter "FullyQualifiedName~PostAsync_ShouldSendJsonDataAndReturnResponse"

# 파일 업로드 테스트
dotnet test --filter "FullyQualifiedName~PostWithFileAsync_WithStream_ShouldUploadFile"

# API 체인 테스트
dotnet test --filter "FullyQualifiedName~ApiChain_FirstApiCallsSecondApi_ShouldSucceed"
```

### 5. 테스트 커버리지 수집
```powershell
dotnet test --collect:"XPlat Code Coverage"
```

### 6. 테스트 결과를 파일로 출력
```powershell
dotnet test --logger "trx;LogFileName=test-results.trx"
```

### 7. 실패한 테스트만 재실행
```powershell
dotnet test --filter "TestCategory=Failed"
```

## 테스트 구성

### FlurlHttpHelperTests (13개 테스트)
- **기본 초기화 테스트** (2개)
  - 기본 URL로 초기화
  - 커스텀 타임아웃으로 초기화

- **POST 요청 테스트** (3개)
  - JSON 데이터 전송
  - Form 데이터 전송
  - 문자열 응답 처리

- **GET 요청 테스트** (1개)
  - 데이터 조회 및 헤더 검증

- **파일 업로드 테스트** (2개)
  - 단일 파일 업로드 (Stream)
  - 다중 파일 업로드 (FileUploadInfo)

- **FileUploadInfo 테스트** (3개)
  - Stream 생성자 초기화
  - 파일 미존재 예외 처리
  - Stream Dispose

- **ApiResponse 테스트** (2개)
  - 기본값 검증
  - 속성 설정 검증

### IntegrationTests (6개 테스트)
API가 다른 API를 호출하는 통합 시나리오 테스트

1. **ApiChain_FirstApiCallsSecondApi_ShouldSucceed**
   - 첫 번째 API가 두 번째 API를 순차적으로 호출

2. **EmailApiScenario_SendEmailWithExternalValidation_ShouldSucceed**
   - 이메일 검증 API → 이메일 전송 API 체인

3. **FileUploadChain_UploadToStorageThenNotifyApi_ShouldSucceed**
   - 파일 업로드 → 알림 API 호출

4. **MultipleFilesWithMetadata_UploadAndProcess_ShouldSucceed**
   - 다중 파일 업로드 → 처리 작업 시작

5. **GetAfterPost_CreateResourceThenRetrieve_ShouldSucceed**
   - POST로 리소스 생성 → GET으로 조회

6. **ErrorHandling_FirstApiFailsSecondApiSkipped_ShouldHandleGracefully**
   - 첫 API 실패 시 두 번째 API 호출 스킵 (예외 처리)

### EmailServiceTests (8개 테스트)
EmailService 비즈니스 로직 테스트
- 이메일 전송 요청 모델 검증
- 이메일 전송 응답 모델 검증
- 이메일 상태 조회 모델 검증

## HttpClientHelper.Core 주요 기능

### FlurlHttpHelper
HTTP 요청을 간편하게 처리하는 헬퍼 클래스

#### POST 요청
```csharp
var helper = new FlurlHttpHelper("https://api.example.com");

// JSON 전송
var response = await helper.PostAsync<ApiResponse<string>>(
    "/api/endpoint",
    new { data = "test" },
    headers: new Dictionary<string, string> { { "Authorization", "Bearer token" } },
    queryParams: new Dictionary<string, object> { { "param", "value" } }
);

// Form 데이터 전송
var formResponse = await helper.PostFormAsync<ApiResponse<bool>>(
    "/api/form",
    new Dictionary<string, string> { { "key", "value" } }
);
```

#### GET 요청
```csharp
var response = await helper.GetAsync<ApiResponse<List<string>>>(
    "/api/items",
    headers: new Dictionary<string, string> { { "Authorization", "Bearer token" } }
);
```

#### 파일 업로드
```csharp
// 단일 파일
using var stream = File.OpenRead("file.txt");
var response = await helper.PostWithFileAsync<ApiResponse<string>>(
    "/api/upload",
    stream,
    "file.txt",
    "file"
);

// 다중 파일
var files = new List<FileUploadInfo>
{
    new FileUploadInfo("files", stream1, "file1.txt"),
    new FileUploadInfo("files", stream2, "file2.txt")
};

var response = await helper.PostWithFilesAsync<ApiResponse<int>>(
    "/api/upload-multiple",
    files,
    formData: new Dictionary<string, string> { { "description", "Test" } }
);
```

### ApiResponse<T>
표준 API 응답 모델

```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public int StatusCode { get; set; }
}
```

## CI/CD

### GitHub Actions (예시)
```yaml
name: .NET Test

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    - name: Restore dependencies
      run: dotnet restore
    - name: Build
      run: dotnet build --no-restore
    - name: Test
      run: dotnet test --no-build --verbosity normal
```

## 라이선스

MIT License
