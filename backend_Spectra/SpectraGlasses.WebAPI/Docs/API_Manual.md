# SpectraGlasses API Manual

Base URL: `/api`

Authentication: JWT Bearer Token in the `Authorization` header as `Bearer <token>`.

Roles: `customer`, `staff`, `manager`, `admin`

Pagination: All paginated endpoints accept `page` (default: 1) and `pageSize` (default: 10, max: 50) query parameters.

Paginated response format:
```json
{
  "totalItems": 0,
  "totalPages": 0,
  "currentPage": 1,
  "pageSize": 10,
  "items": []
}
```

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

Response 400 (validation):
```json
{
  "errorCode": "VALIDATION_ERROR",
  "message": "Email is required"
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
- If user already exists, account must be `active`

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

Response 400:
```json
{
  "errorCode": "VALIDATION_ERROR",
  "message": "ID token is required"
}
```

Response 401:
```json
{
  "errorCode": "INVALID_TOKEN",
  "message": "Invalid or expired Google token"
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

Response 404: `{ "errorCode": "USER_NOT_FOUND", "message": "User not found" }`

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

Response 400: `{ "errorCode": "VALIDATION_ERROR", "message": "Invalid role. Allowed: customer, staff, manager, admin" }`

---

### GET /api/Users/status/{status}

Gets users filtered by status.

Roles allowed: `admin`, `manager`

Path parameter `status`: `active` or `inactive` or `suspended` or `pending`

Query parameters: `page`, `pageSize`

Response 200: Same paginated format as GET /api/Users.

Response 400: `{ "errorCode": "VALIDATION_ERROR", "message": "Invalid status. Allowed: active, inactive, suspended, pending" }`

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

Response 400: `{ "errorCode": "EMAIL_EXISTS", "message": "A user with this email already exists" }`

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

Response 400: `{ "errorCode": "VALIDATION_ERROR", "message": "Invalid status. Allowed: active, inactive, suspended, pending" }`

Response 404: `{ "errorCode": "USER_NOT_FOUND", "message": "User not found" }`

---

### PUT /api/Users/{id}/role

Updates a user's role.

Roles allowed: `admin`

Request body: `{ "role": "string" }` — must be one of: `customer`, `staff`, `manager`, `admin`

Response 200: Updated user object.

Response 400: `{ "errorCode": "VALIDATION_ERROR", "message": "Invalid role. Allowed: customer, staff, manager, admin" }`

Response 404: `{ "errorCode": "USER_NOT_FOUND", "message": "User not found" }`

---

## 3. FRAMES

### GET /api/Frames

Gets all available frames with pagination.

Roles allowed: Public (no authentication required)

Query parameters: `page`, `pageSize`

Visibility rules:
- Frames with status `available` are always shown
- Frames with status `out_of_stock` are shown **unless** they belong to an **upcoming** campaign (status `upcoming` and start date in the future) — these are hidden to avoid spoiling the preorder launch
- Frames with status `inactive` are never shown
- **Note:** Admin inventory endpoints (`GET /api/Frames/inventory/out-of-stock`, `GET /api/Frames/inventory/low-stock`) are **not** affected by these rules and always return all matching frames regardless of campaign status

Preorder info enrichment:
- If a frame is out of stock (`stockQuantity == 0` or `status == "out_of_stock"`) **and** belongs to a currently **active** campaign (`status == "active"`), the response includes a `preorderInfo` object
- If the conditions are not met, `preorderInfo` is `null`

Response 200: Paginated list of frames. Each frame includes `brand`, `material`, `shape`, `frameColors`, `frameMedia`, and `preorderInfo`.

Example `preorderInfo` (when present):
```json
{
  "campaignId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "campaignName": "Summer Collection Pre-order",
  "description": "Pre-order for out-of-stock summer frames",
  "startDate": "2025-06-01T00:00:00",
  "endDate": "2025-07-01T00:00:00",
  "maxSlots": 100,
  "currentSlots": 25,
  "estimatedDeliveryDate": "2025-08-15T00:00:00",
  "campaignPrice": 89.99,
  "maxQuantityPerOrder": 2
}
```

---

### GET /api/Frames/{id}

Gets a specific frame by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the frame.

Visibility rules:
- Any non-inactive frame is returned (both `available` and `out_of_stock`)
- Inactive frames are never returned (404)

Preorder info enrichment:
- If the frame is out of stock (`stockQuantity == 0` or `status == "out_of_stock"`) **and** belongs to a currently **active** campaign (`status == "active"`), the response includes a `preorderInfo` object
- If the conditions are not met, `preorderInfo` is `null`

Response 200: Frame object with details including `preorderInfo`.

Response 404: `{ "errorCode": "FRAME_NOT_FOUND", "message": "Frame not found or is not available" }`

---

### GET /api/Frames/{id}/media

Gets all media (images/videos) for a specific frame.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the frame.

Visibility rules:
- Media is returned for any non-inactive frame (both `available` and `out_of_stock`)
- Returns empty list / 404 for inactive frames

Response 200: List of media items.

Response 404: `{ "errorCode": "FRAME_NOT_FOUND", "message": "Frame not found or is not available" }`

---

### GET /api/Frames/{id}/lens-types

Gets the supported lens types for a specific frame. Single Vision and Non-Prescription lens types are always available.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the frame.

Response 200:
```json
{
  "frameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "frameName": "Aviator Classic",
  "minRx": -6.0,
  "maxRx": 6.0,
  "minPd": 58,
  "maxPd": 72,
  "supportedLensTypes": [...]
}
```

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
  "colorVariants": [
    { "colorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "stockQuantity": 10 }
  ],
  "supportedLensTypeIds": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"],
  "minRx": -6.0,
  "maxRx": 6.0,
  "minPd": 58,
  "maxPd": 72
}
```

Validation:
- `frameName` is required
- `shapeId` is optional — references SHAPE table
- `colorVariants` is optional — list of color variant objects with per-color stock quantities (total stock is auto-calculated)
- `supportedLensTypeIds` is optional — list of lens type GUIDs to associate with the frame
- Size attributes (lensWidth, bridgeWidth, frameWidth, templeLength) are validated for valid ranges
- `stockQuantity` defaults to 0 if not provided (overridden by sum of colorVariants if provided)
- `reorderLevel` defaults to 5 if not provided
- `minRx`, `maxRx`, `minPd`, `maxPd` are optional prescription range limits

Response 201: Created frame object (includes `brand`, `material`, `shape`, `frameColors` with nested `color`).

---

### PUT /api/Frames/{id}

Updates an existing frame.

Roles allowed: `manager`

All fields are optional. Only provided fields will be updated. `status` if provided must be one of: `available`, `inactive`, `out_of_stock`. `shapeId` references the SHAPE table. `colorVariants` if provided replaces all existing frame color variants. `supportedLensTypeIds` if provided replaces all existing supported lens types.

Request body:
```json
{
  "frameName": "string",
  "brandId": "guid",
  "materialId": "guid",
  "shapeId": "guid",
  "lensWidth": 0,
  "bridgeWidth": 0,
  "frameWidth": 0,
  "templeLength": 0,
  "size": "string",
  "basePrice": 0.0,
  "status": "string",
  "stockQuantity": 0,
  "reorderLevel": 0,
  "colorVariants": [
    { "colorId": "guid", "stockQuantity": 10 }
  ],
  "supportedLensTypeIds": ["guid"],
  "minRx": -6.0,
  "maxRx": 6.0,
  "minPd": 58,
  "maxPd": 72
}
```

Response 200: Updated frame object (includes `brand`, `material`, `shape`, `frameColors` with nested `color`).

Response 404: `{ "errorCode": "FRAME_NOT_FOUND", "message": "Frame not found" }`

---

### DELETE /api/Frames/{id}

Soft deletes a frame by setting status to inactive.

Roles allowed: `manager`

Path parameter `id`: GUID of the frame.

Response 204: No content.

Response 404: `{ "errorCode": "FRAME_NOT_FOUND", "message": "Frame not found" }`

---

### GET /api/Frames/inventory/low-stock

Gets frames with stock quantity at or below their reorder level.

Roles allowed: `manager`, `admin`

Response 200: List of frame objects with low stock.

---

### GET /api/Frames/inventory/out-of-stock

Gets frames that are out of stock (stock quantity = 0).

Roles allowed: `manager`, `admin`

Response 200: List of frame objects that are out of stock.

---

### PATCH /api/Frames/{id}/inventory

Updates stock quantity for a frame.

Roles allowed: `manager`, `admin`

Path parameter `id`: GUID of the frame.

Request body:
```json
{
  "quantity": 50,
  "reorderLevel": 10
}
```

Validation:
- `quantity` cannot be negative
- `reorderLevel` is optional

Response 200: Updated frame object.

Response 400: `{ "errorCode": "VALIDATION_ERROR", "message": "Stock quantity cannot be negative" }`

Response 404: `{ "errorCode": "FRAME_NOT_FOUND", "message": "Frame not found" }`

---

## 4. BRANDS

### GET /api/Brands

Gets all active brands.

Roles allowed: Public (no authentication required)

Response 200: List of brand objects.

---

### GET /api/Brands/{id}

Gets a specific brand by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the brand.

Response 200: Brand object.

Response 404: `{ "errorCode": "BRAND_NOT_FOUND", "message": "Brand not found" }`

---

### POST /api/Brands

Creates a new brand.

Roles allowed: `manager`

Request body:
```json
{
  "brandName": "string"
}
```

Validation:
- `brandName` is required

Response 201: Created brand object.

---

### PUT /api/Brands/{id}

Updates an existing brand.

Roles allowed: `manager`

Request body: `{ "brandName": "string" }` — optional.

Response 200: Updated brand object.

Response 404: `{ "errorCode": "BRAND_NOT_FOUND", "message": "Brand not found" }`

---

### DELETE /api/Brands/{id}

Soft deletes a brand.

Roles allowed: `manager`

Response 204: No content.

Response 404: `{ "errorCode": "BRAND_NOT_FOUND", "message": "Brand not found" }`

---

## 5. MATERIALS

### GET /api/Materials

Gets all active materials.

Roles allowed: Public (no authentication required)

Response 200: List of material objects.

---

### GET /api/Materials/{id}

Gets a specific material by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the material.

Response 200: Material object.

Response 404: `{ "errorCode": "MATERIAL_NOT_FOUND", "message": "Material not found" }`

---

### POST /api/Materials

Creates a new material.

Roles allowed: `manager`

Request body:
```json
{
  "materialName": "string"
}
```

Validation:
- `materialName` is required

Response 201: Created material object.

---

### PUT /api/Materials/{id}

Updates an existing material.

Roles allowed: `manager`

Request body: `{ "materialName": "string" }` — optional.

Response 200: Updated material object.

Response 404: `{ "errorCode": "MATERIAL_NOT_FOUND", "message": "Material not found" }`

---

### DELETE /api/Materials/{id}

Soft deletes a material.

Roles allowed: `manager`

Response 204: No content.

Response 404: `{ "errorCode": "MATERIAL_NOT_FOUND", "message": "Material not found" }`

---

## 6. SHAPES

### GET /api/Shapes

Gets all active shapes.

Roles allowed: Public (no authentication required)

Response 200: List of shape objects.

---

### GET /api/Shapes/{id}

Gets a specific shape by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the shape.

Response 200: Shape object.

Response 404: `{ "errorCode": "SHAPE_NOT_FOUND", "message": "Shape not found" }`

---

### POST /api/Shapes

Creates a new shape.

Roles allowed: `manager`

Request body:
```json
{
  "shapeName": "string"
}
```

Validation:
- `shapeName` is required

Response 201: Created shape object.

---

### PUT /api/Shapes/{id}

Updates an existing shape.

Roles allowed: `manager`

Request body: `{ "shapeName": "string" }` — optional.

Response 200: Updated shape object.

Response 404: `{ "errorCode": "SHAPE_NOT_FOUND", "message": "Shape not found" }`

---

### DELETE /api/Shapes/{id}

Soft deletes a shape.

Roles allowed: `manager`

Response 204: No content.

Response 404: `{ "errorCode": "SHAPE_NOT_FOUND", "message": "Shape not found" }`

---

## 7. COLORS

### GET /api/Colors

Gets all active colors.

Roles allowed: Public (no authentication required)

Response 200: List of color objects.

---

### GET /api/Colors/{id}

Gets a specific color by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the color.

Response 200: Color object.

Response 404: `{ "errorCode": "COLOR_NOT_FOUND", "message": "Color not found" }`

---

### POST /api/Colors

Creates a new color.

Roles allowed: `manager`

Request body:
```json
{
  "colorName": "string",
  "hexCode": "string"
}
```

Validation:
- `colorName` is required
- `hexCode` is optional

Response 201: Created color object.

---

### PUT /api/Colors/{id}

Updates an existing color.

Roles allowed: `manager`

Request body: `{ "colorName": "string", "hexCode": "string" }` — all optional.

Response 200: Updated color object.

Response 404: `{ "errorCode": "COLOR_NOT_FOUND", "message": "Color not found" }`

---

### DELETE /api/Colors/{id}

Soft deletes a color.

Roles allowed: `manager`

Response 204: No content.

Response 404: `{ "errorCode": "COLOR_NOT_FOUND", "message": "Color not found" }`

---

## 8. FRAME MEDIA

### GET /api/FrameMedia/frame/{frameId}

Gets all media for a specific frame. Optionally filter by color.

Roles allowed: Public (no authentication required)

Path parameter `frameId`: GUID of the frame.

Query parameters: `colorId` (optional GUID) — filter media by a specific color variant.

Response 200:
```json
[
  {
    "mediaId": "guid",
    "frameId": "guid",
    "mediaUrl": "https://...",
    "mediaType": "image",
    "colorId": "guid",
    "colorName": "Black",
    "hexCode": "#000000"
  }
]
```

Response 404: `{ "errorCode": "FRAME_NOT_FOUND", "message": "Frame not found" }`

---

### GET /api/FrameMedia/{id}

Gets a specific media item by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the media item.

Response 200: Media object.

Response 404: `{ "errorCode": "MEDIA_NOT_FOUND", "message": "Media not found" }`

---

### POST /api/FrameMedia

Adds a new media item to a frame.

Roles allowed: `manager`

Request body:
```json
{
  "frameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "mediaUrl": "https://...",
  "mediaType": "image",
  "colorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

Validation:
- `frameId` must reference an existing frame
- `mediaUrl` is required
- `mediaType` must be one of: `image`, `video`, `thumbnail`, `gallery`
- `colorId` is optional

Response 201: Created media object.

---

### POST /api/FrameMedia/batch

Adds multiple media items to a frame.

Roles allowed: `manager`

Request body:
```json
{
  "frameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "mediaItems": [
    {
      "mediaUrl": "https://...",
      "mediaType": "image",
      "colorId": "guid"
    }
  ]
}
```

Validation:
- `frameId` must reference an existing frame
- At least one media item is required
- Each item's `mediaUrl` is required
- Each item's `mediaType` (if provided) must be valid

Response 201: List of created media objects.

---

### PUT /api/FrameMedia/{id}

Updates a media item.

Roles allowed: `manager`

Request body: `{ "mediaUrl": "string", "mediaType": "string", "colorId": "guid" }` — all optional.

Response 200: Updated media object.

Response 404: `{ "errorCode": "MEDIA_NOT_FOUND", "message": "Media not found" }`

---

### DELETE /api/FrameMedia/{id}

Deletes a media item.

Roles allowed: `manager`

Response 204: No content.

Response 404: `{ "errorCode": "MEDIA_NOT_FOUND", "message": "Media not found" }`

---

### DELETE /api/FrameMedia/frame/{frameId}

Deletes all media for a frame.

Roles allowed: `manager`

Path parameter `frameId`: GUID of the frame.

Response 204: No content.

Response 404: `{ "errorCode": "FRAME_NOT_FOUND", "message": "Frame not found" }`

---

### POST /api/FrameMedia/upload/{frameId}

Uploads an image to Cloudinary and creates a media record for a frame.

Roles allowed: `manager`

Path parameter `frameId`: GUID of the frame.

Query parameters:
- `mediaType` (default: `image`) — must be one of: `image`, `video`, `thumbnail`, `gallery`
- `colorId` (optional GUID) — associate this image with a specific color variant

Request: `multipart/form-data` with `file` field (image file).

Validation:
- Frame must exist
- File is required, max 10MB
- Allowed extensions: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`

Response 201:
```json
{
  "mediaId": "guid",
  "frameId": "guid",
  "mediaUrl": "https://res.cloudinary.com/...",
  "mediaType": "image",
  "colorId": "guid",
  "publicId": "spectra/frames/xxx/filename"
}
```

---

### POST /api/FrameMedia/upload-multiple/{frameId}

Uploads multiple images to Cloudinary and creates media records for a frame.

Roles allowed: `manager`

Path parameter `frameId`: GUID of the frame.

Query parameters:
- `mediaType` (default: `image`) — must be valid
- `colorId` (optional GUID)

Request: `multipart/form-data` with `files` field (max 10 files, each max 10MB, allowed: jpg, jpeg, png, gif, webp).

Response 201:
```json
{
  "uploadedMedia": [...],
  "errors": ["File 3: Exceeds 10MB limit"]
}
```

---

### POST /api/FrameMedia/upload

Uploads an image to Cloudinary without associating it with a frame (useful for getting a URL before creating/updating a frame).

Roles allowed: `manager`

Query parameters: `folder` (default: `spectra/products`)

Request: `multipart/form-data` with `file` field.

Validation: Same file constraints as upload/{frameId}.

Response 200:
```json
{
  "success": true,
  "url": "https://res.cloudinary.com/...",
  "publicId": "spectra/products/filename"
}
```

---

### DELETE /api/FrameMedia/cloudinary/{publicId}

Deletes an image from Cloudinary by its public ID.

Roles allowed: `manager`

Path parameter `publicId`: The Cloudinary public ID (supports path segments).

Response 204: No content.

Response 400: `{ "errorCode": "DELETE_ERROR", "message": "Failed to delete image from Cloudinary" }`

---

## 9. LENS TYPES

### GET /api/LensTypes

Gets all lens types with pagination.

Roles allowed: Public (no authentication required)

Query parameters: `page`, `pageSize`

Response 200: Paginated list of lens type objects.

---

### GET /api/LensTypes/{id}

Gets a specific lens type by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the lens type.

Response 200: Lens type object.

Response 404: `{ "errorCode": "LENS_TYPE_NOT_FOUND", "message": "Lens type not found" }`

---

### GET /api/LensTypes/prescription-required

Gets all lens types that require a prescription.

Roles allowed: Public (no authentication required)

Response 200: List of lens type objects.

---

### GET /api/LensTypes/no-prescription

Gets all lens types that do not require a prescription.

Roles allowed: Public (no authentication required)

Response 200: List of lens type objects.

---

### POST /api/LensTypes

Creates a new lens type.

Roles allowed: `manager`

Request body:
```json
{
  "lensSpecification": "string",
  "requiresPrescription": true,
  "basePrice": 0.0
}
```

Validation:
- `lensSpecification` is required
- `basePrice` cannot be negative

Response 201: Created lens type object.

---

### PUT /api/LensTypes/{id}

Updates an existing lens type.

Roles allowed: `manager`

Request body: `{ "lensSpecification": "string", "requiresPrescription": true, "basePrice": 0.0 }` — all optional.

Response 200: Updated lens type object.

Response 404: `{ "errorCode": "LENS_TYPE_NOT_FOUND", "message": "Lens type not found" }`

---

### DELETE /api/LensTypes/{id}

Deletes a lens type. Only allowed if not used in any orders or preorders.

Roles allowed: `manager`

Response 204: No content.

Response 400: `{ "errorCode": "LENS_TYPE_IN_USE", "message": "Cannot delete lens type because it is used in existing orders or preorders" }`

Response 404: `{ "errorCode": "LENS_TYPE_NOT_FOUND", "message": "Lens type not found" }`

---

## 10. LENS FEATURES

### GET /api/LensFeatures

Gets all lens features with pagination.

Roles allowed: Public (no authentication required)

Query parameters: `page`, `pageSize`

Response 200: Paginated list of lens feature objects.

---

### GET /api/LensFeatures/{id}

Gets a specific lens feature by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the lens feature.

Response 200: Lens feature object.

Response 404: `{ "errorCode": "LENS_FEATURE_NOT_FOUND", "message": "Lens feature not found" }`

---

### POST /api/LensFeatures/calculate-price

Calculates total price based on frame base price, lens type, lens feature, and lens index.

Roles allowed: Public (no authentication required)

Request body:
```json
{
  "basePrice": 100.0,
  "lensFeatureId": "guid",
  "lensTypeId": "guid",
  "lensIndexId": "guid"
}
```

Validation:
- `basePrice` cannot be negative
- All IDs are optional

Response 200:
```json
{
  "basePrice": 100.0,
  "featureExtraPrice": 20.0,
  "lensTypeExtraPrice": 30.0,
  "lensIndexExtraPrice": 15.0,
  "totalPrice": 165.0
}
```

---

### POST /api/LensFeatures

Creates a new lens feature.

Roles allowed: `manager`

Request body:
```json
{
  "featureSpecification": "string",
  "extraPrice": 0.0
}
```

Validation:
- `featureSpecification` is required
- `extraPrice` is validated for valid range

Response 201: Created lens feature object.

---

### PUT /api/LensFeatures/{id}

Updates an existing lens feature.

Roles allowed: `manager`

Request body: `{ "featureSpecification": "string", "extraPrice": 0.0 }` — all optional.

Response 200: Updated lens feature object.

Response 404: `{ "errorCode": "LENS_FEATURE_NOT_FOUND", "message": "Lens feature not found" }`

---

### DELETE /api/LensFeatures/{id}

Deletes a lens feature. Only allowed if not used in any orders or preorders.

Roles allowed: `manager`

Response 204: No content.

Response 400: `{ "errorCode": "LENS_FEATURE_IN_USE", "message": "Cannot delete lens feature because it is used in existing orders or preorders" }`

Response 404: `{ "errorCode": "LENS_FEATURE_NOT_FOUND", "message": "Lens feature not found" }`

---

## 11. LENS INDICES

### GET /api/LensIndices

Gets all active lens indices.

Roles allowed: Public (no authentication required)

Response 200: List of lens index objects.

---

### GET /api/LensIndices/{id}

Gets a specific lens index by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the lens index.

Response 200: Lens index object.

Response 404: `{ "errorCode": "LENS_INDEX_NOT_FOUND", "message": "Lens index not found" }`

---

### GET /api/LensIndices/compatible

Gets compatible lens indices for a given prescription sphere value.

Roles allowed: Public (no authentication required)

Query parameters: `sphere` (double, required) — the prescription sphere value.

Response 200: List of compatible lens index objects.

---

### POST /api/LensIndices

Creates a new lens index.

Roles allowed: `manager`

Request body:
```json
{
  "indexValue": 1.67,
  "name": "string",
  "description": "string",
  "additionalPrice": 0.0,
  "minPrescription": -2.0,
  "maxPrescription": 2.0,
  "brandId": "guid",
  "colorId": "guid"
}
```

Validation:
- `name` is required
- `indexValue` must be greater than 0

Response 201: Created lens index object.

---

### PUT /api/LensIndices/{id}

Updates an existing lens index.

Roles allowed: `manager`

Request body: All fields are optional.

Response 200: Updated lens index object.

Response 404: `{ "errorCode": "LENS_INDEX_NOT_FOUND", "message": "Lens index not found" }`

---

### DELETE /api/LensIndices/{id}

Soft deletes a lens index.

Roles allowed: `manager`

Response 204: No content.

Response 404: `{ "errorCode": "LENS_INDEX_NOT_FOUND", "message": "Lens index not found" }`

---

## 12. PRESCRIPTIONS

### POST /api/Prescriptions

Creates a new prescription for the current user.

Roles allowed: `customer`

Request body:
```json
{
  "sphereRight": -2.0,
  "cylinderRight": -0.75,
  "axisRight": 180,
  "addRight": 1.5,
  "sphereLeft": -1.75,
  "cylinderLeft": -0.5,
  "axisLeft": 170,
  "addLeft": 1.5,
  "pupillaryDistance": 63,
  "doctorName": "Dr. Smith",
  "clinicName": "Vision Care Center",
  "expirationDate": "2026-06-15T00:00:00"
}
```

Validation:
- At least one sphere value (left or right) is required
- Prescription values are validated for valid clinical ranges

Response 201:
```json
{
  "prescriptionId": "guid",
  "userId": "guid",
  "sphereRight": -2.0,
  "cylinderRight": -0.75,
  "axisRight": 180,
  "addRight": 1.5,
  "sphereLeft": -1.75,
  "cylinderLeft": -0.5,
  "axisLeft": 170,
  "addLeft": 1.5,
  "pupillaryDistance": 63,
  "doctorName": "Dr. Smith",
  "clinicName": "Vision Care Center",
  "expirationDate": "2026-06-15T00:00:00",
  "createdAt": "2025-01-15T00:00:00",
  "isExpired": false,
  "daysUntilExpiration": 365
}
```

---

### GET /api/Prescriptions/my

Gets all prescriptions for the current user with pagination.

Roles allowed: `customer`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of prescription objects (includes `isExpired` and `daysUntilExpiration`).

---

### GET /api/Prescriptions/my/valid

Gets only valid (non-expired) prescriptions for the current user.

Roles allowed: `customer`

Response 200: List of valid prescription objects.

---

### GET /api/Prescriptions/{id}

Gets a specific prescription by ID. Customers can only view their own prescriptions.

Roles allowed: Any authenticated user

Path parameter `id`: GUID of the prescription.

Response 200: Prescription object.

Response 403: Forbidden (if customer tries to access another user's prescription).

Response 404: `{ "errorCode": "PRESCRIPTION_NOT_FOUND", "message": "Prescription not found" }`

---

### PUT /api/Prescriptions/{id}

Updates an existing prescription. Customers can only update their own prescriptions.

Roles allowed: `customer`

Request body: Same fields as create, all optional. Values are validated for clinical ranges.

Response 200: Updated prescription object.

Response 404: `{ "errorCode": "PRESCRIPTION_NOT_FOUND", "message": "Prescription not found or you don't have permission to update it" }`

---

### DELETE /api/Prescriptions/{id}

Deletes a prescription. Only allowed if not used in existing orders or preorders. Customers can only delete their own.

Roles allowed: `customer`

Response 204: No content.

Response 400: `{ "errorCode": "PRESCRIPTION_IN_USE", "message": "Cannot delete prescription because it is used in existing orders or preorders" }`

Response 404: `{ "errorCode": "PRESCRIPTION_NOT_FOUND", "message": "Prescription not found or you don't have permission to delete it" }`

---

### GET /api/Prescriptions/{id}/validate

Checks if a prescription is valid (not expired).

Roles allowed: Any authenticated user

Path parameter `id`: GUID of the prescription.

Response 200:
```json
{
  "prescriptionId": "guid",
  "isValid": true,
  "isExpired": false,
  "daysUntilExpiration": 365,
  "expirationDate": "2026-06-15T00:00:00",
  "message": "This prescription is valid"
}
```

Response 404: `{ "errorCode": "PRESCRIPTION_NOT_FOUND", "message": "Prescription not found" }`

---

### GET /api/Prescriptions/user/{userId}

Gets prescriptions for a specific user.

Roles allowed: `staff`, `manager`, `admin`

Path parameter `userId`: GUID of the user.

Query parameters: `page`, `pageSize`

Response 200: Paginated list of prescription objects.

---

## 13. ORDERS

### POST /api/Orders

Creates a new order.

Roles allowed: `customer`

Request body:
```json
{
  "shippingAddress": "string",
  "shippingMethod": "standard",
  "items": [
    {
      "frameId": "guid",
      "lensTypeId": "guid",
      "featureId": "guid",
      "lensIndexId": "guid",
      "prescriptionId": "guid",
      "quantity": 1,
      "selectedColorId": "guid",
      "selectedSize": "string"
    }
  ]
}
```

Validation:
- `shippingAddress` is required
- `items` must contain at least one item
- `shippingMethod` defaults to `standard` if not provided
- Order items are validated (frame availability, lens type compatibility, prescription requirements, etc.)
- Shipping fee is auto-calculated based on method and subtotal

Response 201:
```json
{
  "orderId": "guid",
  "userId": "guid",
  "totalAmount": 150.0,
  "shippingAddress": "123 Main St",
  "status": "pending",
  "createdAt": "2025-01-15T00:00:00",
  "itemCount": 2,
  "convertedFromPreorderId": null
}
```

---

### GET /api/Orders/my

Gets current user's orders with pagination.

Roles allowed: `customer`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of order objects.

---

### GET /api/Orders/{id}

Gets a specific order by ID with full details. Customers can only view their own orders.

Roles allowed: Any authenticated user

Path parameter `id`: GUID of the order.

Response 200: Order object with order items, details, and `convertedFromPreorderId` (GUID of the original preorder if this order was converted from a preorder, otherwise `null`)

Response 403: Forbidden (if customer tries to access another user's order).

Response 404: `{ "errorCode": "ORDER_NOT_FOUND", "message": "Order not found" }`

---

### GET /api/Orders

Gets all orders with pagination.

Roles allowed: `staff`, `manager`, `admin`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of order objects.

---

### PUT /api/Orders/{id}/status

Updates order status.

Roles allowed: `staff`, `manager`, `admin`

Path parameter `id`: GUID of the order.

Request body:
```json
{
  "status": "string"
}
```

Validation:
- `status` is required
- Must be one of: `pending`, `confirmed`, `processing`, `shipped`, `delivered`, `cancelled`
- Status transitions are validated based on user role

Response 200: Updated order object.

Response 400: `{ "errorCode": "VALIDATION_ERROR", "message": "Invalid status..." }`

Response 404: `{ "errorCode": "UPDATE_FAILED", "message": "Order not found or status transition not allowed for your role" }`

---

### DELETE /api/Orders/{id}

Cancels an order (sets status to `cancelled`).

Roles allowed: `manager`, `admin`

Path parameter `id`: GUID of the order.

Response 200: Updated order object.

Response 404: `{ "errorCode": "CANCEL_FAILED", "message": "Order not found or cannot be cancelled" }`

---

## 14. PAYMENTS

### POST /api/Payments

Creates a new payment and returns a VNPay payment URL (if payment method is `vnpay`).

Roles allowed: `customer`

Request body:
```json
{
  "orderId": "guid",
  "preorderId": "guid",
  "paymentMethod": "vnpay"
}
```

Validation:
- Must link to either an `orderId` **OR** a `preorderId`, not both (and not neither)
- `paymentMethod` must be one of: `vnpay`, `cash`, `bank_transfer`

Response 201:
```json
{
  "paymentId": "guid",
  "orderId": "guid",
  "preorderId": null,
  "amount": 150.0,
  "paymentMethod": "vnpay",
  "paymentStatus": "pending",
  "paymentUrl": "https://sandbox.vnpayment.vn/...",
  "paidAt": null
}
```

Response 400: `{ "errorCode": "VALIDATION_ERROR", "message": "Payment must be linked to either an order OR a preorder, not both" }`

Response 400: `{ "errorCode": "PAYMENT_CREATION_FAILED", "message": "..." }`

---

### GET /api/Payments/my

Gets current user's payments with pagination.

Roles allowed: `customer`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of payment objects.

---

### GET /api/Payments/{id}

Gets a specific payment by ID.

Roles allowed: Any authenticated user

Path parameter `id`: GUID of the payment.

Response 200: Payment object.

Response 404: `{ "errorCode": "PAYMENT_NOT_FOUND", "message": "Payment not found" }`

---

### GET /api/Payments/vnpay-return

VNPay return URL handler. Redirects to the frontend thank-you page after VNPay payment completion.

Roles allowed: Public (no authentication required)

This endpoint is called by VNPay after payment and redirects the user to: `{FrontendUrl}/payment/return?vnp_ResponseCode=00&paymentId=...&transactionId=...&amount=...`

---

### POST /api/Payments/vnpay-ipn

VNPay IPN (Instant Payment Notification) handler for server-to-server payment confirmation.

Roles allowed: Public (no authentication required)

Response 200:
```json
{ "rspCode": "00", "message": "Confirm Success" }
```

---

### PUT /api/Payments/{id}/status

Updates payment status.

Roles allowed: `staff`, `manager`, `admin`

Request body:
```json
{
  "status": "string"
}
```

Validation:
- `status` is required
- Must be one of: `pending`, `processing`, `completed`, `failed`, `cancelled`, `refunded`

Response 200: Updated payment object.

Response 404: `{ "errorCode": "UPDATE_FAILED", "message": "Payment not found or status update not allowed" }`

---

### GET /api/Payments/order/{orderId}

Gets payments for a specific order.

Roles allowed: `staff`, `manager`, `admin`

Path parameter `orderId`: GUID of the order.

Response 200: List of payment objects.

---

### GET /api/Payments/preorder/{preorderId}

Gets payments for a specific preorder.

Roles allowed: `staff`, `manager`, `admin`

Path parameter `preorderId`: GUID of the preorder.

Response 200: List of payment objects.

---

## 15. PRODUCT REVIEWS

### GET /api/ProductReviews/frame/{frameId}

Gets reviews for a specific frame with pagination.

Roles allowed: Public (no authentication required)

Path parameter `frameId`: GUID of the frame.

Query parameters: `page`, `pageSize`

Response 200: Paginated list of review objects.

---

### GET /api/ProductReviews/frame/{frameId}/summary

Gets the review summary (average rating, distribution) for a frame.

Roles allowed: Public (no authentication required)

Path parameter `frameId`: GUID of the frame.

Response 200: Review summary object with average rating and rating distribution.

---

### POST /api/ProductReviews

Creates a new review.

Roles allowed: `customer`

Request body:
```json
{
  "frameId": "guid",
  "orderItemId": "guid",
  "rating": 5,
  "title": "string",
  "comment": "string"
}
```

Validation:
- `rating` must be between 0 and 5
- `orderItemId` is optional (for verified purchase link)

Response 201: Created review object.

Response 400: `{ "errorCode": "REVIEW_ERROR", "message": "..." }`

---

### GET /api/ProductReviews/my-reviews

Gets reviews by the current user with pagination.

Roles allowed: `customer`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of review objects.

---

### PUT /api/ProductReviews/{id}

Updates a review. Customers can only update their own reviews.

Roles allowed: `customer`

Request body: `{ "rating": 5, "title": "string", "comment": "string" }` — all optional.

Response 200: Updated review object.

Response 404: `{ "errorCode": "REVIEW_NOT_FOUND", "message": "Review not found or you don't have permission to update it" }`

---

### DELETE /api/ProductReviews/{id}

Deletes a review. Customers can only delete their own reviews.

Roles allowed: `customer`

Response 204: No content.

Response 404: `{ "errorCode": "REVIEW_NOT_FOUND", "message": "Review not found or you don't have permission to delete it" }`

---

### GET /api/ProductReviews/verified-purchase/{frameId}

Checks if the current user has a verified purchase of a frame.

Roles allowed: `customer`

Path parameter `frameId`: GUID of the frame.

Response 200:
```json
{
  "isVerifiedPurchase": true
}
```

---

### PATCH /api/ProductReviews/{id}/hide

Hides a review.

Roles allowed: `manager`, `admin`

Path parameter `id`: GUID of the review.

Response 200: `{ "message": "Review hidden successfully" }`

Response 404: `{ "errorCode": "REVIEW_NOT_FOUND", "message": "Review not found" }`

---

## 16. COMPLAINTS

### POST /api/Complaints

Submits a new complaint/return request.

Roles allowed: `customer`

Request body:
```json
{
  "orderItemId": "guid",
  "requestType": "return",
  "reason": "string",
  "mediaUrl": "string"
}
```

Validation:
- `requestType` must be one of: `return`, `exchange`, `refund`, `complaint`, `warranty`
- `reason` is required
- `mediaUrl` is optional

Response 201:
```json
{
  "requestId": "guid",
  "userId": "guid",
  "orderItemId": "guid",
  "requestType": "return",
  "reason": "Defective product",
  "mediaUrl": "https://...",
  "status": "pending",
  "createdAt": "2025-01-15T00:00:00",
  "canModify": true
}
```

---

### GET /api/Complaints/my

Gets all complaints for the current user with pagination.

Roles allowed: `customer`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of complaint objects (each includes `canModify` flag).

---

### GET /api/Complaints/{id}

Gets a specific complaint by ID. Customers can only view their own complaints.

Roles allowed: Any authenticated user

Path parameter `id`: GUID of the complaint.

Response 200: Complaint object.

Response 403: Forbidden (if customer tries to access another user's complaint).

Response 404: `{ "errorCode": "COMPLAINT_NOT_FOUND", "message": "Complaint not found" }`

---

### PUT /api/Complaints/{id}

Updates a complaint. Only allowed if the complaint is still in `pending` status. Customers can only update their own.

Roles allowed: `customer`

Request body: `{ "requestType": "string", "reason": "string", "mediaUrl": "string" }` — all optional.

Response 200: Updated complaint object.

Response 404: `{ "errorCode": "UPDATE_FAILED", "message": "Complaint not found, you don't have permission, or it can no longer be modified" }`

---

### GET /api/Complaints

Gets all complaints with pagination.

Roles allowed: `staff`, `manager`, `admin`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of complaint objects.

---

### GET /api/Complaints/status/{status}

Gets complaints filtered by status.

Roles allowed: `staff`, `manager`, `admin`

Path parameter `status`: `pending`, `under_review`, `approved`, `rejected`, `in_progress`, `resolved`, `cancelled`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of complaint objects.

Response 400: `{ "errorCode": "VALIDATION_ERROR", "message": "Invalid status. Allowed: pending, under_review, approved, rejected, in_progress, resolved, cancelled" }`

---

### PUT /api/Complaints/{id}/status

Updates complaint status.

Roles allowed: `staff`, `manager`, `admin`

Request body:
```json
{
  "status": "string"
}
```

Validation:
- `status` is required
- Must be one of: `pending`, `under_review`, `approved`, `rejected`, `in_progress`, `resolved`, `cancelled`

Response 200: Updated complaint object.

Response 404: `{ "errorCode": "UPDATE_FAILED", "message": "Complaint not found" }`

---

## 17. PREORDER CAMPAIGNS

### GET /api/PreorderCampaigns

Gets all preorder campaigns regardless of status (upcoming, active, ended). Ordered by newest first.

Roles allowed: Public (no authentication required)

Response 200: List of campaign objects with associated frames.

```json
[
  {
    "campaignId": "guid",
    "campaignName": "Summer Collection Pre-order",
    "description": "...",
    "startDate": "2025-06-01T00:00:00",
    "endDate": "2025-07-01T00:00:00",
    "maxSlots": 100,
    "currentSlots": 25,
    "status": "active",
    "estimatedDeliveryDate": "2025-08-15T00:00:00",
    "createdAt": "2025-05-15T00:00:00",
    "frames": [
      {
        "campaignFrameId": "guid",
        "frameId": "guid",
        "campaignPrice": 89.99,
        "maxQuantityPerOrder": 2,
        "frameName": "Aviator Classic",
        "frameBasePrice": 109.99,
        "frameStatus": "out_of_stock"
      }
    ]
  }
]
```

---

### GET /api/PreorderCampaigns/active

Gets all active preorder campaigns (currently running: start date ? now ? end date, status is `upcoming` or `active`).

Roles allowed: Public (no authentication required)

Response 200: List of campaign objects with associated frames (same format as `GET /api/PreorderCampaigns`).

---

### GET /api/PreorderCampaigns/statuses

Gets the list of possible campaign status values.

Roles allowed: Public (no authentication required)

Response 200:
```json
[
  { "value": "upcoming", "description": "Campaign has not started yet" },
  { "value": "active", "description": "Campaign is currently running" },
  { "value": "ended", "description": "Campaign has ended" }
]
```

---

### GET /api/PreorderCampaigns/{id}

Gets a specific campaign by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the campaign.

Response 200: Campaign object with frames.

Response 404: `{ "errorCode": "CAMPAIGN_NOT_FOUND", "message": "Campaign not found" }`

---

### POST /api/PreorderCampaigns

Creates a new preorder campaign.

Roles allowed: `manager`

Request body:
```json
{
  "campaignName": "string",
  "description": "string",
  "startDate": "2025-06-01T00:00:00",
  "endDate": "2025-07-01T00:00:00",
  "maxSlots": 100,
  "estimatedDeliveryDate": "2025-08-15T00:00:00",
  "frames": [
    {
      "frameId": "guid",
      "campaignPrice": 89.99,
      "maxQuantityPerOrder": 2
    }
  ]
}
```

Validation:
- `campaignName` is required
- `startDate` must be before `endDate`
- At least one frame is required
- `maxQuantityPerOrder` defaults to 2 if not greater than 0

Response 201: Created campaign object with frames.

---

### PUT /api/PreorderCampaigns/{id}

Updates an existing campaign.

Roles allowed: `manager`

Request body: `{ "campaignName": "string", "description": "string", "maxSlots": 100, "estimatedDeliveryDate": "..." }` — all optional.

Response 200: Updated campaign object.

Response 404: `{ "errorCode": "CAMPAIGN_NOT_FOUND", "message": "Campaign not found" }`

---

### PATCH /api/PreorderCampaigns/{id}/end

Ends a campaign.

Roles allowed: `manager`

Path parameter `id`: GUID of the campaign.

Response 200: `{ "message": "Campaign ended successfully" }`

Response 404: `{ "errorCode": "CAMPAIGN_NOT_FOUND", "message": "Campaign not found" }`

---

## 18. PREORDERS

### POST /api/Preorders

Creates a new preorder.

Roles allowed: `customer`

Request body:
```json
{
  "campaignId": "guid",
  "expectedDate": "2025-08-15T00:00:00",
  "shippingAddress": "123 Nguy?n Hu?, Qu?n 1, TP.HCM",
  "items": [
    {
      "frameId": "guid",
      "lensTypeId": "guid",
      "featureId": "guid",
      "lensIndexId": "guid",
      "prescriptionId": "guid",
      "quantity": 1,
      "selectedColorId": "guid",
      "selectedSize": "string"
    }
  ]
}
```

Validation:
- `items` must contain at least one item
- `campaignId` is optional — if provided, the preorder is validated against campaign rules (active status, available slots, matching frames, quantity limits)
- `shippingAddress` is optional — can be provided at preorder creation or later at conversion
- Preorder items are validated (frame availability, lens type compatibility, prescription requirements)

Response 201: Created preorder object (includes `shippingAddress`).

Response 400: `{ "errorCode": "CAMPAIGN_VALIDATION_ERROR", "message": "Preorder does not meet campaign requirements..." }`

---

### GET /api/Preorders/my

Gets current user's preorders with pagination.

Roles allowed: `customer`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of preorder objects. Each preorder includes `shippingAddress`.

---

### GET /api/Preorders/{id}

Gets a specific preorder by ID with full details. Customers can only view their own preorders.

Roles allowed: Any authenticated user

Path parameter `id`: GUID of the preorder.

Response 200: Preorder object with items, details, and `shippingAddress`.

Response 403: Forbidden (if customer tries to access another user's preorder).

Response 404: `{ "errorCode": "PREORDER_NOT_FOUND", "message": "Preorder not found" }`

---

### DELETE /api/Preorders/{id}

Cancels a preorder. Only allowed if the preorder has not been paid. Customers can only cancel their own.

Roles allowed: `customer`

Response 204: No content.

Response 400: `{ "errorCode": "CANCEL_FAILED", "message": "Cannot cancel preorder. It may have already been paid." }`

Response 403: Forbidden (if customer tries to cancel another user's preorder).

Response 404: `{ "errorCode": "PREORDER_NOT_FOUND", "message": "Preorder not found" }`

---

### GET /api/Preorders

Gets all preorders with pagination.

Roles allowed: `staff`, `manager`, `admin`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of preorder objects. Each preorder includes `shippingAddress`.

---

### PUT /api/Preorders/{id}/status

Updates preorder status.

Roles allowed: `staff`, `manager`, `admin`

Request body:
```json
{
  "status": "string"
}
```

Validation:
- `status` is required
- Must be one of: `pending`, `confirmed`, `paid`, `converted`, `cancelled`

Response 200: Updated preorder object.

Response 404: `{ "errorCode": "UPDATE_FAILED", "message": "Preorder not found or status update not allowed" }`

---

### POST /api/Preorders/{id}/convert

Converts a preorder to an order. The resulting order stores a reference back to the original preorder via `convertedFromPreorderId`.

Roles allowed: `staff`, `manager`, `admin`

Path parameter `id`: GUID of the preorder.

Request body:
```json
{
  "shippingAddress": "string"
}
```

Validation:
- `shippingAddress` is required
- Preorder must be in `paid` or `confirmed` status

Response 200: Created order object. The `convertedFromPreorderId` field is set to the original preorder's ID, allowing the frontend to trace the order back to its preorder origin (e.g., for display on the shipping page).

Example response:
```json
{
  "orderId": "order-XYZ-guid",
  "convertedFromPreorderId": "preorder-ABC-guid",
  "status": "confirmed",
  "userId": "guid",
  "totalAmount": 150.0,
  "shippingAddress": "123 Main St",
  "createdAt": "2025-01-15T00:00:00"
}
```

Response 400: `{ "errorCode": "CONVERSION_FAILED", "message": "Preorder cannot be converted. It must be in 'paid' or 'confirmed' status." }`

Response 404: `{ "errorCode": "CONVERSION_FAILED", "message": "Failed to convert preorder to order" }`

---

## 19. SHIPPING

Shipping integrates with [GoShip](https://doc.goship.io) — a Vietnamese shipping aggregator — to provide real carrier rates, shipment creation, and tracking. Local helper endpoints are also available for simple fee calculations.

### Typical Flow

1. **Get rates** — `POST /api/Shipping/goship/rates` with sender/receiver addresses and parcel info ? returns available carriers with prices
2. **User picks a rate** during checkout
3. **Create shipment** — `POST /api/Shipping/goship/shipments` with the chosen `rateId` (and optional `orderId` to auto-assign tracking)
4. **Track shipment** — `GET /api/Shipping/goship/shipments/{shipmentId}`

---

### GET /api/Shipping/methods

Gets all available local shipping methods and their base fees.

Roles allowed: Public (no authentication required)

Response 200:
```json
{
  "method": "standard",
  "fee": 5.0,
  "description": "Standard Shipping (5-7 business days)"
}
```

---

### POST /api/Shipping/calculate

Calculates the shipping fee for a given method and order subtotal. Orders above the free-shipping threshold (default 89.0) get free shipping.

Roles allowed: Public (no authentication required)

Request body:
```json
{
  "shippingMethod": "standard",
  "orderSubtotal": 120.0
}
```

Response 200:
```json
{
  "shippingMethod": "standard",
  "orderSubtotal": 120.0,
  "shippingFee": 0,
  "total": 120.0
}
```

---

### POST /api/Shipping/goship/rates

Gets available shipping rates from GoShip for the given sender/receiver addresses and parcel details. This calls the GoShip `POST /rates` API.

Roles allowed: Public (no authentication required)

Request body:
```json
{
  "addressFrom": {
    "name": "Spectra Store",
    "phone": "0901234567",
    "street": "123 Nguy?n Hu?",
    "ward": "Ph??ng B?n Nghé",
    "district": "Qu?n 1",
    "city": "H? Chí Minh"
  },
  "addressTo": {
    "name": "Nguy?n Vân A",
    "phone": "0987654321",
    "street": "456 Lê L?i",
    "ward": "Ph??ng 1",
    "district": "Qu?n 3",
    "city": "H? Chí Minh"
  },
  "parcel": {
    "cod": 0,
    "weight": 500,
    "width": 20,
    "height": 10,
    "length": 15,
    "metadata": ""
  }
}
```

Field notes:
- `cod` — Cash on delivery amount (in VND), set to `0` if not applicable
- `weight` — Parcel weight in grams
- `width`, `height`, `length` — Dimensions in centimeters

Response 200:
```json
{
  "code": 200,
  "status": "success",
  "data": [
    {
      "id": "rate_abc123",
      "carrier_name": "Giao Hàng Nhanh",
      "carrier_logo": "https://...",
      "service": "Express",
      "total_fee": 25000,
      "total_fee_after_discount": 22000,
      "expected": "2-3 days"
    },
    {
      "id": "rate_def456",
      "carrier_name": "Viettel Post",
      "carrier_logo": "https://...",
      "service": "Standard",
      "total_fee": 18000,
      "total_fee_after_discount": 18000,
      "expected": "3-5 days"
    }
  ]
}
```

Response 502:
```json
{
  "errorCode": "GOSHIP_ERROR",
  "message": "Failed to retrieve rates from GoShip"
}
```

---

### POST /api/Shipping/goship/shipments

Creates a shipment on GoShip using a rate ID obtained from the rates endpoint. Optionally links the shipment to an existing order by providing `orderId`, which auto-assigns the tracking number and carrier to the order.

Roles allowed: `manager`

Request body:
```json
{
  "rateId": "rate_abc123",
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "addressFrom": {
    "name": "Spectra Store",
    "phone": "0901234567",
    "street": "123 Nguy?n Hu?",
    "ward": "Ph??ng B?n Nghé",
    "district": "Qu?n 1",
    "city": "H? Chí Minh"
  },
  "addressTo": {
    "name": "Nguy?n Vân A",
    "phone": "0987654321",
    "street": "456 Lê L?i",
    "ward": "Ph??ng 1",
    "district": "Qu?n 3",
    "city": "H? Chí Minh"
  },
  "parcel": {
    "cod": 0,
    "weight": 500,
    "width": 20,
    "height": 10,
    "length": 15,
    "metadata": ""
  }
}
```

Validation:
- `rateId` is required — must be a valid rate ID from `POST /api/Shipping/goship/rates`
- `orderId` is optional — if provided, tracking number and carrier are auto-assigned to the order, and order status changes from `processing` to `shipped`

Response 200 (with `orderId`):
```json
{
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "trackingNumber": "GHN123456789",
  "shippingCarrier": "Giao Hàng Nhanh",
  "shippedAt": "2025-01-15T10:30:00Z",
  "status": "shipped"
}
```

Response 200 (without `orderId`):
```json
{
  "code": 200,
  "status": "success",
  "data": {
    "id": "shipment_xyz789",
    "tracking_number": "GHN123456789",
    "carrier": "Giao Hàng Nhanh",
    "status": "created",
    "rate": "rate_abc123",
    "price": 22000,
    "created_at": "2025-01-15T10:30:00Z"
  }
}
```

Response 400:
```json
{
  "errorCode": "VALIDATION_ERROR",
  "message": "RateId is required. Call GET /goship/rates first."
}
```

Response 502:
```json
{
  "errorCode": "GOSHIP_ERROR",
  "message": "Failed to create shipment on GoShip or order not found"
}
```

---

### GET /api/Shipping/goship/shipments/{shipmentId}

Gets shipment tracking details from GoShip.

Roles allowed: Public (no authentication required)

Path parameter `shipmentId`: The GoShip shipment ID (string).

Response 200:
```json
{
  "code": 200,
  "status": "success",
  "data": {
    "id": "shipment_xyz789",
    "tracking_number": "GHN123456789",
    "carrier": "Giao Hàng Nhanh",
    "status": "in_transit",
    "rate": "rate_abc123",
    "price": 22000,
    "created_at": "2025-01-15T10:30:00Z"
  }
}
```

Response 404:
```json
{
  "errorCode": "SHIPMENT_NOT_FOUND",
  "message": "Shipment not found on GoShip"
}
```

---

### PATCH /api/Shipping/orders/{orderId}/tracking

Manually assigns a tracking number and carrier to an order. Use this for manual tracking assignment without going through GoShip.

Roles allowed: `manager`

Path parameter `orderId`: GUID of the order.

Request body:
```json
{
  "trackingNumber": "VTP987654321",
  "carrier": "Viettel Post"
}
```

Validation:
- `trackingNumber` is required, must not be empty
- `carrier` is required, must not be empty

Response 200:
```json
{
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "trackingNumber": "VTP987654321",
  "shippingCarrier": "Viettel Post",
  "shippedAt": "2025-01-15T10:30:00Z",
  "status": "shipped"
}
```

Response 400:
```json
{
  "errorCode": "VALIDATION_ERROR",
  "message": "Tracking number is required"
}
```

Response 404:
```json
{
  "errorCode": "ORDER_NOT_FOUND",
  "message": "Order not found"
}
```

---

## 20. DASHBOARD

### GET /api/Dashboard/statistics

Gets overall business statistics.

Roles allowed: `manager`, `admin`

Query parameters:
- `startDate` (optional DateTime) — filter statistics from this date
- `endDate` (optional DateTime) — filter statistics up to this date

Response 200: Statistics object with totals and metrics.

---

### GET /api/Dashboard/revenue/daily

Gets daily revenue report.

Roles allowed: `manager`, `admin`

Query parameters:
- `startDate` (optional DateTime, default: 30 days ago)
- `endDate` (optional DateTime, default: now)

Validation:
- `startDate` must be before `endDate`

Response 200: List of daily revenue data points.

Response 400: `{ "errorCode": "VALIDATION_ERROR", "message": "Start date must be before end date" }`

---

### GET /api/Dashboard/revenue/monthly

Gets monthly revenue report for a year.

Roles allowed: `manager`, `admin`

Query parameters: `year` (optional int, default: current year)

Response 200: List of monthly revenue data points.

---

### GET /api/Dashboard/popular-frames

Gets popular frames by order count.

Roles allowed: `manager`, `admin`

Query parameters:
- `limit` (default: 10, max: 50)
- `startDate` (optional DateTime)
- `endDate` (optional DateTime)

Response 200: List of popular frame objects with order counts.

---

### GET /api/Dashboard/orders/summary

Gets order summary (today, week, month).

Roles allowed: `staff`, `manager`, `admin`

Response 200: Order summary object with counts and totals for different time periods.
