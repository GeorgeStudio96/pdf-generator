# Architecture Roadmap: Preparing for Scale

Your project is **well-architected for growth**. Here's what's working and what to prepare for as you add major features.

---

## ✅ What's Already Right

### 1. **Async Job Processing** (Perfect Foundation)
- Redis-based job queue with Sorted Sets
- Background processor with concurrency control
- Horizontal scalability: Deploy multiple instances
- Already decouples long-running AI calls from HTTP requests

**Status**: Production-ready. No changes needed.

### 2. **Repository Pattern** (Easy to Extend)
- `IJobRepository` abstraction allows swapping Redis → SQL/Cosmos later
- Current Redis implementation is solid
- Business logic in `IJobService` stays untouched when storage changes

**Status**: Scalable. Ready for new repository implementations.

### 3. **Minimal APIs** (Lightweight, Fast)
- No controller bloat
- Direct route → endpoint mapping
- Easy to add new endpoints without boilerplate

**Status**: Keeps codebase lean as it grows.

### 4. **Dependency Injection** (Proper Setup)
- Built-in ASP.NET Core container
- Service scopes for background jobs
- Easy to swap implementations for testing

**Status**: Supports growth without refactoring.

### 5. **Security** (Baseline Ready)
- X-Internal-Key authentication middleware
- DTO-based API (prevents entity leakage)
- Nullable reference types enabled

**Status**: Add JWT/OAuth as you expose to external clients.

---

## 🟡 Prepare Now for Future Features

### 1. **Feature Organization** (Vertical Slices)

**Current**: Files scattered (Components, Services, Models)

**Action**: As new features arrive, organize by feature:
```
src/
├── Features/
│   ├── JobProcessing/
│   │   ├── Endpoints.cs      (API routes)
│   │   ├── JobService.cs
│   │   ├── JobRepository.cs
│   │   └── Models.cs         (DTOs, entities)
│   ├── Reports/              (New feature)
│   │   ├── Endpoints.cs
│   │   ├── ReportService.cs
│   │   └── Models.cs
│   └── Authentication/       (Future)
└── Shared/                   (Cross-feature utilities)
    ├── Database/             (DbContext, migrations)
    ├── Services/             (AI, cache, email)
    └── Extensions/
```

**Why**: All context for "JobProcessing" in one folder. Scales cleanly.

**Timeline**: Start when you add the next feature. Don't refactor existing code unless necessary (YAGNI).

### 2. **Database Layer** (If Adding Persistent Storage)

**Current**: Redis only (perfect for job queue)

**Future Consideration**: If you need SQL (user profiles, audit logs, etc.)

```csharp
// When needed:
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(configuration["ConnectionStrings:Default"]));

// Place in Shared/Database/
public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
}
```

**Don't add until you need it.** Redis is sufficient for current scope.

### 3. **Caching Layer** (Optimize as You Scale)

**Current**: None (not needed yet)

**Pattern when needed** (Cache-Aside):
```csharp
public async Task<JobResponse?> GetJobWithCacheAsync(string id)
{
    var key = $"job:{id}";
    var cached = await _cache.GetStringAsync(key);
    if (!string.IsNullOrEmpty(cached))
        return JsonSerializer.Deserialize<JobResponse>(cached);

    var job = await _repository.GetAsync(id);
    if (job != null)
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(job),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });
    return job;
}
```

**Timeline**: Add when response times exceed acceptable limits, not before.

### 4. **Logging & Observability** (Critical for Production Scale)

**Current**: Basic console logging

**Upgrade when deploying to production**:
```csharp
// Add Serilog for structured logging
builder.Host.UseSerilog((ctx, config) => config
    .WriteTo.Console()
    .WriteTo.File("logs/app.log", rollingInterval: RollingInterval.Day)
    .MinimumLevel.Information());

// Use contextual logging
_logger.LogInformation("Processing job {JobId} with {ItemCount} items", jobId, itemCount);
```

**Why**: Structured logs are searchable and aggregatable in production.

### 5. **Error Handling Strategy** (Centralize Gradually)

**Current**: Basic null checks and status codes

**Enhance with**:
```csharp
// Global exception handler middleware
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger>();

        logger.LogError(exception, "Unhandled exception");

        var response = app.Environment.IsDevelopment()
            ? new { error = exception?.Message }
            : new { error = "Internal server error" };

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    });
});

// Custom exception types
public class JobProcessingException : Exception
{
    public string JobId { get; }
    public JobProcessingException(string jobId, string message)
        : base(message) => JobId = jobId;
}
```

**Timeline**: Add as you encounter production error scenarios.

### 6. **Validation & Input Sanitization** (Add FluentValidation)

**Current**: Manual null checks

**Future**: Centralized validation
```csharp
// Install: FluentValidation
public class CreateJobValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobValidator()
    {
        RuleFor(x => x.ProjectName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Budget).GreaterThan(0).LessThan(1_000_000);
        RuleFor(x => x.Deadline).GreaterThan(DateTime.UtcNow);
    }
}

// Register in Program.cs
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// In endpoint
app.MapPost("/jobs", CreateJob)
   .WithValidator<CreateJobRequest>();
```

**Timeline**: Add when you have multiple endpoints with complex validation.

---

## 🚀 Scaling Strategies

### 1. **Horizontal Job Processing**
Already set up! Just deploy multiple instances:

```yaml
# docker-compose.yml - future enhancement
services:
  processor-1:
    image: pdf-service
    environment:
      PROCESSOR_ID: processor-1

  processor-2:
    image: pdf-service
    environment:
      PROCESSOR_ID: processor-2

  redis:
    image: redis:7
```

Redis Sorted Sets handle distribution automatically.

### 2. **Service-to-Service Communication**
If you split into microservices later:

```csharp
// Use HttpClientFactory for service calls
builder.Services.AddHttpClient("JobService", client =>
{
    client.BaseAddress = new Uri("http://localhost:5001");
});

var client = _clientFactory.CreateClient("JobService");
var response = await client.GetAsync($"/jobs/{id}");
```

**Timeline**: Only when monolith becomes problematic (not yet).

### 3. **Database Optimization**
Prepare queries for scale:

```csharp
// AsNoTracking for read-only queries
var jobs = await _db.Jobs.AsNoTracking().ToListAsync();

// Pagination from day one
var page = 1;
var pageSize = 20;
var jobs = await _db.Jobs
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();

// Indexes on frequently queried fields (migrations)
modelBuilder.Entity<Job>()
    .HasIndex(j => j.CreatedAt)
    .IsUnique(false);
```

**Timeline**: Before you hit production scale (1000+ jobs/day).

---

## 📋 Implementation Checklist for New Features

Use this when adding major features:

- [ ] **Architecture**: Use `@dotnet-architect` agent to design first
- [ ] **Data Model**: Define entities/DTOs
- [ ] **Service Layer**: Implement business logic with interfaces
- [ ] **API Endpoints**: Create minimal API routes
- [ ] **Tests**: Unit tests (services), integration tests (endpoints)
- [ ] **Security**: Auth check, input validation, DTO usage
- [ ] **Error Handling**: Graceful failures, logging
- [ ] **Performance**: Query optimization, caching if needed
- [ ] **Documentation**: API docs, README updates
- [ ] **Code Review**: Use `@dotnet-review` agent before merge

---

## 🛠 Tool Selection for Growing Codebase

| Need | Library | When |
|------|---------|------|
| **Logging** | Serilog | Production deployment |
| **Validation** | FluentValidation | 3+ endpoints with complex rules |
| **ORM** | EF Core | When SQL storage needed |
| **Caching** | StackExchange.Redis (have it) | Performance bottlenecks |
| **Testing** | xUnit + Moq | When features get complex |
| **API Docs** | Swagger/NSwag | External API documentation |
| **Background Jobs** | (Current approach) | Keep Redis for simple queues |

**Rule**: Add only when you have a real problem (YAGNI).

---

## ✨ Senior Development Practices (Now)

These don't require new tools:

1. **Code Review Every PR** → Use `@dotnet-review` agent
2. **Write Tests Alongside Code** → Use `@dotnet-test` agent
3. **Refactor When Patterns Emerge** → Use `@dotnet-refactor` agent
4. **Plan Before Building** → Use `@dotnet-architect` agent
5. **Keep Code Simple** → Review for YAGNI violations

---

## 📦 Current Tech Stack (Stable)

- **.NET**: 9.0 (LTS)
- **API**: ASP.NET Core Minimal APIs
- **DI**: Built-in container
- **Data Access**: EF Core (when needed) + Redis
- **Background Jobs**: BackgroundService + Redis Sorted Sets
- **AI**: Anthropic Claude API
- **PDF**: QuestPDF
- **Testing**: xUnit (recommended) or MSTest
- **Deployment**: Docker + Docker Compose

**No urgent tech changes needed.** This stack scales well.

---

## 🎯 Next Steps (In Order)

1. **Now**: Use `.claude/agents/` for consistent development
   - Feature: `@dotnet-feature`
   - Debug: `@dotnet-debug`
   - Review: `@dotnet-review`

2. **When adding next feature**:
   - Use `@dotnet-architect` to plan
   - Implement with `@dotnet-feature`
   - Organize into feature folder (vertical slice)

3. **When performance matters**:
   - Measure first (don't guess)
   - Add caching (Cache-Aside pattern)
   - Optimize queries (.AsNoTracking(), indexes)

4. **When going production**:
   - Add structured logging (Serilog)
   - Add central error handling
   - Set up monitoring/health checks

5. **When monolith feels crowded**:
   - Consider feature-based slicing
   - Don't split until necessary (monolith scaling is underrated)

---

## 🚨 Anti-Patterns to Avoid

| ❌ Don't | ✅ Do | Why |
|---------|-------|-----|
| Add caching "just in case" | Add caching when slow | YAGNI |
| Create generic helper services | Duplicate code 3x first | YAGNI |
| Over-abstract architecture | Keep it simple | Premature design |
| Mix business logic in endpoints | Put it in services | Testability |
| Use .Result or .Wait() | Always async/await | Deadlock |
| Return raw entities in APIs | Use DTOs | Security |
| Write code without tests | Test as you build | Confidence |

---

## Summary: You're Ready

Your architecture is **solid for the next 3-5 features**. Focus on:

1. **Consistency**: Use agents for every change
2. **Quality**: Tests + code review (use agents)
3. **Simplicity**: YAGNI—don't add until needed
4. **Scalability**: Already built-in (Redis queues, async)

**When things get complex later**, refer to this roadmap. Add infrastructure only when you hit real problems.

Good luck! 🚀
