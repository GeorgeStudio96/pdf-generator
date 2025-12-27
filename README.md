# PDF Service

AI-powered commercial proposal generator by **Mellow Team**.

## What it does

Automatically generates professional PDF proposals using Claude AI. Send project details, get a formatted proposal document.

## How it works

1. Receives project data (name, budget, deadline, description)
2. Claude AI generates structured proposal content
3. Returns professionally designed PDF with branding

## Tech Stack

- .NET 9.0 Web API
- Claude AI (Anthropic)
- QuestPDF for document generation

## Usage

```bash
POST /generate/proposal
X-Internal-Key: <your-key>

{
  "projectName": "Website Redesign",
  "budget": 50000,
  "deadline": "Q2 2025",
  "description": "Modern e-commerce platform"
}
```

Returns: PDF file with executive summary, timeline, budget breakdown, and project stages.
