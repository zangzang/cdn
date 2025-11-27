// Program.cs - .NET 8
using YourNamespace.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Controllers 추가
builder.Services.AddControllers();

// Swagger/OpenAPI 설정
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Email API",
        Version = "v1",
        Description = "이메일 전송 API (파일 첨부 지원)"
    });

    // XML 주석 활성화 (선택사항)
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // 파일 업로드 지원
    c.OperationFilter<SwaggerFileOperationFilter>();
});

// CORS 설정 (필요시)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 파일 업로드 크기 제한 설정
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; // 50MB
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52428800; // 50MB
});

// 서비스 등록
builder.Services.AddScoped<IEmailService, EmailService>();

// 로깅 설정
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Swagger UI (개발 환경)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Email API V1");
        c.RoutePrefix = string.Empty; // Swagger를 루트로 설정
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();

// Swagger 파일 업로드 필터
public class SwaggerFileOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var fileParameters = context.MethodInfo.GetParameters()
            .Where(p => p.ParameterType == typeof(IFormFile) || 
                       p.ParameterType == typeof(List<IFormFile>))
            .ToList();

        if (fileParameters.Any())
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["files"] = new OpenApiSchema
                                {
                                    Type = "array",
                                    Items = new OpenApiSchema
                                    {
                                        Type = "string",
                                        Format = "binary"
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}


/* ========================================
   appsettings.json
   ======================================== */

/*
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  
  "EmailApi": {
    "BaseUrl": "https://api.email-service.com",
    "ApiKey": "your-api-key-here",
    "ClientId": "your-client-id",
    "TimeoutSeconds": 60
  },
  
  "Kestrel": {
    "Limits": {
      "MaxRequestBodySize": 52428800
    }
  }
}
*/


/* ========================================
   appsettings.Development.json
   ======================================== */

/*
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  },
  
  "EmailApi": {
    "BaseUrl": "https://dev-api.email-service.com",
    "ApiKey": "dev-api-key",
    "ClientId": "dev-client",
    "TimeoutSeconds": 120
  }
}
*/