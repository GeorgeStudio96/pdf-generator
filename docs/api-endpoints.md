# API Endpoints

## Асинхронная генерация PDF

### 1. Создать задачу
```http
POST /jobs/proposal
Headers: X-Internal-Key: mellow-secret-key-2025
Content-Type: application/json

Body:
{
  "projectName": "Название проекта",
  "budget": 50000,
  "deadline": "Q1 2025",
  "description": "Описание проекта"
}

Response (50ms):
{
  "jobId": "uuid",
  "status": 0,  // Pending
  "createdAt": "2025-12-27T11:28:41Z"
}
```

### 2. Проверить статус
```http
GET /jobs/{jobId}/status
Headers: X-Internal-Key: mellow-secret-key-2025

Response (10ms):
{
  "jobId": "uuid",
  "status": 0,  // 0=Pending, 1=Processing, 2=Completed, 3=Failed
  "createdAt": "2025-12-27T11:28:41Z",
  "completedAt": "2025-12-27T11:28:53Z",
  "errorMessage": null
}
```

### 3. Скачать PDF
```http
GET /jobs/{jobId}/download
Headers: X-Internal-Key: mellow-secret-key-2025

Response (100ms):
Файл: proposal-{jobId}.pdf
```

## Служебные

### Health Check
```http
GET /health

Response:
{
  "status": "healthy",
  "redis": "connected"
}
```

### Legacy (deprecated)
```http
POST /generate/proposal
Headers: X-Internal-Key: mellow-secret-key-2025

Синхронный endpoint (9-10 секунд ожидания)
```

## Статусы задач

- `0` - **Pending** - В очереди
- `1` - **Processing** - Обрабатывается
- `2` - **Completed** - Готово
- `3` - **Failed** - Ошибка

## Polling Pattern

```javascript
// 1. Создать job
const { jobId } = await POST('/jobs/proposal', data);

// 2. Polling каждые 2 секунды
const interval = setInterval(async () => {
  const { status } = await GET(`/jobs/${jobId}/status`);

  if (status === 2) { // Completed
    clearInterval(interval);
    window.location = `/jobs/${jobId}/download`;
  }

  if (status === 3) { // Failed
    clearInterval(interval);
    showError();
  }
}, 2000);
```
