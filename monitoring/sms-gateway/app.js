// ============================================================================
// SMS GATEWAY SERVICE FOR ERP PROMETHEUS ALERTS
// ============================================================================
// This Node.js application serves as a notification gateway for Prometheus
// alerts. It receives webhook notifications from Alertmanager and sends
// SMS messages and emails to configured recipients.
//
// Features:
// - SMS notifications via Twilio (configurable for other providers)
// - Email notifications via SMTP
// - Basic authentication for security
// - Health check endpoint for monitoring
// - Test endpoints for validation
//
// Environment Variables:
// - TWILIO_ACCOUNT_SID, TWILIO_AUTH_TOKEN: Twilio credentials
// - SMS_FROM_NUMBER, SMS_TO_NUMBERS: Phone number configuration
// - SMTP_HOST, SMTP_PORT, SMTP_USER, SMTP_PASS: Email configuration
// ============================================================================

const express = require('express');        // Web framework for handling HTTP requests
const axios = require('axios');            // HTTP client for external API calls
const bodyParser = require('body-parser'); // Middleware for parsing JSON requests

const app = express();
app.use(bodyParser.json());                // Parse JSON request bodies

// ============================================================================
// CONFIGURATION - SMS PROVIDER SETTINGS
// ============================================================================
// SMS Configuration - Replace with your SMS provider details
const SMS_CONFIG = {
  provider: 'twilio',                                                    // SMS provider name
  accountSid: process.env.TWILIO_ACCOUNT_SID || 'your-account-sid',     // Twilio Account SID
  authToken: process.env.TWILIO_AUTH_TOKEN || 'your-auth-token',        // Twilio Auth Token
  fromNumber: process.env.SMS_FROM_NUMBER || '+1234567890',             // Sender phone number
  toNumbers: (process.env.SMS_TO_NUMBERS || '+1234567890').split(',')   // Recipient phone numbers (comma-separated)
};

// ============================================================================
// CONFIGURATION - EMAIL SMTP SETTINGS
// ============================================================================
// Email Configuration for SMTP notifications
const EMAIL_CONFIG = {
  smtp_host: process.env.SMTP_HOST || 'smtp.gmail.com',                 // SMTP server hostname
  smtp_port: process.env.SMTP_PORT || 587,                             // SMTP port (587 for TLS)
  smtp_user: process.env.SMTP_USER || 'your-email@gmail.com',          // SMTP username/email
  smtp_pass: process.env.SMTP_PASS || 'your-app-password',             // SMTP password/app password
  to_emails: (process.env.EMAIL_TO || 'admin@erpprototype.com').split(',') // Recipient emails (comma-separated)
};

// ============================================================================
// AUTHENTICATION MIDDLEWARE
// ============================================================================
// Basic authentication middleware to secure SMS endpoints
// Uses hardcoded credentials for simplicity (consider using environment variables)
const authenticate = (req, res, next) => {
  const auth = req.headers.authorization;
  
  // Check if Authorization header is present and properly formatted
  if (!auth || !auth.startsWith('Basic ')) {
    return res.status(401).json({ error: 'Authentication required' });
  }
  
  // Decode base64 credentials and validate
  const credentials = Buffer.from(auth.slice(6), 'base64').toString().split(':');
  const username = credentials[0];
  const password = credentials[1];
  
  // Validate credentials (admin:sms-secret-key)
  if (username !== 'admin' || password !== 'sms-secret-key') {
    return res.status(401).json({ error: 'Invalid credentials' });
  }
  
  next(); // Authentication successful, proceed to next middleware
};

// ============================================================================
// SMS SENDING FUNCTIONALITY
// ============================================================================
// SMS sending function using Twilio API
// Supports sending to multiple phone numbers with error handling
async function sendSMS(message, phoneNumbers) {
  const twilio = require('twilio');                                     // Twilio SDK
  const client = twilio(SMS_CONFIG.accountSid, SMS_CONFIG.authToken);  // Initialize Twilio client
  
  // Send SMS to each number in parallel
  const promises = phoneNumbers.map(async (number) => {
    try {
      const result = await client.messages.create({
        body: message,
        from: SMS_CONFIG.fromNumber,
        to: number.trim()
      });
      console.log(`SMS sent to ${number}: ${result.sid}`);
      return { success: true, number, sid: result.sid };
    } catch (error) {
      console.error(`Failed to send SMS to ${number}:`, error.message);
      return { success: false, number, error: error.message };
    }
  });
  
  return Promise.all(promises);
}

// Email sending function
async function sendEmail(subject, message, emailAddresses) {
  const nodemailer = require('nodemailer');
  
  const transporter = nodemailer.createTransporter({
    host: EMAIL_CONFIG.smtp_host,
    port: EMAIL_CONFIG.smtp_port,
    secure: false,
    auth: {
      user: EMAIL_CONFIG.smtp_user,
      pass: EMAIL_CONFIG.smtp_pass
    }
  });
  
  const promises = emailAddresses.map(async (email) => {
    try {
      const result = await transporter.sendMail({
        from: EMAIL_CONFIG.smtp_user,
        to: email.trim(),
        subject: subject,
        text: message,
        html: message.replace(/\n/g, '<br>')
      });
      console.log(`Email sent to ${email}: ${result.messageId}`);
      return { success: true, email, messageId: result.messageId };
    } catch (error) {
      console.error(`Failed to send email to ${email}:`, error.message);
      return { success: false, email, error: error.message };
    }
  });
  
  return Promise.all(promises);
}

// Parse Prometheus alert webhook
function parsePrometheusAlert(alertData) {
  if (!alertData.alerts || alertData.alerts.length === 0) {
    return null;
  }
  
  const alert = alertData.alerts[0];
  const severity = alert.labels.severity || 'unknown';
  const service = alert.labels.service || alert.labels.job || 'unknown service';
  const summary = alert.annotations.summary || 'Service alert';
  const description = alert.annotations.description || 'No description available';
  
  return {
    severity,
    service,
    summary,
    description,
    status: alertData.status,
    timestamp: new Date().toISOString()
  };
}

// Health check endpoint
app.get('/health', (req, res) => {
  res.json({ 
    status: 'healthy', 
    service: 'sms-gateway',
    timestamp: new Date().toISOString(),
    sms_configured: !!SMS_CONFIG.accountSid && SMS_CONFIG.accountSid !== 'your-account-sid',
    email_configured: !!EMAIL_CONFIG.smtp_user && EMAIL_CONFIG.smtp_user !== 'your-email@gmail.com'
  });
});

// Generic webhook endpoint for testing
app.post('/webhook', (req, res) => {
  console.log('Webhook received:', JSON.stringify(req.body, null, 2));
  res.json({ status: 'received', timestamp: new Date().toISOString() });
});

// SMS alert endpoint
app.post('/sms', authenticate, async (req, res) => {
  try {
    const alertData = req.body;
    const parsedAlert = parsePrometheusAlert(alertData);
    
    if (!parsedAlert) {
      return res.status(400).json({ error: 'Invalid alert data' });
    }
    
    const message = `🚨 ERP ALERT 🚨\n${parsedAlert.summary}\nService: ${parsedAlert.service}\nSeverity: ${parsedAlert.severity}\nTime: ${new Date().toLocaleString()}`;
    
    console.log('Sending SMS alert:', message);
    
    // Send SMS to all configured numbers
    const results = await sendSMS(message, SMS_CONFIG.toNumbers);
    
    res.json({
      status: 'sent',
      alert: parsedAlert,
      sms_results: results,
      timestamp: new Date().toISOString()
    });
    
  } catch (error) {
    console.error('SMS alert error:', error);
    res.status(500).json({ 
      error: 'Failed to send SMS alert', 
      details: error.message 
    });
  }
});

// Email alert endpoint
app.post('/email', async (req, res) => {
  try {
    const alertData = req.body;
    const parsedAlert = parsePrometheusAlert(alertData);
    
    if (!parsedAlert) {
      return res.status(400).json({ error: 'Invalid alert data' });
    }
    
    const subject = `ERP System Alert - ${parsedAlert.service}`;
    const message = `
⚠️ ERP System Alert ⚠️

Summary: ${parsedAlert.summary}
Service: ${parsedAlert.service}
Severity: ${parsedAlert.severity}
Description: ${parsedAlert.description}
Status: ${parsedAlert.status}
Time: ${new Date().toLocaleString()}

Dashboard: http://localhost:3000
Prometheus: http://localhost:9090

Please check the system immediately.
    `;
    
    console.log('Sending email alert:', subject);
    
    // Send email to all configured addresses
    const results = await sendEmail(subject, message, EMAIL_CONFIG.to_emails);
    
    res.json({
      status: 'sent',
      alert: parsedAlert,
      email_results: results,
      timestamp: new Date().toISOString()
    });
    
  } catch (error) {
    console.error('Email alert error:', error);
    res.status(500).json({ 
      error: 'Failed to send email alert', 
      details: error.message 
    });
  }
});

// Test SMS endpoint
app.post('/test-sms', authenticate, async (req, res) => {
  try {
    const message = `🧪 ERP Test Alert - ${new Date().toLocaleString()}\nThis is a test message to verify SMS functionality.`;
    const results = await sendSMS(message, SMS_CONFIG.toNumbers);
    
    res.json({
      status: 'test_sent',
      message,
      results,
      timestamp: new Date().toISOString()
    });
  } catch (error) {
    res.status(500).json({ 
      error: 'Test SMS failed', 
      details: error.message 
    });
  }
});

// Test email endpoint
app.post('/test-email', async (req, res) => {
  try {
    const subject = 'ERP Test Alert';
    const message = `🧪 ERP Test Alert - ${new Date().toLocaleString()}\n\nThis is a test message to verify email functionality.\n\nDashboard: http://localhost:3000`;
    const results = await sendEmail(subject, message, EMAIL_CONFIG.to_emails);
    
    res.json({
      status: 'test_sent',
      subject,
      message,
      results,
      timestamp: new Date().toISOString()
    });
  } catch (error) {
    res.status(500).json({ 
      error: 'Test email failed', 
      details: error.message 
    });
  }
});

const PORT = process.env.PORT || 8080;
app.listen(PORT, () => {
  console.log(`🚀 SMS Gateway running on port ${PORT}`);
  console.log(`📱 SMS Provider: ${SMS_CONFIG.provider}`);
  console.log(`📧 Email configured: ${EMAIL_CONFIG.smtp_host}`);
  console.log(`🔗 Health check: http://localhost:${PORT}/health`);
});

module.exports = app;
