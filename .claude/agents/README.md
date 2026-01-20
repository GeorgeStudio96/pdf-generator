# .NET Agents for pdf-service

Custom Claude Code agents for consistent, high-quality .NET development.

## Available Agents

### 1. **dotnet-feature**
**When**: Adding new features or API endpoints
- Implements new functionality following architecture patterns
- Creates services, endpoints, and DTOs
- Follows SOLID principles and code standards
- Includes testing and security considerations

### 2. **dotnet-debug**
**When**: Fixing bugs or investigating errors
- Isolates and diagnoses issues
- Traces root causes
- Provides minimal, surgical fixes
- Includes regression tests
- Common issues: async problems, null refs, DI issues, N+1 queries

### 3. **dotnet-refactor**
**When**: Improving code quality or architecture
- Refactors for maintainability and performance
- Applies SOLID principles
- Fixes architectural violations
- Runs tests after each change
- YAGNI: Only refactors for real problems

### 4. **dotnet-architect**
**When**: Planning major features or system changes
- Designs components before implementation
- Creates data models and API contracts
- Plans scalability and performance
- Breaks down into implementation steps
- Outputs clear design documents

### 5. **dotnet-test**
**When**: Writing unit and integration tests
- Writes unit tests (mocked dependencies)
- Writes integration tests (real Redis/DB)
- Covers happy paths, errors, edge cases
- Uses AAA pattern (Arrange-Act-Assert)
- Guides on test pyramid and coverage

### 6. **dotnet-review**
**When**: Code reviewing before merge
- Checks naming, style, async/await correctness
- Verifies DI setup and architecture
- Security audit (SQL injection, mass assignment, etc.)
- Database optimization (N+1 queries, tracking)
- Error handling and logging
- Performance and API design
- Outputs detailed review with issues and approvals

---

## Quick Reference

| Task | Agent |
|------|-------|
| Add new API endpoint | `dotnet-feature` |
| Fix a bug | `dotnet-debug` |
| Improve code quality | `dotnet-refactor` |
| Plan new feature | `dotnet-architect` |
| Write tests | `dotnet-test` |
| Review pull request | `dotnet-review` |

---

## How to Use

### Via Claude Code CLI
```bash
# Feature implementation
claude-code -agent dotnet-feature "Add user authentication to the API"

# Debug issue
claude-code -agent dotnet-debug "Fix N+1 query in JobService"

# Code review
claude-code -agent dotnet-review "Review the new JobProcessor implementation"
```

### In Claude Interface
Mention the agent directly:
```
@dotnet-feature Implement pagination for the jobs list

@dotnet-test Write unit tests for JobService

@dotnet-review Check this endpoint for security issues
```

---

## Reference Documentation

Each agent references these core documents:

- **UNIFIED-AGENT.md**: Architecture, naming, patterns, DI setup, async/await rules
- **csharp-style-guide.md**: Coding standards and conventions
- **api-integration-guide.md**: API design, React integration, Redis, AI services
- **dotnet-security.md**: Security checklist, authentication, data protection
- **architecture-modular-monolith.md**: Vertical slices, feature organization, folder structure

All in `/docs/dotnet/`.

---

## Architecture Recap

- **Pattern**: Vertical Slice Architecture (features-first, not layers)
- **API**: Minimal APIs (no controllers)
- **DI**: Built-in ASP.NET Core container
- **Database**: EF Core for SQL + Redis for queues/cache
- **Background**: JobProcessorBackgroundService + Redis Sorted Sets
- **AI**: Anthropic Claude for content generation
- **.NET**: C# 12 with nullable reference types enabled

---

## Standards Summary

### Naming
- PascalCase: Classes, methods, properties
- _camelCase: Private fields
- camelCase: Local variables, parameters
- Async methods: End with `Async`
- Interfaces: Start with `I`

### Code
- File-scoped namespaces (C# 10+)
- Primary constructors (C# 12+)
- Records for DTOs, classes for services
- No #region directives
- Expression bodies for one-liners

### Async
- ALWAYS async (no .Result/.Wait())
- CancellationToken in async chains
- Proper await usage

### Security
- DTOs (never return entities)
- SQL injection prevention (EF Core parameterizes)
- Mass assignment prevention (DTO strictness)
- No hardcoded secrets
- Input validation on APIs

---

## When NOT to Use Agents

- **Simple one-liners**: Use Claude directly
- **Quick questions**: Use Claude directly
- **Generic advice**: Use Claude directly
- **Non-.NET tasks**: Use general Claude

Use agents for **substantial .NET engineering work** that benefits from specialized context.

---

## Future Enhancements

- [ ] Continuous Integration agent (CI/CD pipeline setup)
- [ ] Performance profiling agent
- [ ] Database migration agent
- [ ] Docker/containerization agent
- [ ] Monitoring & logging agent
