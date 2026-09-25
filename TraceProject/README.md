# TraceProject - Complete Backend API

A comprehensive ASP.NET Core 10 REST API for employee attendance tracking, access control, and security monitoring.

## 📋 Project Status: ✅ COMPLETE

All API endpoints have been implemented and tested. The backend is ready for frontend integration.

---

## 🎯 Quick Links

- **[API Documentation](./API_DOCUMENTATION.md)** - Complete endpoint reference
- **[Frontend Integration Guide](./FRONTEND_INTEGRATION_GUIDE.md)** - How to connect with React, Vue, or Angular
- **[Database Models](./TraceProject/Models)** - Entity definitions
- **[Controllers](./TraceProject/Controllers)** - All implemented endpoints

---

## 📦 What's Included

### Backend Features
✅ **Complete REST API** with 10+ controllers  
✅ **User Authentication** with JWT tokens  
✅ **Employee Management** with full CRUD operations  
✅ **Attendance Tracking** with date range and location filters  
✅ **Department Management** with organization hierarchy  
✅ **Security Monitoring** with unknown face detection  
✅ **Activity Logging** for audit trails  
✅ **Report Generation** (Daily, Weekly, Monthly)  
✅ **Dashboard Analytics** with real-time statistics  
✅ **Pagination & Search** on all list endpoints  
✅ **Role-based Authorization** (Admin, Manager, User)  

---

## 🚀 Quick Start

### Prerequisites
- **.NET 10 SDK** installed
- **Visual Studio Community 2026** (or any .NET IDE)
- **SQLite** (included with .NET)

### Step 1: Clone the Repository
```bash
git clone https://github.com/arafat229/TraceProject.git
cd TraceProject
```

### Step 2: Restore Dependencies
```bash
dotnet restore
```

### Step 3: Update Database
```bash
dotnet ef database update
```

### Step 4: Run the API
```bash
cd TraceProject
dotnet run
```

The API will be available at: **`http://localhost:5000`**

---

## 📚 API Endpoints Overview

### Authentication
```
POST   /api/account/login              - User login
POST   /api/account/logout             - User logout
```

### Users
```
POST   /api/account/users              - Create user
GET    /api/account/users              - List users (paginated)
GET    /api/account/users/{id}         - Get user details
PUT    /api/account/users/{id}         - Update user
DELETE /api/account/users/{id}         - Delete user
POST   /api/account/users/change-password - Change password
```

### Employees
```
POST   /api/employees                  - Add employee
GET    /api/employees                  - List employees (paginated, searchable)
GET    /api/employees/{id}             - Get employee details
GET    /api/employees/department/{id}  - Get employees by department
PUT    /api/employees/{id}             - Update employee
PUT    /api/employees/{id}/activate    - Toggle activation
DELETE /api/employees/{id}             - Delete employee
```

### Departments
```
POST   /api/setups/departments         - Create department
GET    /api/setups/departments         - List departments (paginated)
GET    /api/setups/departments/{id}    - Get department details
PUT    /api/setups/departments/{id}    - Update department
DELETE /api/setups/departments/{id}    - Delete department
```

### Locations
```
POST   /api/locations                  - Add location
GET    /api/locations                  - List locations (paginated)
GET    /api/locations/{id}             - Get location details
PUT    /api/locations/{id}             - Update location
DELETE /api/locations/{id}             - Delete location
```

### Cameras
```
POST   /api/cameras                    - Add camera
GET    /api/cameras                    - List cameras (paginated)
GET    /api/cameras/{id}               - Get camera details
PUT    /api/cameras/{id}               - Update camera
PUT    /api/cameras/{id}/activate      - Toggle activation
DELETE /api/cameras/{id}               - Delete camera
```

### Attendances
```
POST   /api/attendances                      - Record attendance
GET    /api/attendances                      - List attendances (paginated, filtered)
GET    /api/attendances/{id}                 - Get attendance details
GET    /api/attendances/employee/{id}        - Get employee attendance
GET    /api/attendances/location/{id}        - Get location attendance
PUT    /api/attendances/{id}                 - Update attendance
DELETE /api/attendances/{id}                 - Delete attendance
```

### Activities
```
POST   /api/activities                 - Log activity
GET    /api/activities                 - List activities (paginated)
GET    /api/activities/{id}            - Get activity details
GET    /api/activities/employee/{id}   - Get employee activities
PUT    /api/activities/{id}            - Update activity
PUT    /api/activities/{id}/deactivate - Deactivate activity
DELETE /api/activities/{id}            - Delete activity
```

### Reports
```
POST   /api/reports                           - Generate report
GET    /api/reports                           - List reports (paginated, filtered)
GET    /api/reports/{id}                      - Get report details
GET    /api/reports/employee/{id}             - Get employee reports
GET    /api/reports/department/{id}           - Get department reports
GET    /api/reports/by-date-range             - Reports by date range
GET    /api/reports/by-type/{type}            - Reports by type
DELETE /api/reports/{id}                      - Delete report
```

### Unknown Faces
```
POST   /api/unknownfaces                      - Add unknown face
GET    /api/unknownfaces                      - List unknown faces (paginated)
GET    /api/unknownfaces/{id}                 - Get face details
GET    /api/unknownfaces/location/{id}        - Get faces by location
POST   /api/unknownfaces/{id}/locations/{locId} - Link face to location
DELETE /api/unknownfaces/{id}/locations/{locId} - Unlink face from location
DELETE /api/unknownfaces/{id}                 - Delete unknown face
```

### Dashboard
```
GET    /api/dashboard/summary                 - Dashboard overview
GET    /api/dashboard/attendance-trends       - Attendance trends
GET    /api/dashboard/department-statistics   - Department stats
GET    /api/dashboard/employee-attendance/{id} - Employee attendance summary
GET    /api/dashboard/location-activity/{id}  - Location activity
GET    /api/dashboard/activity-status         - Current activities
GET    /api/dashboard/security-alerts         - Security alerts
GET    /api/dashboard/user-activity-log       - User activity log
```

---

## 🔐 Authentication

All protected endpoints require a JWT token in the Authorization header:

```
Authorization: Bearer {token}
```

### Get Token
```bash
POST /api/account/login
Content-Type: application/json

{
  "email": "admin@company.com",
  "password": "password123"
}
```

---

## 🗄️ Database Schema

The application uses **Entity Framework Core** with **SQLite**. Key entities:

- **User** - System users with roles
- **Employee** - Company employees
- **Department** - Organizational departments
- **Location** - Physical locations
- **Camera** - Surveillance cameras
- **Attendance** - Employee check-in/out records
- **Activity** - Employee activities and logs
- **Report** - Generated reports
- **UnknownFace** - Detected unknown faces
- **FaceProfile** - Facial recognition profiles

---

## 📖 Project Structure

```
TraceProject/
├── Controllers/           # API endpoints
│   ├── AccountController.cs
│   ├── EmployeesController.cs
│   ├── LocationsController.cs
│   ├── CamerasController.cs
│   ├── AttendancesController.cs
│   ├── ActivitiesController.cs
│   ├── ReportsController.cs
│   ├── DashboardController.cs
│   ├── UnknownFacesController.cs
│   └── SetUpsController.cs
├── Models/               # Database entities
│   ├── User.cs
│   ├── Employee.cs
│   ├── Department.cs
│   ├── Location.cs
│   ├── Camera.cs
│   ├── Attendance.cs
│   ├── Activity.cs
│   ├── Report.cs
│   └── UnknownFace.cs
├── Dtos/                # Data transfer objects
│   ├── AccountDtos.cs
│   └── BusinessDtos.cs
├── Data/
│   └── ApplicationDbContext.cs  # EF Core context
├── Migrations/          # Database migrations
├── Helpers/            # Utility classes
├── Program.cs          # Application startup
└── appsettings.json    # Configuration
```

---

## 💻 Frontend Integration

The API is fully compatible with any modern frontend framework:

- **React** - See examples in `FRONTEND_INTEGRATION_GUIDE.md`
- **Vue 3** - See examples in `FRONTEND_INTEGRATION_GUIDE.md`
- **Angular** - See examples in `FRONTEND_INTEGRATION_GUIDE.md`
- **Mobile** (React Native, Flutter) - Uses same REST endpoints

### Example: React Integration

```typescript
import axios from 'axios';

const api = axios.create({
  baseURL: 'http://localhost:5000/api',
});

// Add token to requests
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
	config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Get employees
const response = await api.get('/employees', {
  params: { pageNumber: 1, pageSize: 10 }
});
```

---

## ⚙️ Configuration

Update `appsettings.json`:

```json
{
  "AppSettings": {
	"Token": "your-secret-key-here"
  },
  "ConnectionStrings": {
	"DefaultConnection": "Data Source=traceproject.db"
  },
  "Logging": {
	"LogLevel": {
	  "Default": "Information"
	}
  }
}
```

---

## 🔄 CORS Configuration

The API includes CORS configuration for frontend communication:

```csharp
// In Program.cs
builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowFrontend", policy =>
	{
		policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
			  .AllowAnyMethod()
			  .AllowAnyHeader()
			  .AllowCredentials();
	});
});

app.UseCors("AllowFrontend");
```

Update the `WithOrigins()` with your frontend URL.

---

## 🧪 Testing Endpoints

### Using cURL
```bash
# Login
curl -X POST http://localhost:5000/api/account/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@company.com","password":"password"}'

# Get employees (requires token)
curl -X GET http://localhost:5000/api/employees \
  -H "Authorization: Bearer {token}"
```

### Using Postman
1. Import the API_DOCUMENTATION.md endpoints
2. Set `{{base_url}}` = `http://localhost:5000/api`
3. Set `{{token}}` in environment variables
4. Test each endpoint

### Using Thunder Client (VS Code)
1. Install Thunder Client extension
2. Create new request
3. Use examples from API_DOCUMENTATION.md

---

## 📝 Response Format

All endpoints follow a standard response format:

### Success
```json
{
  "status": "success",
  "message": "Operation successful",
  "data": { /* response data */ }
}
```

### Error
```json
{
  "status": "error",
  "message": "Error description",
  "data": null
}
```

---

## 🚨 Error Codes

| Code | Message |
|------|---------|
| 400 | Bad Request - Invalid input |
| 401 | Unauthorized - Missing/invalid token |
| 403 | Forbidden - Insufficient permissions |
| 404 | Not Found - Resource doesn't exist |
| 500 | Internal Server Error |

---

## 📦 Dependencies

- **ASP.NET Core 10**
- **Entity Framework Core**
- **SQLite**
- **HlkHelpers** (Custom library)
- **Serilog** (Logging)
- **JWT** (Authentication)

---

## 🔗 Sharing Your Code

### Option 1: ZIP File
Your code has been packaged as `TraceProject-Backup.zip`

```bash
# To extract:
Expand-Archive -Path TraceProject-Backup.zip -DestinationPath .
```

### Option 2: GitHub
Push to GitHub and share the repository URL:
```bash
git push origin master
```

### Option 3: Share with Claude
- Upload `TraceProject-Backup.zip` to Claude
- Or paste individual files for code review
- Provide the API_DOCUMENTATION.md for context

---

## 🎓 Next Steps

1. **Review the API** - Check `API_DOCUMENTATION.md` for all endpoints
2. **Set up frontend** - Follow `FRONTEND_INTEGRATION_GUIDE.md`
3. **Test endpoints** - Use Postman or Thunder Client
4. **Integrate with frontend** - Build UI components
5. **Deploy to production** - Set up Azure or your hosting

---

## 📧 Support

- Issues? Check the troubleshooting section in `FRONTEND_INTEGRATION_GUIDE.md`
- Need help? Review the examples in the guide
- Want to contribute? Submit a pull request

---

## 📄 License

This project is part of the TraceProject system.

---

## 🎉 Summary

Your backend API is **fully implemented and production-ready**:

✅ **10 Controllers** with comprehensive endpoints  
✅ **All CRUD operations** implemented  
✅ **Pagination & Filtering** on list endpoints  
✅ **JWT Authentication** configured  
✅ **Role-based Authorization** ready  
✅ **Error handling** throughout  
✅ **Database migrations** prepared  
✅ **CORS enabled** for frontend communication  

**Start building your frontend and enjoy seamless API integration!** 🚀

---

Generated: January 2025  
Version: 1.0  
Status: Production Ready ✅
