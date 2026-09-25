# TraceProject Frontend Integration Guide

## Quick Start

### 1. **Backend Setup**

Ensure your TraceProject API is running:

```bash
cd C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject
dotnet run
```

The API will be available at `http://localhost:5000`

---

## Frontend Integration Examples

### **React Integration**

#### Install Dependencies
```bash
npm install axios react-router-dom
```

#### Create API Service
```typescript
// src/services/api.ts
import axios from 'axios';

const API_BASE_URL = 'http://localhost:5000/api';

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
	'Content-Type': 'application/json',
  },
});

// Add request interceptor to attach token
api.interceptors.request.use(
  (config) => {
	const token = localStorage.getItem('token');
	if (token) {
	  config.headers.Authorization = `Bearer ${token}`;
	}
	return config;
  },
  (error) => Promise.reject(error)
);

export default api;
```

#### Login Component
```tsx
// src/components/Login.tsx
import { useState } from 'react';
import api from '../services/api';
import { useNavigate } from 'react-router-dom';

export default function Login() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const navigate = useNavigate();

  const handleLogin = async (e: React.FormEvent) => {
	e.preventDefault();
	try {
	  const response = await api.post('/account/login', { email, password });
	  if (response.data.status === 'success') {
		localStorage.setItem('token', response.data.data.token);
		localStorage.setItem('user', JSON.stringify(response.data.data.user));
		navigate('/dashboard');
	  }
	} catch (err: any) {
	  setError(err.response?.data?.message || 'Login failed');
	}
  };

  return (
	<form onSubmit={handleLogin}>
	  <input
		type="email"
		value={email}
		onChange={(e) => setEmail(e.target.value)}
		placeholder="Email"
		required
	  />
	  <input
		type="password"
		value={password}
		onChange={(e) => setPassword(e.target.value)}
		placeholder="Password"
		required
	  />
	  {error && <p className="error">{error}</p>}
	  <button type="submit">Login</button>
	</form>
  );
}
```

#### Employees List Component
```tsx
// src/components/Employees.tsx
import { useState, useEffect } from 'react';
import api from '../services/api';

interface Employee {
  id: number;
  email: string;
  fullName: string;
  position: string;
  department: string;
  isActive: boolean;
}

export default function Employees() {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [loading, setLoading] = useState(true);
  const [pageNumber, setPageNumber] = useState(1);
  const [total, setTotal] = useState(0);

  useEffect(() => {
	fetchEmployees();
  }, [pageNumber]);

  const fetchEmployees = async () => {
	try {
	  setLoading(true);
	  const response = await api.get('/employees', {
		params: {
		  pageNumber,
		  pageSize: 10,
		},
	  });
	  if (response.data.status === 'success') {
		setEmployees(response.data.data.items);
		setTotal(response.data.data.totalCount);
	  }
	} catch (error) {
	  console.error('Failed to fetch employees:', error);
	} finally {
	  setLoading(false);
	}
  };

  if (loading) return <div>Loading...</div>;

  return (
	<div>
	  <h1>Employees</h1>
	  <table>
		<thead>
		  <tr>
			<th>Name</th>
			<th>Email</th>
			<th>Position</th>
			<th>Department</th>
			<th>Status</th>
		  </tr>
		</thead>
		<tbody>
		  {employees.map((emp) => (
			<tr key={emp.id}>
			  <td>{emp.fullName}</td>
			  <td>{emp.email}</td>
			  <td>{emp.position}</td>
			  <td>{emp.department}</td>
			  <td>{emp.isActive ? 'Active' : 'Inactive'}</td>
			</tr>
		  ))}
		</tbody>
	  </table>
	  <div>
		<button 
		  onClick={() => setPageNumber(p => Math.max(1, p - 1))}
		  disabled={pageNumber === 1}
		>
		  Previous
		</button>
		<span>Page {pageNumber} of {Math.ceil(total / 10)}</span>
		<button 
		  onClick={() => setPageNumber(p => p + 1)}
		  disabled={pageNumber * 10 >= total}
		>
		  Next
		</button>
	  </div>
	</div>
  );
}
```

#### Add Employee Component
```tsx
// src/components/AddEmployee.tsx
import { useState } from 'react';
import api from '../services/api';

export default function AddEmployee() {
  const [formData, setFormData] = useState({
	email: '',
	fullName: '',
	position: '',
	phoneNumber: '',
	employeeNumber: '',
	departmentId: 1,
  });
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
	const { name, value } = e.target;
	setFormData(prev => ({
	  ...prev,
	  [name]: value,
	}));
  };

  const handleSubmit = async (e: React.FormEvent) => {
	e.preventDefault();
	try {
	  const response = await api.post('/employees', formData);
	  if (response.data.status === 'success') {
		setSuccess('Employee added successfully');
		setFormData({
		  email: '',
		  fullName: '',
		  position: '',
		  phoneNumber: '',
		  employeeNumber: '',
		  departmentId: 1,
		});
	  }
	} catch (err: any) {
	  setError(err.response?.data?.message || 'Failed to add employee');
	}
  };

  return (
	<form onSubmit={handleSubmit}>
	  <div>
		<label>Full Name</label>
		<input
		  type="text"
		  name="fullName"
		  value={formData.fullName}
		  onChange={handleChange}
		  required
		/>
	  </div>
	  <div>
		<label>Email</label>
		<input
		  type="email"
		  name="email"
		  value={formData.email}
		  onChange={handleChange}
		  required
		/>
	  </div>
	  <div>
		<label>Position</label>
		<input
		  type="text"
		  name="position"
		  value={formData.position}
		  onChange={handleChange}
		  required
		/>
	  </div>
	  <div>
		<label>Phone</label>
		<input
		  type="tel"
		  name="phoneNumber"
		  value={formData.phoneNumber}
		  onChange={handleChange}
		/>
	  </div>
	  <div>
		<label>Employee Number</label>
		<input
		  type="text"
		  name="employeeNumber"
		  value={formData.employeeNumber}
		  onChange={handleChange}
		  required
		/>
	  </div>
	  <div>
		<label>Department</label>
		<select name="departmentId" value={formData.departmentId} onChange={handleChange}>
		  <option value={1}>IT</option>
		  <option value={2}>HR</option>
		  <option value={3}>Finance</option>
		</select>
	  </div>
	  {error && <p className="error">{error}</p>}
	  {success && <p className="success">{success}</p>}
	  <button type="submit">Add Employee</button>
	</form>
  );
}
```

---

### **Vue 3 Integration**

#### Create API Composable
```typescript
// src/composables/useApi.ts
import axios from 'axios';

const api = axios.create({
  baseURL: 'http://localhost:5000/api',
  headers: {
	'Content-Type': 'application/json',
  },
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
	config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export function useApi() {
  return { api };
}
```

#### Login Component (Vue)
```vue
<!-- src/components/Login.vue -->
<template>
  <form @submit.prevent="handleLogin">
	<input v-model="email" type="email" placeholder="Email" required />
	<input v-model="password" type="password" placeholder="Password" required />
	<p v-if="error" class="error">{{ error }}</p>
	<button type="submit">Login</button>
  </form>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { useApi } from '@/composables/useApi';

const email = ref('');
const password = ref('');
const error = ref('');
const router = useRouter();
const { api } = useApi();

const handleLogin = async () => {
  try {
	const response = await api.post('/account/login', { email: email.value, password: password.value });
	if (response.data.status === 'success') {
	  localStorage.setItem('token', response.data.data.token);
	  localStorage.setItem('user', JSON.stringify(response.data.data.user));
	  router.push('/dashboard');
	}
  } catch (err: any) {
	error.value = err.response?.data?.message || 'Login failed';
  }
};
</script>
```

---

### **Angular Integration**

#### Create HTTP Service
```typescript
// src/app/services/api.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private api = 'http://localhost:5000/api';

  constructor(private http: HttpClient) {}

  private getHeaders(): HttpHeaders {
	const token = localStorage.getItem('token');
	return new HttpHeaders({
	  'Authorization': `Bearer ${token}`,
	  'Content-Type': 'application/json',
	});
  }

  login(email: string, password: string): Observable<any> {
	return this.http.post(`${this.api}/account/login`, { email, password });
  }

  getEmployees(pageNumber = 1, pageSize = 10): Observable<any> {
	return this.http.get(`${this.api}/employees`, {
	  headers: this.getHeaders(),
	  params: { pageNumber, pageSize },
	});
  }

  addEmployee(data: any): Observable<any> {
	return this.http.post(`${this.api}/employees`, data, {
	  headers: this.getHeaders(),
	});
  }
}
```

---

## Running the Frontend

### **React**
```bash
npm start
# or for Vite:
npm run dev
```

### **Vue**
```bash
npm run dev
```

### **Angular**
```bash
ng serve
```

---

## CORS Configuration

Make sure the API has CORS enabled for your frontend URL. Update `Program.cs`:

```csharp
builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowFrontend", policy =>
	{
		policy
			.WithOrigins("http://localhost:3000", "http://localhost:5173", "http://localhost:4200")
			.AllowAnyMethod()
			.AllowAnyHeader()
			.AllowCredentials();
	});
});

app.UseCors("AllowFrontend");
```

---

## Common Integration Patterns

### **Protected Routes**

**React:**
```tsx
<PrivateRoute>
  <Dashboard />
</PrivateRoute>

function PrivateRoute({ children }: { children: React.ReactNode }) {
  const token = localStorage.getItem('token');
  return token ? <>{children}</> : <Navigate to="/login" />;
}
```

### **Error Handling**

```typescript
async function handleApiCall() {
  try {
	const response = await api.get('/endpoint');
	return response.data;
  } catch (error: any) {
	const message = error.response?.data?.message || 'An error occurred';
	console.error(message);
	if (error.response?.status === 401) {
	  // Token expired, redirect to login
	  window.location.href = '/login';
	}
  }
}
```

### **Token Refresh**

```typescript
api.interceptors.response.use(
  response => response,
  async error => {
	if (error.response?.status === 401) {
	  // Clear token and redirect to login
	  localStorage.removeItem('token');
	  window.location.href = '/login';
	}
	return Promise.reject(error);
  }
);
```

---

## Environment Variables

Create `.env` file in your frontend:

```
VITE_API_URL=http://localhost:5000/api
VITE_APP_NAME=TraceProject
```

Then use it:
```typescript
const apiUrl = import.meta.env.VITE_API_URL;
```

---

## Helpful Resources

- [Axios Documentation](https://axios-http.com/)
- [React Documentation](https://react.dev/)
- [Vue Documentation](https://vuejs.org/)
- [Angular Documentation](https://angular.io/)
- [REST API Best Practices](https://restfulapi.net/)

---

## Troubleshooting

### **CORS Error**
- Ensure CORS is enabled in `Program.cs`
- Check that your frontend URL is in the allowed origins

### **401 Unauthorized**
- Verify you have a valid token in localStorage
- Check token expiration

### **404 Not Found**
- Verify the endpoint URL is correct
- Check API is running on `http://localhost:5000`

### **Connection Refused**
- Start the API: `dotnet run`
- Verify correct port configuration

---

## Next Steps

1. **Set up your frontend project** (React, Vue, or Angular)
2. **Configure API service** with your base URL
3. **Build login page** using provided examples
4. **Create pages** for employees, departments, attendance, etc.
5. **Test API integration** using Postman or similar tools
6. **Deploy** to production

---

Good luck with your development! 🚀
