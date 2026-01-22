# 🚀 PDF Proposal Generator - Implementation Guide

## Архитектура

```
Webflow (Frontend)
       ↓
Hono.js API (Proxy) → C# Microservice → Redis
       ↓                      ↓
   PDF Preview          QuestPDF + Claude AI
```

---

## 📁 Структура проекта (Feature-Based)

```
src/
├── Features/
│   ├── ProposalGeneration/      # Создание предложений
│   │   ├── Models.cs             # ProposalRequest, ProposalData
│   │   ├── ClaudeService.cs      # AI генерация
│   │   ├── ProposalDocument.cs   # QuestPDF рендеринг
│   │   ├── BrandSystem.cs        # Дизайн система
│   │   └── Components/           # PDF компоненты
│   │
│   ├── ProposalUpdate/           # Обновление предложений
│   │   ├── Models.cs             # UpdateRequest, UpdateType
│   │   ├── ClassifierService.cs  # AI классификатор
│   │   ├── UpdateService.cs      # Логика обновлений
│   │   └── Endpoints.cs          # API endpoints
│   │
│   ├── DocumentProcessing/       # RAG документы
│   │   ├── DocumentService.cs    # Обработка файлов
│   │   ├── EmbeddingService.cs   # OpenAI embeddings
│   │   ├── DocumentRepository.cs # Redis хранилище
│   │   └── Endpoints.cs
│   │
│   └── JobQueue/                 # Async очередь
│       ├── JobService.cs
│       ├── JobRepository.cs
│       ├── JobProcessor.cs
│       └── Models.cs
│
├── Shared/
│   └── RedisConfig.cs
│
└── Program.cs
```

**Преимущества:**
- ✅ Все связанное с feature в одной папке
- ✅ Легко находить код
- ✅ Просто добавлять новые features
- ✅ БЕЗ ОВЕРХЕДА (не DDD, не CQRS)

---

## 🔄 Как работает система

### 1. Первичное создание (9-18 секунд)

```
1. Пользователь заполняет форму:
   - Project Name
   - Budget
   - Deadline
   - Description
   - [Optional] Загружает документ (TZ)

2. Frontend → Hono API → C# Microservice

3. C# проверяет наличие документа:
   ├─ ЕСЛИ документ загружен:
   │  └─ RAG: Разбивает на чанки → Embeddings → Redis
   │  └─ Claude Haiku использует RAG контекст
   │
   └─ ЕСЛИ документа НЕТ:
      └─ Только Claude Haiku (без RAG)

4. Claude генерирует ProposalData (JSON)

5. QuestPDF рендерит PDF

6. Сохраняет в Redis:
   - PDF (bytes)
   - ProposalData (JSON) ← ВАЖНО для обновлений!

7. Frontend получает PDF и показывает preview
```

### 2. Обновление (1-10 секунд)

```
1. Пользователь видит preview PDF

2. Пользователь пишет что изменить:
   "Измени deadline на 3 недели"

3. Frontend → Hono API → C# UpdateService

4. AI Классификатор (Haiku ~500ms) определяет тип:

   ├─ Simple (1-2 сек):
   │  └─ Простые правки текста
   │  └─ Изменение чисел
   │  └─ БЕЗ AI генерации, только редактирование JSON
   │
   ├─ Complex (5-8 сек):
   │  └─ Переформулировка текста
   │  └─ Haiku редактирует ProposalData
   │  └─ БЕЗ RAG
   │
   └─ FullRegen (9-18 сек):
      └─ Новый документ загружен
      └─ Полная регенерация с RAG

5. QuestPDF рендерит обновленный PDF

6. Frontend показывает обновленный preview

7. Пользователь может:
   - Скачать PDF
   - ИЛИ продолжить редактирование (повторить пункт 2)
```

---

## ⚡ Оптимизации скорости

### Почему так быстро?

1. **Job Queue (Redis)**
   - Async обработка
   - Concurrency: 5 одновременных jobs
   - Polling каждые 2 секунды

2. **Умный классификатор**
   - Simple changes: ~1-2 секунды (без AI)
   - Complex changes: ~5-8 секунд (Haiku)
   - Full regen: ~9-18 секунд (Haiku + RAG)

3. **Сохранение ProposalData**
   - Не нужно регенерировать весь контент
   - Правки применяются к существующему JSON
   - Детерминизм: AI не переделывает всё

4. **Условная RAG логика**
   - Если документов нет → Haiku без RAG (быстрее)
   - Если документы есть → RAG (медленнее, но точнее)

---

## 🛠️ Деплой (Railway)

### 1. C# Microservice

```bash
# railway.toml уже настроен

railway login
railway init
railway link

# Добавить переменные окружения:
railway variables set ANTHROPIC_API_KEY=sk-...
railway variables set INTERNAL_API_KEY=your-secret-key
railway variables set REDIS_URL=${{Redis.REDIS_URL}}

railway up
```

### 2. Hono.js API

```bash
mkdir hono-api
cd hono-api
npm init -y
npm install hono

# Скопируйте docs/hono-api-complete.js → index.js

railway init
railway variables set DOTNET_API_URL=https://your-dotnet.railway.app
railway variables set INTERNAL_API_KEY=your-secret-key

railway up
```

### 3. Webflow Frontend

1. Создайте страницу `/pdf-proposal`

2. Добавьте HTML структуру (см. `docs/webflow-frontend-complete.html`)

3. В Page Settings → Custom Code → Before </body>:
   - Вставьте весь код из `docs/webflow-frontend-complete.html`
   - Обновите `HONO_API_URL` на ваш Railway URL

4. Опубликуйте!

---

## 📊 API Endpoints

### ProposalGeneration

```
POST   /jobs/proposal           # Создать job
GET    /jobs/{id}/status        # Статус job
GET    /jobs/{id}/download      # Скачать PDF
POST   /generate/proposal       # Legacy sync endpoint
```

### ProposalUpdate

```
POST   /jobs/proposal/{id}/update   # Обновить предложение
GET    /jobs/proposal/{id}/data     # Получить ProposalData
```

### DocumentProcessing (RAG)

```
POST   /api/documents/upload    # Загрузить документ
DELETE /api/documents/{id}      # Удалить документы проекта
```

---

## 🔐 Безопасность

### Authentication
Все endpoints защищены `X-Internal-Key` header:

```javascript
headers: {
  'X-Internal-Key': process.env.INTERNAL_API_KEY
}
```

**Важно:** Храните `INTERNAL_API_KEY` в переменных окружения!

---

## 🎨 Customization

### Изменить цвета бренда

```csharp
// src/Features/ProposalGeneration/BrandSystem.cs

public static class BrandColors
{
    public static string Primary => "#FF6F23";  // Оранжевый
    public static string Background => "#F8F2E9";  // Бежевый
    // Измените на свои цвета!
}
```

### Изменить шрифт

```csharp
// Program.cs

var fontFiles = new[] { "Inter-Regular.ttf", "Inter-Bold.ttf" };
// Замените на свои .ttf файлы
```

### Изменить логотип

Замените файл `logo.png` в корне проекта.

---

## 🧪 Тестирование

### Локальная разработка

```bash
# C# microservice
dotnet run

# Hono API
cd hono-api
node --watch index.js

# Frontend
# Откройте Webflow и тестируйте
```

### Тестовые сценарии

1. **Без документа (только Haiku):**
   - Заполните форму без файла
   - Ожидаемо: ~9 секунд

2. **С документом (RAG):**
   - Загрузите PDF/TXT
   - Ожидаемо: ~13-18 секунд

3. **Simple Update:**
   - "Измени deadline на 3 недели"
   - Ожидаемо: ~1-2 секунды

4. **Complex Update:**
   - "Сделай summary более профессиональным"
   - Ожидаемо: ~5-8 секунд

5. **Full Regen:**
   - Загрузите новый документ
   - Ожидаемо: ~13-18 секунд

---

## 💰 Стоимость (Claude Haiku)

### Input
- $0.25 / 1M tokens

### Output
- $1.25 / 1M tokens

### Примерная стоимость на 1 proposal:

- **Без RAG:** ~$0.0001-0.0003
- **С RAG:** ~$0.0003-0.0008
- **Simple Update:** ~$0.00001
- **Complex Update:** ~$0.0001-0.0002

**$1000 бюджет** ≈ **1-3 млн proposals** (в зависимости от сложности)

---

## 📝 Supported File Types

### RAG Documents
- ✅ PDF (.pdf)
- ✅ Word (.doc, .docx)
- ✅ Markdown (.md)
- ✅ Text (.txt)
- ✅ JSON (.json)

### Frontend Validation

```javascript
const allowedTypes = [
  'application/pdf',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'text/markdown',
  'text/plain',
  'application/json'
];
```

---

## 🐛 Troubleshooting

### PDF не генерируется

1. Проверьте Redis подключение:
   ```bash
   railway logs
   # Ищите "Redis connected"
   ```

2. Проверьте Anthropic API Key:
   ```bash
   railway variables
   # ANTHROPIC_API_KEY должен быть установлен
   ```

### Frontend не подключается

1. Проверьте CORS в Hono API:
   ```javascript
   origin: '*',  // Или укажите домен Webflow
   ```

2. Проверьте HONO_API_URL в Webflow коде

### Update не работает

1. Проверьте что ProposalData сохраняется:
   ```bash
   # В логах должно быть:
   "Job {JobId} PDF result saved"
   ```

2. Проверьте ClassifierService работает:
   ```bash
   # В логах:
   "Update classified as {Type}"
   ```

---

## 🚀 Next Steps

### Фаза 1 (Сделано ✅)
- Feature-Based архитектура
- ProposalGeneration
- ProposalUpdate с классификатором
- RAG документы
- Hono.js API
- Webflow Frontend

### Фаза 2 (TODO)
- [ ] Multi-page PDF support
- [ ] Custom templates
- [ ] Email delivery
- [ ] Payment integration
- [ ] Analytics dashboard

---

## 📚 Resources

- [QuestPDF Documentation](https://www.questpdf.com/)
- [Claude AI Docs](https://docs.anthropic.com/)
- [Hono Documentation](https://hono.dev/)
- [Railway Docs](https://docs.railway.app/)
- [Webflow University](https://university.webflow.com/)

---

**Вопросы?** Создайте issue в репозитории!

**Made with ❤️ by Claude Agent**
