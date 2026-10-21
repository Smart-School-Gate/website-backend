const { Pool } = require('pg');
const crypto = require('crypto');

// Neon PostgreSQL connection configuration
const connectionString = process.env.DATABASE_URL ||
  'postgresql://neondb_owner:npg_T2Fen4oiDqOQ@ep-withered-cake-b4iocdkv-pooler.c-6.us-east-2.aws.neon.tech/neondb?sslmode=require&channel_binding=require';

const pool = new Pool({
  connectionString,
  ssl: {
    rejectUnauthorized: false,
  },
  max: 10,
  idleTimeoutMillis: 30000,
});

// Simple in-memory rate limiter per IP (5 requests / 60s)
const rateLimitMap = new Map();
const RATE_LIMIT_WINDOW = 60 * 1000;
const RATE_LIMIT_MAX = 5;

function isRateLimited(ip) {
  const now = Date.now();
  const entry = rateLimitMap.get(ip);
  if (!entry || now - entry.startTime > RATE_LIMIT_WINDOW) {
    rateLimitMap.set(ip, { count: 1, startTime: now });
    return false;
  }
  if (entry.count >= RATE_LIMIT_MAX) {
    return true;
  }
  entry.count++;
  return false;
}

// Cleanup old rate limit entries every 5 minutes
const cleanupTimer = setInterval(() => {
  const now = Date.now();
  for (const [ip, entry] of rateLimitMap.entries()) {
    if (now - entry.startTime > RATE_LIMIT_WINDOW) {
      rateLimitMap.delete(ip);
    }
  }
}, 5 * 60 * 1000);
if (cleanupTimer.unref) {
  cleanupTimer.unref();
}

const ALLOWED_ORIGINS = [
  'https://sms.rootaca.com',
  'https://smart-school-website.web.app',
  'https://smart-school-website.firebaseapp.com',
  'http://localhost:5173',
  'http://localhost:3000',
];

function setCorsHeaders(req, res) {
  const origin = req.headers.origin || '';
  if (ALLOWED_ORIGINS.includes(origin) || !origin) {
    res.setHeader('Access-Control-Allow-Origin', origin || '*');
  } else {
    res.setHeader('Access-Control-Allow-Origin', origin);
  }
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Accept');
  res.setHeader('Access-Control-Max-Age', '86400');
}

// Validation regular expressions
const PHONE_REGEX = /^[\d\s\+\-\(\)\.]{7,30}$/;
const EMAIL_REGEX = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;

module.exports = async function handler(req, res) {
  setCorsHeaders(req, res);

  // Handle preflight OPTIONS request
  if (req.method === 'OPTIONS') {
    return res.status(204).end();
  }

  // Health check endpoint
  if (req.method === 'GET') {
    return res.status(200).json({
      status: 'healthy',
      service: 'Smart School Website Contact API',
      timestamp: new Date().toISOString(),
      endpoint: '/api/v1/contact-submissions',
    });
  }

  if (req.method !== 'POST') {
    return res.status(405).json({
      status: 405,
      title: 'Method Not Allowed',
      detail: `HTTP method ${req.method} is not supported on this endpoint.`,
    });
  }

  // Extract client IP for rate limiting
  const forwardedFor = req.headers['x-forwarded-for'];
  const clientIp = (forwardedFor ? forwardedFor.split(',')[0].trim() : req.socket.remoteAddress) || 'unknown';

  if (isRateLimited(clientIp)) {
    return res.status(429).json({
      status: 429,
      title: 'Too Many Requests',
      detail: 'Rate limit exceeded. Please wait before submitting another request.',
    });
  }

  // Parse payload
  let body = req.body;
  if (typeof body === 'string') {
    try {
      body = JSON.parse(body);
    } catch {
      return res.status(400).json({
        status: 400,
        title: 'Invalid payload',
        detail: 'A valid JSON request body is required.',
      });
    }
  }

  if (!body || typeof body !== 'object') {
    return res.status(400).json({
      status: 400,
      title: 'Invalid payload',
      detail: 'A valid JSON request body is required.',
    });
  }

  // Support both "name" and "fullName"
  const name = (body.name || body.fullName || '').trim();
  const schoolName = (body.schoolName || '').trim();
  const phoneNumber = (body.phoneNumber || '').trim();
  const workEmail = (body.workEmail || '').trim().toLowerCase();
  const message = body.message ? String(body.message).trim() : null;

  // Validation
  const errors = {};

  if (!name) {
    errors.Name = ['Your Name is required.'];
  } else if (name.length > 150) {
    errors.Name = ['Your Name cannot exceed 150 characters.'];
  }

  if (!schoolName) {
    errors.SchoolName = ['School Name is required.'];
  } else if (schoolName.length > 200) {
    errors.SchoolName = ['School Name cannot exceed 200 characters.'];
  }

  if (!phoneNumber) {
    errors.PhoneNumber = ['Phone Number is required.'];
  } else if (phoneNumber.length > 30) {
    errors.PhoneNumber = ['Phone Number cannot exceed 30 characters.'];
  } else if (!PHONE_REGEX.test(phoneNumber)) {
    errors.PhoneNumber = ['Please enter a valid phone number.'];
  }

  if (!workEmail) {
    errors.WorkEmail = ['Work Email is required.'];
  } else if (workEmail.length > 254) {
    errors.WorkEmail = ['Work Email cannot exceed 254 characters.'];
  } else if (!EMAIL_REGEX.test(workEmail)) {
    errors.WorkEmail = ['Please enter a valid work email.'];
  }

  if (message && message.length > 2000) {
    errors.Message = ['Message cannot exceed 2000 characters.'];
  }

  if (Object.keys(errors).length > 0) {
    return res.status(400).json({
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
      title: 'One or more validation errors occurred.',
      status: 400,
      errors,
    });
  }

  // Generate server-controlled UUID and timestamp
  const submissionId = crypto.randomUUID();
  const createdAtUtc = new Date().toISOString();

  try {
    const query = `
      INSERT INTO "ContactSubmissions"
        ("Id", "Name", "SchoolName", "PhoneNumber", "WorkEmail", "Message", "CreatedAtUtc")
      VALUES
        ($1, $2, $3, $4, $5, $6, $7)
    `;

    await pool.query(query, [
      submissionId,
      name,
      schoolName,
      phoneNumber,
      workEmail,
      message,
      createdAtUtc,
    ]);

    // Structured logging (do not log personal messages or sensitive info)
    console.log(`[ContactSubmissions] Created submission ${submissionId} for school: ${schoolName}`);

    return res.status(201).json({
      id: submissionId,
      message: 'Your request has been submitted successfully.',
    });
  } catch (err) {
    console.error('[ContactSubmissions] Database error occurred:', err.message);

    return res.status(500).json({
      status: 500,
      title: 'Internal Server Error',
      detail: 'An unexpected error occurred while processing your request. Please try again later.',
    });
  }
};
