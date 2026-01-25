// =======================================================================================
// HONO.JS API - Complete Implementation
// =======================================================================================
// This is a complete Hono.js API that acts as a proxy between Webflow and C# microservice
// Deploy to Railway or any Node.js hosting

import { Hono } from 'hono';
import { cors } from 'hono/cors';

const app = new Hono();

// Configuration
const DOTNET_API_URL = process.env.DOTNET_API_URL || 'http://localhost:5000';
const INTERNAL_API_KEY = process.env.INTERNAL_API_KEY || 'your-secret-key';

// CORS - allow requests from Webflow
app.use('/*', cors({
  origin: '*',  // In production, specify your Webflow domain
  allowMethods: ['GET', 'POST', 'DELETE', 'OPTIONS'],
  allowHeaders: ['Content-Type'],
}));

// =======================================================================================
// PROPOSAL GENERATION ENDPOINTS
// =======================================================================================

// Create new proposal job
app.post('/api/proposals/create', async (c) => {
  try {
    const body = await c.req.json();

    const response = await fetch(`${DOTNET_API_URL}/jobs/proposal`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Internal-Key': INTERNAL_API_KEY,
      },
      body: JSON.stringify({
        projectName: body.projectName,
        budget: body.budget,
        deadline: body.deadline,
        description: body.description,
        projectId: body.projectId || null,  // Optional: for RAG context
      }),
    });

    if (!response.ok) {
      throw new Error(`C# API error: ${response.status}`);
    }

    const result = await response.json();

    return c.json({
      jobId: result.jobId,
      status: result.status,
      createdAt: result.createdAt,
    });
  } catch (error) {
    console.error('Error creating proposal:', error);
    return c.json({ error: error.message }, 500);
  }
});

// Get job status
app.get('/api/proposals/status/:jobId', async (c) => {
  const jobId = c.req.param('jobId');

  try {
    const response = await fetch(`${DOTNET_API_URL}/jobs/${jobId}/status`, {
      headers: {
        'X-Internal-Key': INTERNAL_API_KEY,
      },
    });

    if (!response.ok) {
      if (response.status === 404) {
        return c.json({ error: 'Job not found' }, 404);
      }
      throw new Error(`C# API error: ${response.status}`);
    }

    const result = await response.json();

    return c.json({
      jobId: result.jobId,
      status: result.status,  // 0=Pending, 1=Processing, 2=Completed, 3=Failed
      createdAt: result.createdAt,
      completedAt: result.completedAt,
      errorMessage: result.errorMessage,
    });
  } catch (error) {
    console.error('Error getting status:', error);
    return c.json({ error: error.message }, 500);
  }
});

// Download PDF
app.get('/api/proposals/download/:jobId', async (c) => {
  const jobId = c.req.param('jobId');

  try {
    const response = await fetch(`${DOTNET_API_URL}/jobs/${jobId}/download`, {
      headers: {
        'X-Internal-Key': INTERNAL_API_KEY,
      },
    });

    if (!response.ok) {
      if (response.status === 404) {
        return c.json({ error: 'PDF not ready or job not found' }, 404);
      }
      throw new Error(`C# API error: ${response.status}`);
    }

    const pdfBuffer = await response.arrayBuffer();

    return new Response(pdfBuffer, {
      headers: {
        'Content-Type': 'application/pdf',
        'Content-Disposition': `attachment; filename="proposal-${jobId}.pdf"`,
      },
    });
  } catch (error) {
    console.error('Error downloading PDF:', error);
    return c.json({ error: error.message }, 500);
  }
});

// Get ProposalData for editing
app.get('/api/proposals/:jobId/data', async (c) => {
  const jobId = c.req.param('jobId');

  try {
    const response = await fetch(`${DOTNET_API_URL}/jobs/proposal/${jobId}/data`, {
      headers: {
        'X-Internal-Key': INTERNAL_API_KEY,
      },
    });

    if (!response.ok) {
      if (response.status === 404) {
        return c.json({ error: 'Job not found' }, 404);
      }
      throw new Error(`C# API error: ${response.status}`);
    }

    const result = await response.json();

    return c.json({
      jobId: result.jobId,
      proposalData: result.proposalData,
      createdAt: result.createdAt,
      completedAt: result.completedAt,
    });
  } catch (error) {
    console.error('Error getting proposal data:', error);
    return c.json({ error: error.message }, 500);
  }
});

// =======================================================================================
// PROPOSAL UPDATE ENDPOINT
// =======================================================================================

app.post('/api/proposals/:jobId/update', async (c) => {
  const jobId = c.req.param('jobId');

  try {
    const body = await c.req.json();

    const response = await fetch(`${DOTNET_API_URL}/jobs/proposal/${jobId}/update`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Internal-Key': INTERNAL_API_KEY,
      },
      body: JSON.stringify({
        changeDescription: body.changeDescription || null,
        updatedBaseRequest: body.updatedBaseRequest || null,
        newDocumentPath: body.newDocumentPath || null,
      }),
    });

    if (!response.ok) {
      throw new Error(`C# API error: ${response.status}`);
    }

    const result = await response.json();

    return c.json({
      jobId: result.jobId,
      status: result.status,
      message: result.message,
    });
  } catch (error) {
    console.error('Error updating proposal:', error);
    return c.json({ error: error.message }, 500);
  }
});

// =======================================================================================
// DOCUMENT UPLOAD ENDPOINT (RAG)
// =======================================================================================

app.post('/api/documents/upload', async (c) => {
  try {
    const formData = await c.req.formData();

    // Forward multipart form data to C# API
    const response = await fetch(`${DOTNET_API_URL}/api/documents/upload`, {
      method: 'POST',
      headers: {
        'X-Internal-Key': INTERNAL_API_KEY,
        // Don't set Content-Type - let fetch handle multipart boundary
      },
      body: formData,
    });

    if (!response.ok) {
      throw new Error(`C# API error: ${response.status}`);
    }

    const result = await response.json();

    return c.json({
      projectId: result.projectId,
      fileName: result.fileName,
      chunks: result.chunks,
      message: result.message,
    });
  } catch (error) {
    console.error('Error uploading document:', error);
    return c.json({ error: error.message }, 500);
  }
});

// Delete project documents
app.delete('/api/documents/:projectId', async (c) => {
  const projectId = c.req.param('projectId');

  try {
    const response = await fetch(`${DOTNET_API_URL}/api/documents/${projectId}`, {
      method: 'DELETE',
      headers: {
        'X-Internal-Key': INTERNAL_API_KEY,
      },
    });

    if (!response.ok) {
      throw new Error(`C# API error: ${response.status}`);
    }

    const result = await response.json();

    return c.json({
      message: result.message,
    });
  } catch (error) {
    console.error('Error deleting documents:', error);
    return c.json({ error: error.message }, 500);
  }
});

// =======================================================================================
// HEALTH CHECK
// =======================================================================================

app.get('/health', async (c) => {
  try {
    const response = await fetch(`${DOTNET_API_URL}/health`, {
      headers: {
        'X-Internal-Key': INTERNAL_API_KEY,
      },
    });

    const result = await response.json();

    return c.json({
      hono: 'healthy',
      dotnet: result.status,
      redis: result.redis,
    });
  } catch (error) {
    return c.json({
      hono: 'healthy',
      dotnet: 'unhealthy',
      error: error.message,
    }, 503);
  }
});

app.get('/', (c) => {
  return c.text('Hono PDF Proposal API ✨');
});

export default app;

// =======================================================================================
// DEPLOYMENT
// =======================================================================================
//
// Railway deployment:
// 1. Create new project on Railway
// 2. Connect your Git repo
// 3. Set environment variables:
//    - DOTNET_API_URL=https://your-dotnet-service.railway.app
//    - INTERNAL_API_KEY=your-secret-key
// 4. Deploy!
//
// Local development:
// npm install hono
// npm install -D @hono/node-server
// node --watch server.js
