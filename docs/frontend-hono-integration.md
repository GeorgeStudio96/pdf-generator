# Интеграция Frontend + Hono с PDF-сервисом (C# + Redis)

## Обзор архитектуры

```
Frontend (Vite/TypeScript) → Hono Backend → C# PDF-сервис → Redis + Claude AI
     mellow.io/dashboard         :3000           :5038
```

**Что УЖЕ готово:**
- ✅ C# PDF-сервис с async job queue (Redis)
- ✅ Background processor для обработки jobs
- ✅ 3 endpoint'а: create job, check status, download PDF

**Что НУЖНО сделать:**
- 🔨 Hono бекенд: 3 роута (проксирование в C# сервис)
- 🔨 Frontend: форма + polling + download

---

## 1. Hono Backend (3 роута)

### Структура проекта
```
api-dev/
  ├── server.ts          # Главный файл
  ├── routes/
  │   └── proposals.ts   # Роуты для PDF
  └── config.ts          # Конфигурация
```

### Файл: api-dev/config.ts
```typescript
export const config = {
  pdfServiceUrl: process.env.PDF_SERVICE_URL || 'http://localhost:5038',
  internalApiKey: process.env.INTERNAL_API_KEY || 'mellow-secret-key-2025',
  corsOrigin: process.env.CORS_ORIGIN || 'https://mellow.io',
};
```

### Файл: api-dev/routes/proposals.ts
```typescript
import { Hono } from 'hono';
import { zValidator } from '@hono/zod-validator';
import { z } from 'zod';
import { config } from '../config';

const proposalsRouter = new Hono();

// Схема валидации для создания proposal
const proposalSchema = z.object({
  projectName: z.string().min(2, 'Укажите название проекта'),
  budget: z.number().min(100, 'Бюджет слишком мал'),
  deadline: z.string().min(2, 'Укажите дедлайн (напр. "Q2 2025")'),
  description: z.string().min(10, 'Опишите задачу подробнее'),
});

// 1️⃣ Создать job
proposalsRouter.post(
  '/',
  zValidator('json', proposalSchema),
  async (c) => {
    const data = c.req.valid('json');

    try {
      const response = await fetch(`${config.pdfServiceUrl}/jobs/proposal`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Internal-Key': config.internalApiKey,
        },
        body: JSON.stringify(data),
      });

      if (!response.ok) {
        throw new Error('PDF service error');
      }

      const result = await response.json();

      return c.json({
        success: true,
        jobId: result.jobId,
        status: result.status,
        createdAt: result.createdAt,
      });
    } catch (error) {
      console.error('Error creating job:', error);
      return c.json({ error: 'Не удалось создать задачу' }, 500);
    }
  }
);

// 2️⃣ Проверить статус
proposalsRouter.get('/:jobId/status', async (c) => {
  const jobId = c.req.param('jobId');

  try {
    const response = await fetch(
      `${config.pdfServiceUrl}/jobs/${jobId}/status`,
      {
        headers: { 'X-Internal-Key': config.internalApiKey },
      }
    );

    if (!response.ok) {
      return c.json({ error: 'Job не найден' }, 404);
    }

    const result = await response.json();

    return c.json({
      jobId: result.jobId,
      status: result.status, // 0=Pending, 1=Processing, 2=Completed, 3=Failed
      createdAt: result.createdAt,
      completedAt: result.completedAt,
      errorMessage: result.errorMessage,
    });
  } catch (error) {
    console.error('Error checking status:', error);
    return c.json({ error: 'Ошибка проверки статуса' }, 500);
  }
});

// 3️⃣ Скачать PDF
proposalsRouter.get('/:jobId/download', async (c) => {
  const jobId = c.req.param('jobId');

  try {
    const response = await fetch(
      `${config.pdfServiceUrl}/jobs/${jobId}/download`,
      {
        headers: { 'X-Internal-Key': config.internalApiKey },
      }
    );

    if (!response.ok) {
      return c.json({ error: 'PDF не готов или не найден' }, 404);
    }

    const pdfBuffer = await response.arrayBuffer();

    return c.body(pdfBuffer, 200, {
      'Content-Type': 'application/pdf',
      'Content-Disposition': `attachment; filename="proposal-${jobId}.pdf"`,
    });
  } catch (error) {
    console.error('Error downloading PDF:', error);
    return c.json({ error: 'Ошибка скачивания PDF' }, 500);
  }
});

export default proposalsRouter;
```

### Файл: api-dev/server.ts
```typescript
import { serve } from '@hono/node-server';
import { Hono } from 'hono';
import { cors } from 'hono/cors';
import proposalsRouter from './routes/proposals';
import { config } from './config';

const app = new Hono();

// CORS
app.use('/*', cors({
  origin: config.corsOrigin,
  credentials: true,
}));

// Health check
app.get('/health', (c) => c.json({ status: 'ok' }));

// Routes
app.route('/api/proposals', proposalsRouter);

// 404
app.notFound((c) => c.json({ error: 'Not found' }, 404));

// Start server
const port = Number(process.env.PORT) || 3000;
console.log(`🚀 Hono server running on http://localhost:${port}`);

serve({
  fetch: app.fetch,
  port,
});
```

### Файл: .env (в корне Hono проекта)
```env
PDF_SERVICE_URL=http://localhost:5038
INTERNAL_API_KEY=mellow-secret-key-2025
CORS_ORIGIN=https://mellow.io
PORT=3000
```

---

## 2. Frontend (Vite/TypeScript)

### Структура
```
src/
  ├── pages/
  │   └── dashboard.ts   # Логика dashboard
  ├── types/
  │   └── proposal.ts    # TypeScript типы
  └── utils/
      └── api.ts         # API клиент
```

### Файл: src/types/proposal.ts
```typescript
export interface ProposalFormData {
  projectName: string;
  budget: number;
  deadline: string;
  description: string;
}

export interface JobResponse {
  success: boolean;
  jobId: string;
  status: number;
  createdAt: string;
}

export interface JobStatus {
  jobId: string;
  status: 0 | 1 | 2 | 3; // Pending, Processing, Completed, Failed
  createdAt: string;
  completedAt: string | null;
  errorMessage: string | null;
}

export const JobStatusText = {
  0: '⏳ В очереди',
  1: '🔄 Обрабатывается',
  2: '✅ Готово',
  3: '❌ Ошибка',
} as const;
```

### Файл: src/utils/api.ts
```typescript
import type { ProposalFormData, JobResponse, JobStatus } from '../types/proposal';

// ============================================================
// Configuration
// ============================================================
const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:3000';

// ============================================================
// API Functions
// ============================================================
export async function createProposal(data: ProposalFormData): Promise<JobResponse> {
  const response = await fetch(`${API_URL}/api/proposals`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.error || 'Ошибка создания задачи');
  }

  return response.json();
}

export async function checkJobStatus(jobId: string): Promise<JobStatus> {
  const response = await fetch(`${API_URL}/api/proposals/${jobId}/status`);

  if (!response.ok) {
    throw new Error('Job не найден');
  }

  return response.json();
}

export function getDownloadUrl(jobId: string): string {
  return `${API_URL}/api/proposals/${jobId}/download`;
}
```

### Файл: src/pages/dashboard.ts
```typescript
import { createProposal, checkJobStatus, getDownloadUrl } from '../utils/api';
import { JobStatusText } from '../types/proposal';
import type { ProposalFormData } from '../types/proposal';

// ============================================================
// Состояние (module scope)
// ============================================================
let currentJobId: string | null = null;
let pollingInterval: number | null = null;

// ============================================================
// DOM Utilities
// ============================================================
function getElements() {
  return {
    form: document.querySelector<HTMLFormElement>('[data-proposal-form]'),
    status: document.querySelector<HTMLElement>('[data-status]'),
    downloadBtn: document.querySelector<HTMLButtonElement>('[data-download]'),
    submitBtn: document.querySelector<HTMLButtonElement>('[data-submit]'),
  };
}

// ============================================================
// UI Updates
// ============================================================
function updateStatus(status: 0 | 1 | 2 | 3) {
  const elements = getElements();
  if (elements.status) {
    elements.status.textContent = JobStatusText[status];
  }
}

function showDownloadButton() {
  const elements = getElements();
  if (elements.downloadBtn) {
    elements.downloadBtn.style.display = 'block';
  }
}

function setLoading(loading: boolean) {
  const elements = getElements();
  if (elements.submitBtn) {
    elements.submitBtn.disabled = loading;
    elements.submitBtn.textContent = loading ? 'Генерируем...' : 'Создать КП';
  }
}

function showError(message: string) {
  alert(`Ошибка: ${message}`);
  updateStatus(3);
}

// ============================================================
// Polling Logic
// ============================================================
function stopPolling() {
  if (pollingInterval) {
    clearInterval(pollingInterval);
    pollingInterval = null;
  }
}

function startPolling(jobId: string) {
  pollingInterval = window.setInterval(async function () {
    try {
      const statusData = await checkJobStatus(jobId);
      updateStatus(statusData.status);

      // Completed
      if (statusData.status === 2) {
        stopPolling();
        showDownloadButton();
        setLoading(false);
      }

      // Failed
      if (statusData.status === 3) {
        stopPolling();
        showError(statusData.errorMessage || 'Неизвестная ошибка');
        setLoading(false);
      }
    } catch (error) {
      console.error('Polling error:', error);
    }
  }, 2000);
}

// ============================================================
// Event Handlers
// ============================================================
async function handleFormSubmit(event: Event) {
  event.preventDefault();

  const form = event.target as HTMLFormElement;
  const formData = new FormData(form);

  const data: ProposalFormData = {
    projectName: formData.get('projectName') as string,
    budget: Number(formData.get('budget')),
    deadline: formData.get('deadline') as string,
    description: formData.get('description') as string,
  };

  try {
    setLoading(true);

    const response = await createProposal(data);
    currentJobId = response.jobId;

    updateStatus(0); // Pending
    startPolling(currentJobId);
  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : 'Ошибка';
    showError(errorMessage);
    setLoading(false);
  }
}

function handleDownloadClick() {
  if (currentJobId) {
    window.location.href = getDownloadUrl(currentJobId);
  }
}

// ============================================================
// Initialization
// ============================================================
function initDashboard() {
  const elements = getElements();

  if (elements.form) {
    elements.form.addEventListener('submit', handleFormSubmit);
  }

  if (elements.downloadBtn) {
    elements.downloadBtn.addEventListener('click', handleDownloadClick);
  }
}

// ============================================================
// Entry Point
// ============================================================
if (document.querySelector('[data-page="dashboard"]')) {
  initDashboard();
}
```

### HTML в Webflow (data-атрибуты)
```html
<!-- Добавь на страницу mellow.io/dashboard -->

<div data-page="dashboard">
  <form data-proposal-form>
    <input type="text" name="projectName" placeholder="Название проекта" required>
    <input type="number" name="budget" placeholder="Бюджет" required>
    <input type="text" name="deadline" placeholder="Дедлайн (Q1 2025)" required>
    <textarea name="description" placeholder="Описание проекта" required></textarea>

    <button type="submit" data-submit>Создать КП</button>
  </form>

  <div data-status style="margin-top: 20px; font-weight: bold;"></div>

  <button data-download style="display: none; margin-top: 20px;">
    Скачать PDF
  </button>
</div>
```

---

## 3. Промпт для Claude Code агента

Скопируй это в новый VSCode проект (где Hono + Frontend):

```markdown
# Задача: Интеграция Frontend + Hono с PDF-сервисом

## Контекст
У меня есть готовый C# PDF-сервис с async job queue (Redis), который работает на localhost:5038.

Мне нужно создать:
1. **Hono бекенд** (порт 3000) - проксирует запросы в C# сервис
2. **Frontend (Vite/TypeScript)** - форма + polling + скачивание PDF

## Архитектура

```
Frontend → Hono (:3000) → C# PDF-сервис (:5038) → Redis + Claude
```

## Готовые C# endpoints

### 1. Создать job
```http
POST http://localhost:5038/jobs/proposal
Headers:
  X-Internal-Key: mellow-secret-key-2025
  Content-Type: application/json

Body:
{
  "projectName": "Test",
  "budget": 50000,
  "deadline": "Q1 2025",
  "description": "Description"
}

Response (50ms):
{
  "jobId": "uuid",
  "status": 0,
  "createdAt": "2025-12-27T..."
}
```

### 2. Проверить статус
```http
GET http://localhost:5038/jobs/{jobId}/status
Headers: X-Internal-Key: mellow-secret-key-2025

Response:
{
  "jobId": "uuid",
  "status": 0, // 0=Pending, 1=Processing, 2=Completed, 3=Failed
  "createdAt": "...",
  "completedAt": "..." или null,
  "errorMessage": "..." или null
}
```

### 3. Скачать PDF
```http
GET http://localhost:5038/jobs/{jobId}/download
Headers: X-Internal-Key: mellow-secret-key-2025

Response: PDF file (application/pdf)
```

## Что нужно сделать

### Hono Backend (api-dev/)

Создай структуру:
```
api-dev/
  ├── server.ts
  ├── routes/
  │   └── proposals.ts
  └── config.ts
```

**Требования:**
- 3 роута: POST /api/proposals, GET /api/proposals/:jobId/status, GET /api/proposals/:jobId/download
- Проксировать запросы в C# сервис (http://localhost:5038)
- Добавлять header X-Internal-Key: mellow-secret-key-2025
- Валидация через zod (уже установлен)
- CORS для https://mellow.io
- Обработка ошибок

### Frontend (src/)

Создай структуру:
```
src/
  ├── pages/
  │   └── dashboard.ts
  ├── types/
  │   └── proposal.ts
  └── utils/
      └── api.ts
```

**Требования:**
- Форма с 4 полями: projectName, budget, deadline, description
- При submit → POST /api/proposals
- Получить jobId → запустить polling каждые 2 секунды
- Показывать статус: "В очереди" → "Обрабатывается" → "Готово"
- Когда status === 2 → показать кнопку "Скачать PDF"
- Использовать data-атрибуты для селекторов: [data-proposal-form], [data-status], [data-download]
- TypeScript типы для всех данных
- **ВАЖНО:** Использовать ТОЛЬКО function declarations (function foo() {}), НЕ arrow functions
- Структура: типы → утилиты → основные функции → event handlers → инициализация (вызовы в конце)
- Читаемый, функциональный стиль без классов

### .env файлы

**Hono (.env в корне):**
```env
PDF_SERVICE_URL=http://localhost:5038
INTERNAL_API_KEY=mellow-secret-key-2025
CORS_ORIGIN=https://mellow.io
PORT=3000
```

**Frontend (.env в корне):**
```env
VITE_API_URL=http://localhost:3000
```

## Технические детали

- **Polling интервал:** 2 секунды
- **Статусы:** 0 (Pending), 1 (Processing), 2 (Completed), 3 (Failed)
- **Тайм-ауты:** Нет (C# сервис сам управляет TTL jobs)
- **Ошибки:** Показывать alert() для простоты

## Запуск

```bash
# Терминал 1: C# сервис (уже работает)
cd /Users/georgeershov/Desktop/pdf-service
dotnet run

# Терминал 2: Hono backend
npm run dev:api

# Терминал 3: Vite frontend
npm run dev:vite
```

## Важно

- НЕ используй WebSocket (только HTTP + polling)
- НЕ храни API ключи в frontend коде (только в Hono)
- НЕ используй классы/ООП в JavaScript (только function declarations)
- НЕ используй arrow functions (только function foo() {})
- НЕ используй Redux/Context (module scope переменные достаточно)
- Код должен быть простым и понятным (~100 строк на frontend)
- Функции объявляются сверху вниз, вызовы — в конце файла

Начни с создания структуры файлов, потом реализуй Hono роуты, затем frontend.
```

---

## 4. Проверка интеграции

### Шаг 1: Запустить всё

```bash
# Терминал 1: Redis
cd /Users/georgeershov/Desktop/pdf-service
docker-compose up -d

# Терминал 2: C# PDF-сервис
dotnet run

# Терминал 3: Hono backend
cd /path/to/hono-project
npm run dev:api

# Терминал 4: Frontend (Vite)
npm run dev:vite
```

### Шаг 2: Протестировать

1. Открой http://localhost:5173/dashboard (или твой Vite dev URL)
2. Заполни форму
3. Нажми "Создать КП"
4. Увидишь: "В очереди" → "Обрабатывается" → "Готово" (через ~10 сек)
5. Нажми "Скачать PDF"

### Шаг 3: Проверить логи

**Hono консоль:**
```
POST /api/proposals → 200 OK
GET /api/proposals/abc-123/status → 200 OK (status: 1)
GET /api/proposals/abc-123/status → 200 OK (status: 2)
GET /api/proposals/abc-123/download → 200 OK
```

**C# консоль:**
```
Job abc-123 created for project 'Test Project'
Job abc-123 status updated to Processing
Job abc-123 completed successfully in 9503ms
```

---

## 5. Деплой (Production)

### Hono Backend → Vercel/Railway
```bash
# Vercel
vercel --prod

# Environment Variables:
PDF_SERVICE_URL=https://your-csharp-service.com
INTERNAL_API_KEY=your-prod-key
CORS_ORIGIN=https://mellow.io
```

### Frontend → Vercel (как сейчас)
```bash
# В .env.production
VITE_API_URL=https://api.mellow.io
```

### C# PDF-сервис → Azure/AWS/DigitalOcean
- Настрой Redis (Azure Redis Cache или AWS ElastiCache)
- Обнови REDIS_CONNECTION_STRING в .env

---

## Итого

**Что получишь:**
- ✅ Мгновенный ответ пользователю (50ms вместо 9 секунд)
- ✅ Polling показывает прогресс
- ✅ Безопасность (API ключ только в Hono)
- ✅ Масштабируемость (Redis + async queue)
- ✅ Простой код (~300 строк всего)

Удачи! 🚀
