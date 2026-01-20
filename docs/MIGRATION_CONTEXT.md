# Context & Migration Plan: From Hybrid Vercel/Hono to Unified .NET Backend

## 1. Current Architecture ("The Zoo")
The project is currently a complex distributed system involving multiple platforms and languages, built by a full-stack developer exploring different technologies.

- **Frontend Static:** Webflow (mellow.io) hosting the marketing site.
- **Frontend App:** Webflow Cloud (hosting a Next.js app via Edge Workers) for the dashboard UI (/app).
- **Frontend Scripts:** A Vite project (`my-mellow` repo) deployed on **Vercel**. It builds static JS files (`main.js`) used by Webflow via dynamic imports.
- **Backend Proxy (The Bottleneck):** **Hono** (Node.js) deployed as **Vercel Serverless Functions** inside the same `my-mellow` repo.
  - *Role:* Auth (Supabase), Validation (Zod), Queueing tasks to Redis.
- **Core Backend:** A **C# .NET Worker** deployed on **Railway**.
  - *Role:* Consumes tasks from Redis, generates PDFs.
- **Database/Auth:** Supabase & Redis (hosted on Railway).

## 2. The Problem
Attempting to host a "Backend-for-Frontend" (Hono) inside a Vercel static build environment (`vite`) created severe infrastructure friction:

1.  **Build Conflicts:** Vercel struggled to manage two distinct build pipelines (Vite for frontend assets vs. TypeScript for Hono API) in one repo. This led to production outages (Error 404 for `main.js`) when the build command was switched to favor the API.
2.  **CORS Hell:** Conflicting CORS headers from Vercel's infrastructure (`vercel.json`) and Hono's middleware caused requests to be blocked by browsers (Double CORS Headers issue).
3.  **Routing Mismatches:** Vercel's file-system routing conflicted with Hono's internal routing, leading to 404s on API endpoints.
4.  **Redundant Logic:** Validation logic is duplicated between Hono (TypeScript/Zod) and the Core Backend (C#).
5.  **Infrastructure Fragility:** The system is too fragile. A small config change in `vercel.json` to fix the API breaks the static site, and vice versa.

## 3. The Goal
Simplify the architecture by removing the "middleman" (Hono/Vercel Functions) and consolidating the backend logic into the existing .NET ecosystem on Railway.

**Desired State:**
1.  **Vercel:** Hosts **ONLY** static assets (`main.js`). No API, no Serverless Functions. Build command is simple: `npm run build:vite`.
2.  **Railway:** Hosts the Unified Backend (.NET).
    - **API Service:** A new .NET Web API project (in the same solution as the Worker). Handles Auth (Supabase), Validation, and Redis publishing.
    - **Worker Service:** The existing C# PDF generator.
3.  **Frontend:** Sends requests directly to the .NET API on Railway.

## 4. Why .NET?
- **Code Sharing:** Share DTOs (Data Transfer Objects) and Validation logic between the API and the Worker. No need to sync TS interfaces with C# classes.
- **Reliability:** Railway provides a stable container environment. No "cold start" timeouts or complex serverless limitations.
- **Simplified Networking:** The API and Worker live in the same private network (or easily connected) on Railway.
- **Single Source of Truth:** One solution to manage the entire backend logic.

## 5. Action Plan for the Agent
Your task is to implement the **.NET Web API** to replace the current Hono implementation.

1.  **Analyze Hono Logic:** Read `api/proposals/index.ts` and `api/_lib/` to understand the current flow:
    - Auth Middleware (Supabase JWT).
    - Validation (Zod Schema -> Proposal Data).
    - Redis Logic (`LPUSH` to queue).
2.  **Create .NET API:**
    - Set up a new ASP.NET Core Web API project in the existing C# solution.
    - Implement `Supabase.Authentication`.
    - Create a Controller (e.g., `ProposalsController`) with a `Create` endpoint.
    - Port the validation logic using C# DataAnnotations or FluentValidation.
    - Implement the Redis publisher (using `StackExchange.Redis`).
3.  **CORS Setup:** Configure global CORS in `Program.cs` to allow requests from `https://mellow.io` and `https://*.webflow.io`.
4.  **Deploy:** Prepare the Dockerfile/Railway config to deploy this API service alongside the worker.

**Outcome:** A robust, type-safe backend infrastructure that eliminates the Vercel configuration headaches and provides a solid foundation for future features.
