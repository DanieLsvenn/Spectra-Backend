# SpectraGlasses API Manual

Base URL: `/api`

Authentication: JWT Bearer Token in the `Authorization` header as `Bearer <token>`.

Roles: `customer`, `staff`, `manager`, `admin`

Pagination: All paginated endpoints accept `page` (default: 1) and `pageSize` (default: 10, max: 50) query parameters.

---

## 1. AUTH

### POST /api/Auth/login

Authenticates a user and returns a JWT token.

Roles allowed: Public (no authentication required)

Request body:
```json
{
  "email": "string",
  "password": "string"
}
```

Validation:
- `email` is required, must not be empty
- `password` is required, must not be empty
- User account must have status `active`

Response 200:
```json
{
  "token": "token_string",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "d@mail.com",
  "fullName": "Josh",
  "role": "manager"
}
```

Response 400:
```json
{
  "errorCode": "INVALID_REQUEST",
  "message": "Email and password are required"
}
```

Response 401:
```json
{
  "errorCode": "INVALID_CREDENTIALS",
  "message": "Invalid email or password"
}
```

Response 401 (inactive account):
```json
{
  "errorCode": "ACCOUNT_INACTIVE",
  "message": "Your account is not active. Please contact support."
}
```

---

### POST /api/Auth/register

Registers a new customer account.

Roles allowed: Public (no authentication required)

Request body:
```json
{
  "email": "string",
  "password": "string",
  "fullName": "string",
  "phone": "string"
}
```

Validation:
- `email` is required, must be a valid email format
- `password` is required, minimum 6 characters
- `email` must not already be registered

Response 201:
```json
{
  "token": "token_string",
  "userId": "892d812b-0a27-4d50-837d-15ef14639496",
  "email": "camellia@mail.com",
  "fullName": "Camellia",
  "role": "customer"
}
```

Response 400 (duplicate email):
```json
{
  "errorCode": "EMAIL_EXISTS",
  "message": "An account with this email already exists"
}
```

---

### POST /api/Auth/google

Login or register using Google/Firebase authentication.

Roles allowed: Public (no authentication required)

Request body:
```json
{
  "idToken": "string"
}
```

Validation:
- `idToken` is required
- Token must be a valid Google OAuth or Firebase ID token
- For Google tokens, the audience must match the configured client ID
- Email must be verified

Response 200:
```json
{
  "token": "token_string",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@gmail.com",
  "fullName": "User Name",
  "role": "customer"
}
```

Response 401:
```json
{
  "errorCode": "INVALID_TOKEN",
  "message": "Invalid or expired Google token"
}
```

---

## 2. USERS

### GET /api/Users/me

Gets the current authenticated user's profile.

Roles allowed: Any authenticated user

Response 200:
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fullName": "Josh",
  "email": "d@mail.com",
  "phone": "123456789",
  "address": "123 Main St",
  "role": "customer",
  "status": "active",
  "createdAt": "2026-01-01T00:00:00"
}
```

---

### PUT /api/Users/me

Updates the current authenticated user's profile.

Roles allowed: Any authenticated user

Request body:
```json
{
  "fullName": "string",
  "phone": "string",
  "address": "string"
}
```

All fields are optional. Only provided fields will be updated.

Response 200: Updated user object.

---

### GET /api/Users

Gets all users with pagination.

Roles allowed: `admin`, `manager`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of user objects.

---

### GET /api/Users/search

Searches users by search term.

Roles allowed: `admin`, `manager`

Query parameters: `searchTerm`, `page`, `pageSize`

If `searchTerm` is empty, returns all users.

Response 200: Same paginated format as GET /api/Users.

---

### GET /api/Users/role/{role}

Gets users filtered by role.

Roles allowed: `admin`, `manager`

Path parameter `role`: `customer` or `staff` or `manager` or `admin`

Query parameters: `page`, `pageSize`

Response 200: Same paginated format as GET /api/Users.

---

### GET /api/Users/status/{status}

Gets users filtered by status.

Roles allowed: `admin`, `manager`

Path parameter `status`: `active` or `inactive` or `suspended` or `pending`

Query parameters: `page`, `pageSize`

Response 200: Same paginated format as GET /api/Users.

---

### GET /api/Users/{id}

Gets a specific user by ID.

Roles allowed: `admin`, `manager`

Path parameter `id`: GUID of the user.

Response 200: User object.

Response 404: `{ "errorCode": "USER_NOT_FOUND", "message": "User not found" }`

---

### POST /api/Users

Creates a new user.

Roles allowed: `admin`

Request body:
```json
{
  "email": "string",
  "password": "string",
  "fullName": "string",
  "phone": "string",
  "address": "string",
  "role": "string"
}
```

Validation:
- `email` is required
- `password` is required, minimum 6 characters
- `role` must be one of: `customer`, `staff`, `manager`, `admin` (defaults to `customer`)
- `email` must not already be registered

Response 201: Created user object.

---

### PUT /api/Users/{id}

Updates a user's profile.

Roles allowed: `admin`

Request body: `{ "fullName": "string", "phone": "string", "address": "string" }` — all optional.

Response 200: Updated user object.

Response 404: `{ "errorCode": "USER_NOT_FOUND", "message": "User not found" }`

---

### PUT /api/Users/{id}/status

Updates a user's status.

Roles allowed: `admin`

Request body: `{ "status": "string" }` — must be one of: `active`, `inactive`, `suspended`, `pending`

Response 200: Updated user object.

---

### PUT /api/Users/{id}/role

Updates a user's role.

Roles allowed: `admin`

Request body: `{ "role": "string" }` — must be one of: `customer`, `staff`, `manager`, `admin`

Response 200: Updated user object.

---

## 3. FRAMES

### GET /api/Frames

Gets all available frames with pagination.

Roles allowed: Public (no authentication required)

Query parameters: `page`, `pageSize`

Visibility rules:
- Frames with status `available` are always shown
- Frames with status `out_of_stock` are **only** shown if they belong to an active preorder campaign
- Frames with status `inactive` are never shown

Response 200: Paginated list of frames. Each frame includes `brand`, `material`, `shape`, `frameColors`, and `frameMedia`.

---

### GET /api/Frames/{id}

Gets a specific frame by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the frame.

Visibility rules:
- Available frames are always returned
- Out-of-stock frames are returned only if they belong to an active preorder campaign
- Inactive frames are never returned (404)

Response 200: Frame object with details.

Response 404: `{ "errorCode": "FRAME_NOT_FOUND", "message": "Frame not found or is not available" }`

---

### GET /api/Frames/{id}/media

Gets all media (images/videos) for a specific frame.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the frame.

Response 200: List of media items.

Response 404: `{ "errorCode": "FRAME_NOT_FOUND", "message": "Frame not found or is not available" }`

---

### POST /api/Frames

Creates a new frame.

Roles allowed: `manager`

Request body:
```json
{
  "frameName": "string",
  "brandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "materialId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "shapeId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "lensWidth": 0,
  "bridgeWidth": 0,
  "frameWidth": 0,
  "templeLength": 0,
  "size": "string",
  "basePrice": 0.0,
  "stockQuantity": 0,
  "reorderLevel": 0,
  "colorIds": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"]
}
```

Validation:
- `frameName` is required
- `shapeId` is optional — references SHAPE table
- `colorIds` is optional — list of COLOR GUIDs to associate with the frame (first color becomes default)
- Size attributes (lensWidth, bridgeWidth, frameWidth, templeLength) are validated for valid ranges
- `stockQuantity` defaults to 0 if not provided
- `reorderLevel` defaults to 5 if not provided

Response 201: Created frame object (includes `brand`, `material`, `shape`, `frameColors` with nested `color`).

---

### PUT /api/Frames/{id}

Updates an existing frame.

Roles allowed: `manager`

All fields are optional. Only provided fields will be updated. `status` if provided must be one of: `available`, `inactive`, `out_of_stock`. `shapeId` references the SHAPE table. `colorIds` replaces all existing frame colors (first color becomes default).

Response 200: Updated frame object (includes `brand`, `material`, `shape`, `frameColors` with nested `color`).
