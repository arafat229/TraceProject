# TraceProject API Documentation

## Overview
TraceProject is an ASP.NET Core 10 REST API that provides comprehensive employee attendance tracking, access control, and security monitoring features.

**Base URL:** `http://localhost:5000/api`

---

## Authentication

### Login Endpoint
```
POST /api/account/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "password123"
}

Response: 
{
  "status": "success",
  "message": "Login successful",
  "data": {
	"token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
	"user": {
	  "id": 1,
	  "email": "user@example.com",
	  "fullName": "John Doe",
	  "role": "Admin"
	}
  }
}
```

**All subsequent endpoints require:**
```
Authorization: Bearer {token}
```

---

## Endpoints

### 1. EMPLOYEES (`/api/employees`)

#### Create Employee
```
POST /api/employees
Content-Type: application/json
Authorization: Bearer {token}

{
  "email": "john.doe@company.com",
  "fullName": "John Doe",
  "position": "Software Engineer",
  "phoneNumber": "555-1234",
  "employeeNumber": "EMP001",
  "departmentId": 1
}

Response: 201 Created
```

#### Get All Employees (Paginated)
```
GET /api/employees?pageNumber=1&pageSize=10&query=john
Authorization: Bearer {token}

Response: 200 OK
{
  "status": "success",
  "data": {
	"items": [
	  {
		"id": 1,
		"email": "john.doe@company.com",
		"fullName": "John Doe",
		"position": "Software Engineer",
		"phoneNumber": "555-1234",
		"employeeNumber": "EMP001",
		"isActive": true,
		"department": "IT",
		"createdAt": "2025-01-15T10:30:00Z",
		"updatedAt": "2025-01-15T10:30:00Z"
	  }
	],
	"pageNumber": 1,
	"pageSize": 10,
	"totalCount": 50
  }
}
```

#### Get Employee by ID
```
GET /api/employees/{id}
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Employees by Department
```
GET /api/employees/department/{departmentId}?pageNumber=1&pageSize=10
Authorization: Bearer {token}

Response: 200 OK
```

#### Update Employee
```
PUT /api/employees/{id}
Content-Type: application/json
Authorization: Bearer {token}

{
  "email": "john.doe@company.com",
  "fullName": "John Doe Updated",
  "position": "Senior Engineer",
  "phoneNumber": "555-5678",
  "employeeNumber": "EMP001",
  "departmentId": 1
}

Response: 200 OK
```

#### Toggle Employee Activation
```
PUT /api/employees/{id}/activate?activate=true
Authorization: Bearer {token}

Response: 200 OK
```

#### Delete Employee
```
DELETE /api/employees/{id}
Authorization: Bearer {token}

Response: 200 OK
```

#### Search Employees
```
GET /api/employees/search?query=john&pageNumber=1&pageSize=10
Authorization: Bearer {token}

Response: 200 OK
```

---

### 2. DEPARTMENTS (`/api/setups/departments`)

#### Create Department
```
POST /api/setups/departments
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "Human Resources",
  "isActive": true
}

Response: 201 Created
```

#### Get All Departments (Paginated)
```
GET /api/setups/departments?pageNumber=1&pageSize=10&query=hr
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Department by ID
```
GET /api/setups/departments/{id}
Authorization: Bearer {token}

Response: 200 OK
```

#### Update Department
```
PUT /api/setups/departments/{id}
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "Human Resources Updated",
  "isActive": true
}

Response: 200 OK
```

#### Delete Department
```
DELETE /api/setups/departments/{id}
Authorization: Bearer {token}

Response: 200 OK
```

---

### 3. CAMERAS (`/api/cameras`)

#### Create Camera
```
POST /api/cameras
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "Main Entrance",
  "location": "Front Gate",
  "ipAddress": "192.168.1.100",
  "isActive": true
}

Response: 201 Created
```

#### Get All Cameras (Paginated)
```
GET /api/cameras?pageNumber=1&pageSize=10
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Camera by ID
```
GET /api/cameras/{id}
Authorization: Bearer {token}

Response: 200 OK
```

#### Update Camera
```
PUT /api/cameras/{id}
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "Main Entrance Updated",
  "location": "Front Gate",
  "ipAddress": "192.168.1.100",
  "isActive": true
}

Response: 200 OK
```

#### Toggle Camera Activation
```
PUT /api/cameras/{id}/activate?activate=false
Authorization: Bearer {token}

Response: 200 OK
```

#### Delete Camera
```
DELETE /api/cameras/{id}
Authorization: Bearer {token}

Response: 200 OK
```

---

### 4. LOCATIONS (`/api/locations`)

#### Create Location
```
POST /api/locations
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "Building A",
  "description": "Main office building",
  "address": "123 Main St",
  "isActive": true
}

Response: 201 Created
```

#### Get All Locations (Paginated)
```
GET /api/locations?pageNumber=1&pageSize=10&query=building
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Location by ID
```
GET /api/locations/{id}
Authorization: Bearer {token}

Response: 200 OK
```

#### Update Location
```
PUT /api/locations/{id}
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "Building A Updated",
  "description": "Main office building updated",
  "address": "123 Main St",
  "isActive": true
}

Response: 200 OK
```

#### Delete Location
```
DELETE /api/locations/{id}
Authorization: Bearer {token}

Response: 200 OK
```

---

### 5. ATTENDANCES (`/api/attendances`)

#### Record Attendance
```
POST /api/attendances
Content-Type: application/json
Authorization: Bearer {token}

{
  "employeeId": 1,
  "locationId": 1,
  "checkInTime": "2025-01-15T09:00:00Z",
  "checkOutTime": "2025-01-15T17:00:00Z"
}

Response: 201 Created
```

#### Get Attendances (Filtered & Paginated)
```
GET /api/attendances?pageNumber=1&pageSize=10&startDate=2025-01-01&endDate=2025-01-31&employeeId=1&locationId=1
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Attendance by ID
```
GET /api/attendances/{id}
Authorization: Bearer {token}

Response: 200 OK
```

#### Update Attendance
```
PUT /api/attendances/{id}
Content-Type: application/json
Authorization: Bearer {token}

{
  "employeeId": 1,
  "locationId": 1,
  "checkInTime": "2025-01-15T08:30:00Z",
  "checkOutTime": "2025-01-15T17:30:00Z"
}

Response: 200 OK
```

#### Get Employee Attendance
```
GET /api/attendances/employee/{employeeId}?pageNumber=1&pageSize=10
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Location Attendance
```
GET /api/attendances/location/{locationId}?pageNumber=1&pageSize=10
Authorization: Bearer {token}

Response: 200 OK
```

#### Delete Attendance
```
DELETE /api/attendances/{id}
Authorization: Bearer {token}

Response: 200 OK
```

---

### 6. ACTIVITIES (`/api/activities`)

#### Log Activity
```
POST /api/activities
Content-Type: application/json
Authorization: Bearer {token}

{
  "employeeId": 1,
  "activityType": "Login",
  "description": "Employee logged in",
  "timestamp": "2025-01-15T09:00:00Z"
}

Response: 201 Created
```

#### Get Activities (Paginated)
```
GET /api/activities?pageNumber=1&pageSize=10
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Activity by ID
```
GET /api/activities/{id}
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Employee Activity
```
GET /api/activities/employee/{employeeId}?pageNumber=1&pageSize=10
Authorization: Bearer {token}

Response: 200 OK
```

#### Update Activity
```
PUT /api/activities/{id}
Content-Type: application/json
Authorization: Bearer {token}

{
  "employeeId": 1,
  "activityType": "Logout",
  "description": "Employee logged out"
}

Response: 200 OK
```

#### Deactivate Activity
```
PUT /api/activities/{id}/deactivate
Authorization: Bearer {token}

Response: 200 OK
```

#### Delete Activity
```
DELETE /api/activities/{id}
Authorization: Bearer {token}

Response: 200 OK
```

---

### 7. REPORTS (`/api/reports`)

#### Generate Report
```
POST /api/reports
Content-Type: application/json
Authorization: Bearer {token}

{
  "employeeId": 1,
  "reportType": "Monthly",
  "startDate": "2025-01-01",
  "endDate": "2025-01-31"
}

Response: 201 Created
```

#### Get Reports (Paginated & Filtered)
```
GET /api/reports?pageNumber=1&pageSize=10&startDate=2025-01-01&endDate=2025-01-31&employeeId=1&reportType=Monthly
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Report by ID
```
GET /api/reports/{id}
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Employee Reports
```
GET /api/reports/employee/{employeeId}
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Department Reports
```
GET /api/reports/department/{departmentId}
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Reports by Date Range
```
GET /api/reports/by-date-range?startDate=2025-01-01&endDate=2025-01-31
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Reports by Type
```
GET /api/reports/by-type/Monthly
Authorization: Bearer {token}

Response: 200 OK
```

#### Delete Report
```
DELETE /api/reports/{id}
Authorization: Bearer {token}

Response: 200 OK
```

---

### 8. UNKNOWN FACES (`/api/unknownfaces`)

#### Add Unknown Face Detection
```
POST /api/unknownfaces
Content-Type: application/json
Authorization: Bearer {token}

{
  "faceImagePath": "/images/face_001.jpg",
  "detectionTime": "2025-01-15T10:30:00Z",
  "confidence": 0.95
}

Response: 201 Created
```

#### Get Unknown Faces (Paginated)
```
GET /api/unknownfaces?pageNumber=1&pageSize=10
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Unknown Face by ID
```
GET /api/unknownfaces/{id}
Authorization: Bearer {token}

Response: 200 OK
```

#### Add Location to Unknown Face
```
POST /api/unknownfaces/{id}/locations/{locationId}
Authorization: Bearer {token}

Response: 200 OK
```

#### Remove Location from Unknown Face
```
DELETE /api/unknownfaces/{id}/locations/{locationId}
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Unknown Faces by Location
```
GET /api/unknownfaces/location/{locationId}
Authorization: Bearer {token}

Response: 200 OK
```

#### Delete Unknown Face
```
DELETE /api/unknownfaces/{id}
Authorization: Bearer {token}

Response: 200 OK
```

---

### 9. DASHBOARD (`/api/dashboard`)

#### Get Dashboard Summary
```
GET /api/dashboard/summary
Authorization: Bearer {token}

Response: 200 OK
{
  "totalEmployees": 50,
  "totalDepartments": 5,
  "totalCameras": 10,
  "totalUsers": 15,
  "todayAttendance": 45,
  "unknownFaceDetections": 2
}
```

#### Get Attendance Trends
```
GET /api/dashboard/attendance-trends?startDate=2025-01-01&endDate=2025-01-31
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Department Statistics
```
GET /api/dashboard/department-statistics
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Employee Attendance Summary
```
GET /api/dashboard/employee-attendance/{employeeId}
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Location Activity
```
GET /api/dashboard/location-activity/{locationId}
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Activity Status
```
GET /api/dashboard/activity-status
Authorization: Bearer {token}

Response: 200 OK
```

#### Get Security Alerts
```
GET /api/dashboard/security-alerts
Authorization: Bearer {token}

Response: 200 OK
```

#### Get User Activity Log
```
GET /api/dashboard/user-activity-log
Authorization: Bearer {token}

Response: 200 OK
```

---

### 10. USER ACCOUNT (`/api/account`)

#### Create User
```
POST /api/account/users
Content-Type: application/json

{
  "email": "admin@company.com",
  "fullName": "Admin User",
  "role": "Admin"
}

Response: 201 Created
```

#### Set User Password
```
POST /api/account/users/set-password
Content-Type: application/json

{
  "email": "admin@company.com",
  "password": "SecurePass123!"
}

Response: 200 OK
```

#### Get All Users (Paginated)
```
GET /api/account/users?pageNumber=1&pageSize=10
Authorization: Bearer {token}

Response: 200 OK
```

#### Get User by ID
```
GET /api/account/users/{id}
Authorization: Bearer {token}

Response: 200 OK
```

#### Update User
```
PUT /api/account/users/{id}
Content-Type: application/json
Authorization: Bearer {token}

{
  "email": "admin@company.com",
  "fullName": "Admin User Updated",
  "role": "SuperAdmin"
}

Response: 200 OK
```

#### Change Password
```
POST /api/account/users/change-password
Content-Type: application/json
Authorization: Bearer {token}

{
  "oldPassword": "OldPass123!",
  "newPassword": "NewPass123!"
}

Response: 200 OK
```

#### Delete User
```
DELETE /api/account/users/{id}
Authorization: Bearer {token}

Response: 200 OK
```

#### Logout
```
POST /api/account/logout
Authorization: Bearer {token}

Response: 200 OK
```

---

## Response Format

All endpoints follow this standard response format:

### Success Response
```json
{
  "status": "success",
  "message": "Operation successful",
  "data": {
	// Response data here
  }
}
```

### Error Response
```json
{
  "status": "error",
  "message": "Error description",
  "data": null
}
```

---

## Pagination

List endpoints support pagination with the following query parameters:

- `pageNumber` (default: 1)
- `pageSize` (default: 10)
- `query` (optional search term for applicable endpoints)

Example:
```
GET /api/employees?pageNumber=1&pageSize=20&query=john
```

---

## Database Models

### Employee
```csharp
{
  Id: long,
  Email: string,
  FullName: string,
  Position: string,
  PhoneNumber: string,
  EmployeeNumber: string,
  DepartmentId: long,
  IsActive: bool,
  CreatedAt: DateTime,
  UpdatedAt: DateTime
}
```

### Attendance
```csharp
{
  Id: long,
  EmployeeId: long,
  LocationId: long,
  CheckInTime: DateTime,
  CheckOutTime: DateTime?,
  CreatedAt: DateTime,
  UpdatedAt: DateTime
}
```

### Report
```csharp
{
  Id: long,
  EmployeeId: long,
  ReportType: enum { Daily, Weekly, Monthly },
  GeneratedAt: DateTime,
  StartDate: DateTime,
  EndDate: DateTime
}
```

---

## Running the API

```bash
cd TraceProject
dotnet run
```

The API will be available at `http://localhost:5000`

---

## CORS Configuration

The API requires CORS to be enabled for frontend communication. Update `Program.cs`:

```csharp
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

---

## Error Codes

| Code | Message |
|------|---------|
| 400 | Bad Request - Invalid input data |
| 401 | Unauthorized - Missing or invalid token |
| 403 | Forbidden - Insufficient permissions |
| 404 | Not Found - Resource not found |
| 500 | Internal Server Error - Server-side error |

---

## Support

For issues or questions, contact the development team.
