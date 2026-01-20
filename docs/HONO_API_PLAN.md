# План: Hono API на Railway (отдельный репозиторий)

> **Цель:** Отдельный репо с Hono API на чистом JS, без TypeScript, для деплоя на Railway

---

## Что будем делать:

1. Создать новый репозиторий
2. Настроить Hono на чистом JS (без TS)
3. Добавить JWT auth (Supabase)
4. Добавить Redis для ownership
5. 3 эндпоинта: create, status, download
6. Dockerfile для Railway
7. Задеплоить на Railway

**Время:** ~1 час работы

---

## Структура нового проекта:

```
hono-api/
├── index.js          # Главный файл (весь код здесь)
├── package.json      # Зависимости
├── Dockerfile        # Для Railway
├── .env.example      # Пример переменных
├── .gitignore
└── README.md
```

**Всё в ОДНОМ файле** - максимально просто!

---

## ШАГ 1: Создать репозиторий

```bash
# На твоём компьютере
mkdir hono-api
cd hono-api

# Инициализация
npm init -y
git init
```

---

## ШАГ 2: Установить зависимости

```bash
npm install hono
npm install ioredis
npm install jose
npm install dotenv
```

**Что это:**
- `hono` - фреймворк
- `ioredis` - Redis клиент (как у тебя сейчас)
- `jose` - JWT проверка (как у тебя сейчас)
- `dotenv` - переменные окружения (.env файл)

---

## ШАГ 3: Создать index.js

**Файл:** `index.js`

```javascript
import { Hono } from 'hono';
import { cors } from 'hono/cors';
import Redis from 'ioredis';
import { jwtVerify, createRemoteJWKSet } from 'jose';
import 'dotenv/config';

// === КОНФИГУРАЦИЯ ===
const PORT = process.env.PORT || 3000;
const SUPABASE_JWKS_URL = process.env.SUPABASE_JWKS_URL;
const REDIS_URL = process.env.REDIS_URL;
const PDF_SERVICE_URL = process.env.PDF_SERVICE_URL;
const INTERNAL_API_KEY = process.env.INTERNAL_API_KEY;
const CORS_ORIGIN = process.env.CORS_ORIGIN || '*';

// Проверка обязательных переменных
if (!SUPABASE_JWKS_URL) throw new Error('SUPABASE_JWKS_URL не указан');
if (!REDIS_URL) throw new Error('REDIS_URL не указан');
if (!PDF_SERVICE_URL) throw new Error('PDF_SERVICE_URL не указан');

// === REDIS ===
const redis = new Redis(REDIS_URL);
redis.on('error', (err) => console.error('Redis error:', err));

// Сохранить владельца job
async function setOwner(jobId, userId) {
  await redis.set(`job:owner:${jobId}`, userId, 'EX', 86400); // 24 часа
}

// Проверить владельца job
async function verifyOwner(jobId, userId) {
  const owner = await redis.get(`job:owner:${jobId}`);
  return !owner || owner === userId; // Если нет владельца или совпадает
}

// === JWT AUTH ===
const JWKS = createRemoteJWKSet(new URL(SUPABASE_JWKS_URL));

// Проверить токен
async function verifyToken(token) {
  try {
    const { payload } = await jwtVerify(token, JWKS, {
      algorithms: ['ES256'], // Supabase использует ES256
    });
    return payload;
  } catch (err) {
    console.error('JWT проверка не прошла:', err.message);
    return null;
  }
}

// Извлечь токен из заголовка
function extractToken(authHeader) {
  if (!authHeader) return null;
  if (authHeader.startsWith('Bearer ')) return authHeader.slice(7);
  return authHeader;
}

// === MIDDLEWARE AUTH ===
async function authMiddleware(c, next) {
  const authHeader = c.req.header('Authorization');
  const token = extractToken(authHeader);

  if (!token) {
    return c.json({ error: 'Нет токена авторизации' }, 401);
  }

  const payload = await verifyToken(token);

  if (!payload || !payload.sub) {
    return c.json({ error: 'Невалидный токен' }, 401);
  }

  // Сохраняем user ID в контекст
  c.set('userId', payload.sub);
  await next();
}

// === ВАЛИДАЦИЯ ===
function validateProposal(data) {
  const errors = [];

  if (!data.projectName || data.projectName.length < 2) {
    errors.push('Название проекта минимум 2 символа');
  }
  if (!data.budget || data.budget < 100) {
    errors.push('Бюджет минимум 100');
  }
  if (!data.deadline || data.deadline.length < 2) {
    errors.push('Укажите дедлайн');
  }
  if (!data.description || data.description.length < 10) {
    errors.push('Описание минимум 10 символов');
  }

  return errors;
}

// === HONO APP ===
const app = new Hono();

// CORS (разрешить запросы с твоего сайта)
app.use('*', cors({
  origin: CORS_ORIGIN,
  allowMethods: ['GET', 'POST', 'OPTIONS'],
  allowHeaders: ['Content-Type', 'Authorization'],
  exposeHeaders: ['Content-Disposition', 'Content-Type'],
}));

// Health check (проверка что сервис работает)
app.get('/health', (c) => {
  return c.json({ status: 'ok', timestamp: new Date().toISOString() });
});

// === ЭНДПОИНТ 1: Создать proposal ===
app.post('/api/proposals/create', authMiddleware, async (c) => {
  const userId = c.get('userId');
  const data = await c.req.json();

  // Валидация
  const errors = validateProposal(data);
  if (errors.length > 0) {
    return c.json({ error: 'Ошибка валидации', details: errors }, 400);
  }

  try {
    // Вызвать .NET сервис для создания job
    const response = await fetch(`${PDF_SERVICE_URL}/jobs/proposal`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Internal-Key': INTERNAL_API_KEY,
      },
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      throw new Error('PDF сервис вернул ошибку');
    }

    const result = await response.json();

    // Сохранить ownership в Redis
    await setOwner(result.jobId, userId);

    return c.json({
      jobId: result.jobId,
      status: result.status || 'pending',
    });
  } catch (err) {
    console.error('Ошибка создания:', err);
    return c.json({ error: 'Не удалось создать proposal' }, 500);
  }
});

// === ЭНДПОИНТ 2: Проверить статус ===
app.get('/api/proposals/status/:jobId', authMiddleware, async (c) => {
  const userId = c.get('userId');
  const jobId = c.req.param('jobId');

  // Проверить ownership
  const hasAccess = await verifyOwner(jobId, userId);
  if (!hasAccess) {
    return c.json({ error: 'Доступ запрещён' }, 403);
  }

  try {
    // Запросить статус у .NET сервиса
    const response = await fetch(`${PDF_SERVICE_URL}/jobs/${jobId}/status`, {
      headers: { 'X-Internal-Key': INTERNAL_API_KEY },
    });

    if (!response.ok) {
      return c.json({ error: 'Job не найден' }, 404);
    }

    const result = await response.json();

    // Если завершён - добавить ссылку на скачивание
    if (result.status === 2 || result.status === 'Completed' || result.status === 'completed') {
      result.downloadUrl = `/api/proposals/download/${jobId}`;
    }

    return c.json(result);
  } catch (err) {
    console.error('Ошибка получения статуса:', err);
    return c.json({ error: 'Не удалось получить статус' }, 500);
  }
});

// === ЭНДПОИНТ 3: Скачать PDF ===
app.get('/api/proposals/download/:jobId', authMiddleware, async (c) => {
  const userId = c.get('userId');
  const jobId = c.req.param('jobId');

  // Проверить ownership
  const hasAccess = await verifyOwner(jobId, userId);
  if (!hasAccess) {
    return c.json({ error: 'Доступ запрещён' }, 403);
  }

  try {
    // Скачать PDF у .NET сервиса
    const response = await fetch(`${PDF_SERVICE_URL}/jobs/${jobId}/download`, {
      headers: { 'X-Internal-Key': INTERNAL_API_KEY },
    });

    if (!response.ok) {
      return c.json({ error: 'PDF не готов' }, 404);
    }

    const pdfBuffer = await response.arrayBuffer();

    // Вернуть PDF файл
    return c.body(pdfBuffer, 200, {
      'Content-Type': 'application/pdf',
      'Content-Disposition': `attachment; filename="proposal-${jobId}.pdf"`,
    });
  } catch (err) {
    console.error('Ошибка скачивания:', err);
    return c.json({ error: 'Не удалось скачать PDF' }, 500);
  }
});

// Запустить сервер
console.log(`🚀 Сервер запущен на порту ${PORT}`);
export default {
  port: PORT,
  fetch: app.fetch,
};
```

---

## ШАГ 4: Создать package.json

**Файл:** `package.json`

```json
{
  "name": "hono-api",
  "version": "1.0.0",
  "type": "module",
  "scripts": {
    "dev": "node --watch index.js",
    "start": "node index.js"
  },
  "dependencies": {
    "hono": "^4.0.0",
    "ioredis": "^5.3.2",
    "jose": "^5.2.0",
    "dotenv": "^16.4.0"
  }
}
```

---

## ШАГ 5: Создать .env файл

**Файл:** `.env` (для локальной разработки)

```bash
# Supabase
SUPABASE_JWKS_URL=https://твой-проект.supabase.co/auth/v1/.well-known/jwks.json

# Redis
REDIS_URL=redis://localhost:6379

# .NET PDF сервис (Railway URL)
PDF_SERVICE_URL=https://твой-нет-сервис.up.railway.app
INTERNAL_API_KEY=mellow-secret-key-2025

# CORS
CORS_ORIGIN=https://mellow.io

# Порт (Railway установит автоматически)
PORT=3000
```

**Файл:** `.env.example` (закоммитить в git)

```bash
SUPABASE_JWKS_URL=https://your-project.supabase.co/auth/v1/.well-known/jwks.json
REDIS_URL=redis://localhost:6379
PDF_SERVICE_URL=https://your-service.railway.app
INTERNAL_API_KEY=your-secret-key
CORS_ORIGIN=https://your-domain.com
PORT=3000
```

---

## ШАГ 6: Создать .gitignore

**Файл:** `.gitignore`

```
node_modules/
.env
*.log
.DS_Store
```

---

## ШАГ 7: Создать Dockerfile

**Файл:** `Dockerfile`

```dockerfile
# Используем официальный Node.js образ
FROM node:20-alpine

# Рабочая директория
WORKDIR /app

# Копировать package.json и установить зависимости
COPY package*.json ./
RUN npm install --production

# Копировать код
COPY . .

# Порт (Railway установит через переменную PORT)
EXPOSE 3000

# Запустить приложение
CMD ["node", "index.js"]
```

---

## ШАГ 8: Протестировать локально

```bash
# Установить зависимости
npm install

# Запустить (нужен Redis локально)
npm run dev

# Проверить health
curl http://localhost:3000/health

# Должно вернуть: {"status":"ok","timestamp":"..."}
```

---

## ШАГ 9: Создать git репозиторий

```bash
# Инициализация
git init
git add .
git commit -m "Initial commit: Hono API"

# Создать репо на GitHub (через браузер)
# Потом:
git remote add origin git@github.com:твой-юзер/hono-api.git
git push -u origin main
```

---

## ШАГ 10: Деплой на Railway

### Вариант A: Через Railway CLI

```bash
# Установить Railway CLI
npm install -g @railway/cli

# Войти
railway login

# Создать проект
railway init

# Добавить Redis
railway add
# Выбрать: Redis

# Установить переменные окружения
railway variables set SUPABASE_JWKS_URL="https://..."
railway variables set PDF_SERVICE_URL="https://..."
railway variables set INTERNAL_API_KEY="mellow-secret-key-2025"
railway variables set CORS_ORIGIN="https://mellow.io"

# Деплой
railway up

# Получить URL
railway domain
```

### Вариант B: Через Railway Dashboard

1. Открыть railway.app
2. New Project → Deploy from GitHub
3. Выбрать репозиторий `hono-api`
4. Railway автоматически найдёт Dockerfile
5. В Settings → Variables добавить переменные
6. Deploy!

---

## ШАГ 11: Проверить на Railway

```bash
# Получить URL (например: hono-api-production.up.railway.app)
RAILWAY_URL="https://твой-url.up.railway.app"

# Проверить health
curl $RAILWAY_URL/health

# Проверить API (нужен JWT токен)
curl -X POST $RAILWAY_URL/api/proposals/create \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ВАШ_JWT_ТОКЕН" \
  -d '{
    "projectName": "Test",
    "budget": 1000,
    "deadline": "1 месяц",
    "description": "Тестовый проект для проверки"
  }'
```

---

## ШАГ 12: Обновить фронтенд

В твоём фронтенде поменять API URL:

```javascript
// Старый URL (Vercel)
const API_URL = 'https://твой-проект.vercel.app';

// Новый URL (Railway)
const API_URL = 'https://hono-api-production.up.railway.app';

// Эндпоинты остались теми же
// POST /api/proposals/create
// GET /api/proposals/status/:jobId
// GET /api/proposals/download/:jobId
```

---

## ✅ Чек-лист

### Локально
- [ ] `npm install` успешно
- [ ] `npm run dev` запускается
- [ ] `/health` возвращает 200
- [ ] Redis подключен (проверить логи)

### Railway
- [ ] Репозиторий создан на GitHub
- [ ] Railway проект создан
- [ ] Redis добавлен
- [ ] Переменные окружения установлены
- [ ] Деплой успешен
- [ ] `/health` работает на production URL

### Интеграция
- [ ] Фронтенд обновлён с новым API URL
- [ ] Можно создать proposal
- [ ] Можно проверить статус
- [ ] Можно скачать PDF

---

## 🔧 Типичные проблемы

**"Cannot find module 'hono'"**
- Забыл `npm install`

**"SUPABASE_JWKS_URL not configured"**
- Не установлены переменные окружения
- Проверь `.env` файл

**"Redis connection error"**
- На Railway: Redis не добавлен или не подключен
- Локально: Redis не запущен (`redis-server`)

**"CORS error"**
- Проверь что `CORS_ORIGIN` указан правильно
- В продакшене должен быть `https://mellow.io`
- Для теста можно временно `*`

**"PDF service error"**
- Проверь `PDF_SERVICE_URL` (должен быть Railway URL твоего .NET сервиса)
- Проверь `INTERNAL_API_KEY` (должен совпадать с .NET)

---

## 📚 Что дальше?

После успешного деплоя:

1. **Удалить Vercel API** (старый Hono код)
2. **Оставить Vercel только для статики** (vite build)
3. **Обновить CORS** в .NET сервисе (разрешить Hono API URL)

**Готово!** Теперь у тебя:
- ✅ Чистая архитектура (разделение фронта и бека)
- ✅ Понятный код (один файл JS)
- ✅ Стабильный деплой (Railway, не serverless)
- ✅ Масштабируемость (Railway справится с нагрузкой)
