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

Response 200:
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fullName": "Updated Name",
  "email": "d@mail.com",
  "phone": "987654321",
  "address": "456 Oak Ave",
  "role": "customer",
  "status": "active",
  "createdAt": "2026-01-01T00:00:00"
}
```

---

### GET /api/Users

Gets all users with pagination.

Roles allowed: `admin`, `manager`

Query parameters: `page`, `pageSize`

Response 200:
```json
{
  "totalItems": 9,
  "totalPages": 1,
  "currentPage": 1,
  "pageSize": 10,
  "items": [
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
  ]
}
```

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

Response 400 (invalid role):
```json
{
  "errorCode": "VALIDATION_ERROR",
  "message": "Invalid role. Allowed: customer, staff, manager, admin"
}
```

---

### GET /api/Users/status/{status}

Gets users filtered by status.

Roles allowed: `admin`, `manager`

Path parameter `status`: `active` or `inactive` or `suspended` or `pending`

Query parameters: `page`, `pageSize`

Response 200: Same paginated format as GET /api/Users.

Response 400:
```json
{
  "errorCode": "VALIDATION_ERROR",
  "message": "Invalid status. Allowed: active, inactive, suspended, pending"
}
```

---

### GET /api/Users/{id}

Gets a specific user by ID.

Roles allowed: `admin`, `manager`

Path parameter `id`: GUID of the user.

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

Response 404:
```json
{
  "errorCode": "USER_NOT_FOUND",
  "message": "User not found"
}
```

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

Response 201:
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fullName": "New User",
  "email": "newuser@mail.com",
  "phone": "123456789",
  "address": "123 Main St",
  "role": "staff",
  "status": "active",
  "createdAt": "2026-01-01T00:00:00"
}
```

---

### PUT /api/Users/{id}

Updates a user's profile.

Roles allowed: `admin`

Path parameter `id`: GUID of the user.

Request body:
```json
{
  "fullName": "string",
  "phone": "string",
  "address": "string"
}
```

All fields are optional.

Response 200: Updated user object.

Response 404:
```json
{
  "errorCode": "USER_NOT_FOUND",
  "message": "User not found"
}
```

---

### PUT /api/Users/{id}/status

Updates a user's status.

Roles allowed: `admin`

Path parameter `id`: GUID of the user.

Request body:
```json
{
  "status": "string"
}
```

Validation:
- `status` is required
- `status` must be one of: `active`, `inactive`, `suspended`, `pending`

Response 200: Updated user object.

---

### PUT /api/Users/{id}/role

Updates a user's role.

Roles allowed: `admin`

Path parameter `id`: GUID of the user.

Request body:
```json
{
  "role": "string"
}
```

Validation:
- `role` is required
- `role` must be one of: `customer`, `staff`, `manager`, `admin`

Response 200: Updated user object.

---

## 3. FRAMES

### GET /api/Frames

Gets all available frames with pagination.

Roles allowed: Public (no authentication required)

Query parameters: `page`, `pageSize`

Response 200: Paginated list of frames.

---

### GET /api/Frames/{id}

Gets a specific frame by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the frame.

Response 200: Frame object with details.

Response 404:
```json
{
  "errorCode": "FRAME_NOT_FOUND",
  "message": "Frame not found or is not available"
}
```

---

### GET /api/Frames/{id}/media

Gets all media (images/videos) for a specific frame.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the frame.

Response 200: List of media items.

Response 404:
```json
{
  "errorCode": "FRAME_NOT_FOUND",
  "message": "Frame not found or is not available"
}
```

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
  "lensWidth": 0,
  "bridgeWidth": 0,
  "frameWidth": 0,
  "templeLength": 0,
  "shape": "string",
  "size": "string",
  "basePrice": 0.0,
  "stockQuantity": 0,
  "reorderLevel": 0
}
```

Validation:
- `frameName` is required
- Size attributes (lensWidth, bridgeWidth, frameWidth, templeLength) are validated for valid ranges
- `stockQuantity` defaults to 0 if not provided
- `reorderLevel` defaults to 5 if not provided

Response 201: Created frame object.

Response 400:
```json
{
  "errorCode": "VALIDATION_ERROR",
  "message": "Frame name is required"
}
```

---

### PUT /api/Frames/{id}

Updates an existing frame.

Roles allowed: `manager`

Path parameter `id`: GUID of the frame.

Request body:
```json
{
  "frameName": "string",
  "brandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "materialId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "lensWidth": 0,
  "bridgeWidth": 0,
  "frameWidth": 0,
  "templeLength": 0,
  "shape": "string",
  "size": "string",
  "basePrice": 0.0,
  "status": "string",
  "stockQuantity": 0,
  "reorderLevel": 0
}
```

All fields are optional. Only provided fields will be updated.

Validation:
- `status` if provided must be one of: `available`, `inactive`, `out_of_stock`
- Size attributes are validated for valid ranges if provided

Response 200: Updated frame object.

Response 404:
```json
{
  "errorCode": "FRAME_NOT_FOUND",
  "message": "Frame not found"
}
```

---

### DELETE /api/Frames/{id}

Soft deletes a frame by setting its status to inactive.

Roles allowed: `manager`

Path parameter `id`: GUID of the frame.

Response 204: No content.

Response 404:
```json
{
  "errorCode": "FRAME_NOT_FOUND",
  "message": "Frame not found"
}
```

---

### GET /api/Frames/inventory/low-stock

Gets frames with stock below their reorder level.

Roles allowed: `manager`, `admin`

Response 200: List of frames with low stock.

---

### GET /api/Frames/inventory/out-of-stock

Gets frames that are out of stock.

Roles allowed: `manager`, `admin`

Response 200: List of out-of-stock frames.

---

### PATCH /api/Frames/{id}/inventory

Updates stock quantity for a frame.

Roles allowed: `manager`, `admin`

Path parameter `id`: GUID of the frame.

Request body:
```json
{
  "quantity": 0,
  "reorderLevel": 0
}
```

Validation:
- `quantity` must be >= 0
- `reorderLevel` is optional

Response 200: Updated frame object.

Response 404:
```json
{
  "errorCode": "FRAME_NOT_FOUND",
  "message": "Frame not found"
}
```

---

## 4. FRAME MEDIA

### GET /api/FrameMedia/frame/{frameId}

Gets all media for a specific frame. Optionally filter by color.

Roles allowed: Public (no authentication required)

Path parameter `frameId`: GUID of the frame.

Query parameter `colorId` (optional): GUID of a color to filter by.

Response 200:
```json
[
  {
    "mediaId": "8d96586d-b2f9-450c-aeff-18f2b16f716d",
    "frameId": "668e34d7-b23e-48b0-ac51-0c833522dca8",
    "mediaUrl": "https://res.cloudinary.com/example/image.png",
    "mediaType": "image",
    "colorId": null,
    "colorName": null,
    "hexCode": null
  }
]
```

Response 404:
```json
{
  "errorCode": "FRAME_NOT_FOUND",
  "message": "Frame not found"
}
```

---

### GET /api/FrameMedia/{id}

Gets a specific media item by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the media item.

Response 200: Single media object.

Response 404:
```json
{
  "errorCode": "MEDIA_NOT_FOUND",
  "message": "Media not found"
}
```

---

### POST /api/FrameMedia

Adds a new media item to a frame via URL.

Roles allowed: `manager`

Request body:
```json
{
  "frameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "mediaUrl": "string",
  "mediaType": "string",
  "colorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

Validation:
- `frameId` must reference an existing frame
- `mediaUrl` is required
- `mediaType` must be one of: `image`, `video`, `thumbnail`, `gallery` (defaults to `image`)
- `colorId` is optional

Response 201: Created media object.

---

### POST /api/FrameMedia/batch

Adds multiple media items to a frame at once.

Roles allowed: `manager`

Request body:
```json
{
  "frameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "mediaItems": [
    {
      "mediaUrl": "string",
      "mediaType": "string",
      "colorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ]
}
```

Validation:
- `frameId` must reference an existing frame
- At least one media item is required
- Each item: `mediaUrl` is required, `mediaType` must be valid if provided

Response 201: List of created media objects.

---

### PUT /api/FrameMedia/{id}

Updates a media item.

Roles allowed: `manager`

Path parameter `id`: GUID of the media item.

Request body:
```json
{
  "mediaUrl": "string",
  "mediaType": "string",
  "colorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

All fields are optional. `mediaType` if provided must be one of: `image`, `video`, `thumbnail`, `gallery`.

Response 200: Updated media object.

Response 404:
```json
{
  "errorCode": "MEDIA_NOT_FOUND",
  "message": "Media not found"
}
```

---

### DELETE /api/FrameMedia/{id}

Deletes a single media item.

Roles allowed: `manager`

Path parameter `id`: GUID of the media item.

Response 204: No content.

Response 404:
```json
{
  "errorCode": "MEDIA_NOT_FOUND",
  "message": "Media not found"
}
```

---

### DELETE /api/FrameMedia/frame/{frameId}

Deletes all media for a frame.

Roles allowed: `manager`

Path parameter `frameId`: GUID of the frame.

Response 204: No content.

Response 404:
```json
{
  "errorCode": "FRAME_NOT_FOUND",
  "message": "Frame not found"
}
```

---

### POST /api/FrameMedia/upload/{frameId}

Uploads an image file to Cloudinary and creates a media record for a frame.

Roles allowed: `manager`

Path parameter `frameId`: GUID of the frame.

Form data: `file` (the image file)

Query parameters:
- `mediaType` (optional, default: `image`): `image`, `video`, `thumbnail`, `gallery`
- `colorId` (optional): GUID of a color to associate

Validation:
- `frameId` must reference an existing frame
- File is required and must not be empty
- Maximum file size: 10 MB
- Allowed file types: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`

Response 201:
```json
{
  "mediaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "frameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "mediaUrl": "https://res.cloudinary.com/example/image.png",
  "mediaType": "image",
  "colorId": null,
  "publicId": "spectra/frames/abc123/image"
}
```

---

### POST /api/FrameMedia/upload-multiple/{frameId}

Uploads multiple image files to Cloudinary and creates media records.

Roles allowed: `manager`

Path parameter `frameId`: GUID of the frame.

Form data: `files` (list of image files)

Query parameters:
- `mediaType` (optional, default: `image`)
- `colorId` (optional)

Validation:
- Maximum 10 files per request
- Each file: max 10 MB, allowed types: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`

Response 201:
```json
{
  "uploadedMedia": [
    {
      "mediaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "frameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "mediaUrl": "https://res.cloudinary.com/example/image.png",
      "mediaType": "image",
      "colorId": null,
      "publicId": "spectra/frames/abc123/image"
    }
  ],
  "errors": null
}
```

---

### POST /api/FrameMedia/upload

Uploads an image to Cloudinary without associating it with a frame. Returns a URL for later use.

Roles allowed: `manager`

Form data: `file` (the image file)

Query parameter `folder` (optional, default: `spectra/products`)

Validation:
- File is required and must not be empty
- Maximum file size: 10 MB
- Allowed file types: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`

Response 200:
```json
{
  "success": true,
  "url": "https://res.cloudinary.com/example/image.png",
  "publicId": "spectra/products/image"
}
```

---

### DELETE /api/FrameMedia/cloudinary/{publicId}

Deletes an image from Cloudinary by its public ID.

Roles allowed: `manager`

Path parameter `publicId`: The Cloudinary public ID (supports path segments via wildcard route).

Validation:
- `publicId` is required

Response 204: No content.

Response 400:
```json
{
  "errorCode": "DELETE_ERROR",
  "message": "Failed to delete image from Cloudinary"
}
```

---

## 5. BRANDS

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

Response 404:
```json
{
  "errorCode": "BRAND_NOT_FOUND",
  "message": "Brand not found"
}
```

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

Path parameter `id`: GUID of the brand.

Request body:
```json
{
  "brandName": "string"
}
```

Response 200: Updated brand object.

Response 404:
```json
{
  "errorCode": "BRAND_NOT_FOUND",
  "message": "Brand not found"
}
```

---

### DELETE /api/Brands/{id}

Soft deletes a brand.

Roles allowed: `manager`

Path parameter `id`: GUID of the brand.

Response 204: No content.

Response 404:
```json
{
  "errorCode": "BRAND_NOT_FOUND",
  "message": "Brand not found"
}
```

---

## 6. MATERIALS

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

Response 404:
```json
{
  "errorCode": "MATERIAL_NOT_FOUND",
  "message": "Material not found"
}
```

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

Path parameter `id`: GUID of the material.

Request body:
```json
{
  "materialName": "string"
}
```

Response 200: Updated material object.

Response 404:
```json
{
  "errorCode": "MATERIAL_NOT_FOUND",
  "message": "Material not found"
}
```

---

### DELETE /api/Materials/{id}

Soft deletes a material.

Roles allowed: `manager`

Path parameter `id`: GUID of the material.

Response 204: No content.

Response 404:
```json
{
  "errorCode": "MATERIAL_NOT_FOUND",
  "message": "Material not found"
}
```

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

Response 404:
```json
{
  "errorCode": "COLOR_NOT_FOUND",
  "message": "Color not found"
}
```

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

Path parameter `id`: GUID of the color.

Request body:
```json
{
  "colorName": "string",
  "hexCode": "string"
}
```

All fields are optional.

Response 200: Updated color object.

Response 404:
```json
{
  "errorCode": "COLOR_NOT_FOUND",
  "message": "Color not found"
}
```

---

### DELETE /api/Colors/{id}

Soft deletes a color.

Roles allowed: `manager`

Path parameter `id`: GUID of the color.

Response 204: No content.

Response 404:
```json
{
  "errorCode": "COLOR_NOT_FOUND",
  "message": "Color not found"
}
```

---

## 8. LENS TYPES

### GET /api/LensTypes

Gets all lens types with pagination.

Roles allowed: Public (no authentication required)

Query parameters: `page`, `pageSize`

Response 200: Paginated list of lens types.

---

### GET /api/LensTypes/{id}

Gets a specific lens type by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the lens type.

Response 200: Lens type object.

Response 404:
```json
{
  "errorCode": "LENS_TYPE_NOT_FOUND",
  "message": "Lens type not found"
}
```

---

### GET /api/LensTypes/prescription-required

Gets all lens types that require a prescription.

Roles allowed: Public (no authentication required)

Response 200: List of lens types.

---

### GET /api/LensTypes/no-prescription

Gets all lens types that do not require a prescription.

Roles allowed: Public (no authentication required)

Response 200: List of lens types.

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
- `basePrice` if provided cannot be negative

Response 201: Created lens type object.

---

### PUT /api/LensTypes/{id}

Updates an existing lens type.

Roles allowed: `manager`

Path parameter `id`: GUID of the lens type.

Request body:
```json
{
  "lensSpecification": "string",
  "requiresPrescription": true,
  "basePrice": 0.0
}
```

All fields are optional. `basePrice` if provided cannot be negative.

Response 200: Updated lens type object.

Response 404:
```json
{
  "errorCode": "LENS_TYPE_NOT_FOUND",
  "message": "Lens type not found"
}
```

---

### DELETE /api/LensTypes/{id}

Deletes a lens type. Only allowed if the lens type is not used in any orders or preorders.

Roles allowed: `manager`

Path parameter `id`: GUID of the lens type.

Response 204: No content.

Response 400:
```json
{
  "errorCode": "LENS_TYPE_IN_USE",
  "message": "Cannot delete lens type because it is used in existing orders or preorders"
}
```

Response 404:
```json
{
  "errorCode": "LENS_TYPE_NOT_FOUND",
  "message": "Lens type not found"
}
```

---

## 9. LENS FEATURES

### GET /api/LensFeatures

Gets all lens features with pagination.

Roles allowed: Public (no authentication required)

Query parameters: `page`, `pageSize`

Response 200: Paginated list of lens features.

---

### GET /api/LensFeatures/{id}

Gets a specific lens feature by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the lens feature.

Response 200: Lens feature object.

Response 404:
```json
{
  "errorCode": "LENS_FEATURE_NOT_FOUND",
  "message": "Lens feature not found"
}
```

---

### POST /api/LensFeatures/calculate-price

Calculates the total price based on a frame base price, lens type, lens feature, and lens index.

Roles allowed: Public (no authentication required)

Request body:
```json
{
  "basePrice": 0.0,
  "lensFeatureId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "lensTypeId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "lensIndexId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

Validation:
- `basePrice` cannot be negative
- `lensFeatureId`, `lensTypeId`, `lensIndexId` are all optional

Response 200:
```json
{
  "basePrice": 120.0,
  "featureExtraPrice": 10.0,
  "lensTypeExtraPrice": 25.0,
  "lensIndexExtraPrice": 15.0,
  "totalPrice": 170.0
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
- `extraPrice` is validated (cannot be negative)

Response 201: Created lens feature object.

---

### PUT /api/LensFeatures/{id}

Updates an existing lens feature.

Roles allowed: `manager`

Path parameter `id`: GUID of the lens feature.

Request body:
```json
{
  "featureSpecification": "string",
  "extraPrice": 0.0
}
```

All fields are optional. `extraPrice` is validated if provided.

Response 200: Updated lens feature object.

Response 404:
```json
{
  "errorCode": "LENS_FEATURE_NOT_FOUND",
  "message": "Lens feature not found"
}
```

---

### DELETE /api/LensFeatures/{id}

Deletes a lens feature. Only allowed if the lens feature is not used in any orders or preorders.

Roles allowed: `manager`

Path parameter `id`: GUID of the lens feature.

Response 204: No content.

Response 400:
```json
{
  "errorCode": "LENS_FEATURE_IN_USE",
  "message": "Cannot delete lens feature because it is used in existing orders or preorders"
}
```

Response 404:
```json
{
  "errorCode": "LENS_FEATURE_NOT_FOUND",
  "message": "Lens feature not found"
}
```

---

## 10. LENS INDICES

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

Response 404:
```json
{
  "errorCode": "LENS_INDEX_NOT_FOUND",
  "message": "Lens index not found"
}
```

---

### GET /api/LensIndices/compatible

Gets lens indices compatible with a given prescription sphere value.

Roles allowed: Public (no authentication required)

Query parameter `sphere` (required): The prescription sphere value (double).

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
  "minPrescription": 0.0,
  "maxPrescription": 0.0
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

Path parameter `id`: GUID of the lens index.

Request body:
```json
{
  "indexValue": 1.67,
  "name": "string",
  "description": "string",
  "additionalPrice": 0.0,
  "minPrescription": 0.0,
  "maxPrescription": 0.0
}
```

All fields are optional.

Response 200: Updated lens index object.

Response 404:
```json
{
  "errorCode": "LENS_INDEX_NOT_FOUND",
  "message": "Lens index not found"
}
```

---

### DELETE /api/LensIndices/{id}

Soft deletes a lens index.

Roles allowed: `manager`

Path parameter `id`: GUID of the lens index.

Response 204: No content.

Response 404:
```json
{
  "errorCode": "LENS_INDEX_NOT_FOUND",
  "message": "Lens index not found"
}
```

---

## 11. PRESCRIPTIONS

### POST /api/Prescriptions

Creates a new prescription for the current user.

Roles allowed: `customer`

Request body:
```json
{
  "sphereRight": 0.0,
  "cylinderRight": 0.0,
  "axisRight": 0,
  "addRight": 0.0,
  "sphereLeft": 0.0,
  "cylinderLeft": 0.0,
  "axisLeft": 0,
  "addLeft": 0.0,
  "pupillaryDistance": 0,
  "doctorName": "string",
  "clinicName": "string",
  "expirationDate": "2026-12-31T00:00:00"
}
```

Validation:
- At least one sphere value (left or right) is required
- Prescription values are validated for valid optometric ranges

Response 201:
```json
{
  "prescriptionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sphereRight": -2.0,
  "cylinderRight": -0.5,
  "axisRight": 90,
  "addRight": null,
  "sphereLeft": -1.75,
  "cylinderLeft": -0.25,
  "axisLeft": 85,
  "addLeft": null,
  "pupillaryDistance": 63,
  "doctorName": "Dr. Smith",
  "clinicName": "Eye Care Clinic",
  "expirationDate": "2026-12-31T00:00:00",
  "createdAt": "2026-02-24T10:09:01.823",
  "isExpired": false,
  "daysUntilExpiration": 310
}
```

---

### GET /api/Prescriptions/my

Gets all prescriptions for the current user with pagination.

Roles allowed: `customer`

Query parameters: `page`, `pageSize`

Response 200:
```json
{
  "totalItems": 2,
  "totalPages": 1,
  "currentPage": 1,
  "pageSize": 10,
  "items": [
    {
      "prescriptionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "sphereRight": -2.0,
      "cylinderRight": -0.5,
      "axisRight": 90,
      "addRight": null,
      "sphereLeft": -1.75,
      "cylinderLeft": -0.25,
      "axisLeft": 85,
      "addLeft": null,
      "pupillaryDistance": 63,
      "doctorName": "Dr. Smith",
      "clinicName": "Eye Care Clinic",
      "expirationDate": "2026-12-31T00:00:00",
      "createdAt": "2026-02-24T10:09:01.823",
      "isExpired": false,
      "daysUntilExpiration": 310
    }
  ]
}
```

---

### GET /api/Prescriptions/my/valid

Gets only valid (non-expired) prescriptions for the current user.

Roles allowed: `customer`

Response 200: List of valid prescription objects.

---

### GET /api/Prescriptions/{id}

Gets a specific prescription by ID.

Roles allowed: Any authenticated user (customers can only view their own)

Path parameter `id`: GUID of the prescription.

Restrictions:
- Customers can only access their own prescriptions (returns 403 otherwise)
- Staff/Manager/Admin can access any prescription

Response 200: Prescription object.

Response 403: Forbidden (customer accessing another user's prescription).

Response 404:
```json
{
  "errorCode": "PRESCRIPTION_NOT_FOUND",
  "message": "Prescription not found"
}
```

---

### PUT /api/Prescriptions/{id}

Updates an existing prescription.

Roles allowed: `customer`

Path parameter `id`: GUID of the prescription.

Request body:
```json
{
  "sphereRight": 0.0,
  "cylinderRight": 0.0,
  "axisRight": 0,
  "addRight": 0.0,
  "sphereLeft": 0.0,
  "cylinderLeft": 0.0,
  "axisLeft": 0,
  "addLeft": 0.0,
  "pupillaryDistance": 0,
  "doctorName": "string",
  "clinicName": "string",
  "expirationDate": "2026-12-31T00:00:00"
}
```

All fields are optional. Prescription values are validated.

Response 200: Updated prescription object.

Response 404:
```json
{
  "errorCode": "PRESCRIPTION_NOT_FOUND",
  "message": "Prescription not found or you don't have permission to update it"
}
```

---

### DELETE /api/Prescriptions/{id}

Deletes a prescription. Only allowed if the prescription is not used in any orders or preorders.

Roles allowed: `customer`

Path parameter `id`: GUID of the prescription.

Response 204: No content.

Response 400:
```json
{
  "errorCode": "PRESCRIPTION_IN_USE",
  "message": "Cannot delete prescription because it is used in existing orders or preorders"
}
```

Response 404:
```json
{
  "errorCode": "PRESCRIPTION_NOT_FOUND",
  "message": "Prescription not found or you don't have permission to delete it"
}
```

---

### GET /api/Prescriptions/{id}/validate

Checks if a prescription is valid (not expired).

Roles allowed: Any authenticated user

Path parameter `id`: GUID of the prescription.

Response 200:
```json
{
  "prescriptionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "isValid": true,
  "isExpired": false,
  "daysUntilExpiration": 310,
  "expirationDate": "2026-12-31T00:00:00",
  "message": "This prescription is valid"
}
```

---

### GET /api/Prescriptions/user/{userId}

Gets prescriptions for a specific user.

Roles allowed: `staff`, `manager`, `admin`

Path parameter `userId`: GUID of the user.

Query parameters: `page`, `pageSize`

Response 200: Paginated list of prescription objects.

---

## 12. ORDERS

### POST /api/Orders

Creates a new order.

Roles allowed: `customer`

Request body:
```json
{
  "shippingAddress": "string",
  "shippingMethod": "string",
  "items": [
    {
      "frameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "lensTypeId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "featureId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "lensIndexId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "prescriptionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "quantity": 1,
      "selectedColorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ]
}
```

Validation:
- `shippingAddress` is required
- `items` must contain at least one item
- `shippingMethod` defaults to `standard` if not provided
- Each item's `frameId` is required; `lensTypeId`, `featureId`, `lensIndexId`, `prescriptionId`, `selectedColorId` are optional
- `quantity` defaults to 1
- Order items are validated (frame exists, stock available, prescription valid if lens requires it, etc.)
- Shipping fee is calculated based on the shipping method and order subtotal

Response 201:
```json
{
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "totalAmount": 145.0,
  "shippingAddress": "123 Main St",
  "status": "pending",
  "createdAt": "2026-02-24T10:09:01.823",
  "itemCount": 1
}
```

---

### GET /api/Orders/my

Gets the current user's orders with pagination.

Roles allowed: `customer`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of order objects.

---

### GET /api/Orders/{id}

Gets a specific order by ID with full details.

Roles allowed: Any authenticated user (customers can only view their own)

Path parameter `id`: GUID of the order.

Restrictions:
- Customers can only access their own orders (returns 403 otherwise)
- Staff/Manager/Admin can access any order

Response 200: Order object with items and details.

Response 403: Forbidden (customer accessing another user's order).

Response 404:
```json
{
  "errorCode": "ORDER_NOT_FOUND",
  "message": "Order not found"
}
```

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
- `status` must be one of: `pending`, `confirmed`, `processing`, `shipped`, `delivered`, `cancelled`

Response 200: Updated order object.

Response 400:
```json
{
  "errorCode": "VALIDATION_ERROR",
  "message": "Invalid status. Allowed values: pending, confirmed, processing, shipped, delivered, cancelled"
}
```

Response 404:
```json
{
  "errorCode": "UPDATE_FAILED",
  "message": "Order not found or status transition not allowed for your role"
}
```

---

### DELETE /api/Orders/{id}

Cancels an order by setting its status to `cancelled`.

Roles allowed: `manager`, `admin`

Path parameter `id`: GUID of the order.

Response 200: Updated order object with `cancelled` status.

Response 404:
```json
{
  "errorCode": "CANCEL_FAILED",
  "message": "Order not found or cannot be cancelled"
}
```

---

## 13. PREORDERS

### POST /api/Preorders

Creates a new preorder.

Roles allowed: `customer`

Request body:
```json
{
  "campaignId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "expectedDate": "2026-06-01T00:00:00",
  "items": [
    {
      "frameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "lensTypeId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "featureId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "lensIndexId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "prescriptionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "quantity": 1,
      "selectedColorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ]
}
```

Validation:
- `items` must contain at least one item
- `campaignId` is optional; if provided, the campaign must exist, be active, have available slots, and items must match campaign frame/quantity rules
- `expectedDate` is optional
- Preorder items are validated similarly to order items

Response 201: Created preorder object.

---

### GET /api/Preorders/my

Gets the current user's preorders with pagination.

Roles allowed: `customer`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of preorder objects.

---

### GET /api/Preorders/{id}

Gets a specific preorder by ID with full details.

Roles allowed: Any authenticated user (customers can only view their own)

Path parameter `id`: GUID of the preorder.

Restrictions:
- Customers can only access their own preorders (returns 403 otherwise)
- Staff/Manager/Admin can access any preorder

Response 200: Preorder object with items and details.

Response 403: Forbidden (customer accessing another user's preorder).

Response 404:
```json
{
  "errorCode": "PREORDER_NOT_FOUND",
  "message": "Preorder not found"
}
```

---

### DELETE /api/Preorders/{id}

Cancels a preorder. Only if it has not been paid.

Roles allowed: `customer`

Path parameter `id`: GUID of the preorder.

Restrictions:
- Customer must own the preorder
- Preorder cannot be cancelled if already paid

Response 204: No content.

Response 400:
```json
{
  "errorCode": "CANCEL_FAILED",
  "message": "Cannot cancel preorder. It may have already been paid."
}
```

Response 404:
```json
{
  "errorCode": "PREORDER_NOT_FOUND",
  "message": "Preorder not found"
}
```

---

### GET /api/Preorders

Gets all preorders with pagination.

Roles allowed: `staff`, `manager`, `admin`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of preorder objects.

---

### PUT /api/Preorders/{id}/status

Updates preorder status.

Roles allowed: `staff`, `manager`, `admin`

Path parameter `id`: GUID of the preorder.

Request body:
```json
{
  "status": "string"
}
```

Validation:
- `status` is required
- `status` must be one of: `pending`, `confirmed`, `paid`, `converted`, `cancelled`

Response 200: Updated preorder object.

Response 400:
```json
{
  "errorCode": "VALIDATION_ERROR",
  "message": "Invalid status. Allowed values: pending, confirmed, paid, converted, cancelled"
}
```

Response 404:
```json
{
  "errorCode": "UPDATE_FAILED",
  "message": "Preorder not found or status update not allowed"
}
```

---

### POST /api/Preorders/{id}/convert

Converts a preorder to an order.

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

Response 200: Created order object (converted from preorder).

Response 400:
```json
{
  "errorCode": "CONVERSION_FAILED",
  "message": "Preorder cannot be converted. It must be in 'paid' or 'confirmed' status."
}
```

Response 404:
```json
{
  "errorCode": "CONVERSION_FAILED",
  "message": "Failed to convert preorder to order"
}
```

---

## 14. PAYMENTS

### POST /api/Payments

Creates a new payment and returns a VNPay payment URL (if method is `vnpay`).

Roles allowed: `customer`

Request body:
```json
{
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "preorderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "paymentMethod": "string"
}
```

Validation:
- Exactly one of `orderId` or `preorderId` must be provided (not both, not neither)
- `paymentMethod` must be one of: `vnpay`, `cash`, `bank_transfer`

Response 201:
```json
{
  "paymentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "preorderId": null,
  "amount": 145.0,
  "paymentMethod": "vnpay",
  "paymentStatus": "pending",
  "paymentUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?...",
  "paidAt": null
}
```

---

### GET /api/Payments/my

Gets the current user's payments with pagination.

Roles allowed: `customer`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of payment objects.

---

### GET /api/Payments/{id}

Gets a specific payment by ID.

Roles allowed: Any authenticated user

Path parameter `id`: GUID of the payment.

Response 200: Payment object.

Response 404:
```json
{
  "errorCode": "PAYMENT_NOT_FOUND",
  "message": "Payment not found"
}
```

---

### GET /api/Payments/vnpay-return

VNPay return URL handler. Processes the VNPay callback and redirects to the frontend.

Roles allowed: Public (no authentication required, called by VNPay redirect)

Query parameters: All VNPay callback parameters (`vnp_TxnRef`, `vnp_ResponseCode`, `vnp_TransactionNo`, `vnp_SecureHash`, `vnp_Amount`, `vnp_OrderInfo`, `vnp_PayDate`, `vnp_BankCode`)

Behavior:
- Verifies the VNPay signature
- Completes the payment if signature is valid and response code is `00`
- Redirects to the frontend at `/payment/return` with payment result parameters

---

### POST /api/Payments/vnpay-ipn

VNPay IPN (Instant Payment Notification) handler.

Roles allowed: Public (no authentication required, called by VNPay server)

This endpoint is called server-to-server by VNPay. It verifies the signature, checks for duplicate processing, and confirms the payment.

Response 200:
```json
{
  "rspCode": "00",
  "message": "Confirm Success"
}
```

---

### PUT /api/Payments/{id}/status

Updates payment status.

Roles allowed: `staff`, `manager`, `admin`

Path parameter `id`: GUID of the payment.

Request body:
```json
{
  "status": "string"
}
```

Validation:
- `status` is required
- `status` must be one of: `pending`, `processing`, `completed`, `failed`, `cancelled`, `refunded`

Response 200: Updated payment object.

Response 404:
```json
{
  "errorCode": "UPDATE_FAILED",
  "message": "Payment not found or status update not allowed"
}
```

---

### GET /api/Payments/order/{orderId}

Gets all payments for a specific order.

Roles allowed: `staff`, `manager`, `admin`

Path parameter `orderId`: GUID of the order.

Response 200: List of payment objects.

---

### GET /api/Payments/preorder/{preorderId}

Gets all payments for a specific preorder.

Roles allowed: `staff`, `manager`, `admin`

Path parameter `preorderId`: GUID of the preorder.

Response 200: List of payment objects.

---

## 15. COMPLAINTS

### POST /api/Complaints

Submits a new complaint/return request.

Roles allowed: `customer`

Request body:
```json
{
  "orderItemId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "requestType": "string",
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
  "requestId": "39f952dc-8f3b-4acc-b3ee-10b88279e3ff",
  "userId": "e58d29b3-ad7a-41ed-93a1-e3cf683b3ab6",
  "orderItemId": "cf93ef90-23cd-4f9b-93be-f2b61ee76011",
  "requestType": "return",
  "reason": "Color slightly different from expectation",
  "mediaUrl": "https://example.com/photo.jpg",
  "status": "pending",
  "createdAt": "2026-02-24T10:09:01.823",
  "canModify": true
}
```

---

### GET /api/Complaints/my

Gets all complaints for the current user with pagination.

Roles allowed: `customer`

Query parameters: `page`, `pageSize`

Response 200:
```json
{
  "totalItems": 5,
  "totalPages": 1,
  "currentPage": 1,
  "pageSize": 10,
  "items": [
    {
      "requestId": "39f952dc-8f3b-4acc-b3ee-10b88279e3ff",
      "userId": "e58d29b3-ad7a-41ed-93a1-e3cf683b3ab6",
      "orderItemId": "cf93ef90-23cd-4f9b-93be-f2b61ee76011",
      "requestType": "return",
      "reason": "Color slightly different from expectation",
      "mediaUrl": "https://example.com/photo.jpg",
      "status": "pending",
      "createdAt": "2026-02-24T10:09:01.823",
      "canModify": true
    }
  ]
}
```

---

### GET /api/Complaints/{id}

Gets a specific complaint by ID.

Roles allowed: Any authenticated user (customers can only view their own)

Path parameter `id`: GUID of the complaint.

Restrictions:
- Customers can only access their own complaints (returns 403 otherwise)
- Staff/Manager/Admin can access any complaint

Response 200:
```json
{
  "requestId": "39f952dc-8f3b-4acc-b3ee-10b88279e3ff",
  "userId": "e58d29b3-ad7a-41ed-93a1-e3cf683b3ab6",
  "orderItemId": "cf93ef90-23cd-4f9b-93be-f2b61ee76011",
  "requestType": "return",
  "reason": "Color slightly different from expectation",
  "mediaUrl": "https://example.com/photo.jpg",
  "status": "pending",
  "createdAt": "2026-02-24T10:09:01.823",
  "canModify": true
}
```

Response 404:
```json
{
  "errorCode": "COMPLAINT_NOT_FOUND",
  "message": "Complaint not found"
}
```

---

### PUT /api/Complaints/{id}

Updates a complaint. Only allowed if the complaint status permits modification (`canModify` is `true`, typically when status is `pending`).

Roles allowed: `customer`

Path parameter `id`: GUID of the complaint.

Request body:
```json
{
  "requestType": "string",
  "reason": "string",
  "mediaUrl": "string"
}
```

All fields are optional.

Validation:
- `requestType` if provided must be one of: `return`, `exchange`, `refund`, `complaint`, `warranty`
- Customer must own the complaint
- Complaint must still be modifiable

Response 200: Updated complaint object.

Response 404:
```json
{
  "errorCode": "UPDATE_FAILED",
  "message": "Complaint not found, you don't have permission, or it can no longer be modified"
}
```

---

### GET /api/Complaints

Gets all complaints with pagination.

Roles allowed: `staff`, `manager`, `admin`

Query parameters: `page`, `pageSize`

Response 200:
```json
{
  "totalItems": 5,
  "totalPages": 1,
  "currentPage": 1,
  "pageSize": 10,
  "items": [
    {
      "requestId": "39f952dc-8f3b-4acc-b3ee-10b88279e3ff",
      "userId": "e58d29b3-ad7a-41ed-93a1-e3cf683b3ab6",
      "orderItemId": "cf93ef90-23cd-4f9b-93be-f2b61ee76011",
      "requestType": "return",
      "reason": "Color slightly different from expectation",
      "mediaUrl": "https://example.com/photo.jpg",
      "status": "pending",
      "createdAt": "2026-02-24T10:09:01.823",
      "canModify": true
    }
  ]
}
```

---

### GET /api/Complaints/status/{status}

Gets complaints filtered by status.

Roles allowed: `staff`, `manager`, `admin`

Path parameter `status`: `pending` or `under_review` or `approved` or `rejected` or `in_progress` or `resolved` or `cancelled`

Query parameters: `page`, `pageSize`

Response 200: Same paginated format as GET /api/Complaints.

Response 400:
```json
{
  "errorCode": "VALIDATION_ERROR",
  "message": "Invalid status. Allowed: pending, under_review, approved, rejected, in_progress, resolved, cancelled"
}
```

---

### PUT /api/Complaints/{id}/status

Updates a complaint's status.

Roles allowed: `staff`, `manager`, `admin`

Path parameter `id`: GUID of the complaint.

Request body:
```json
{
  "status": "string"
}
```

Validation:
- `status` is required
- `status` must be one of: `pending`, `under_review`, `approved`, `rejected`, `in_progress`, `resolved`, `cancelled`

Response 200: Updated complaint object.

Response 404:
```json
{
  "errorCode": "UPDATE_FAILED",
  "message": "Complaint not found"
}
```

---

## 16. PRODUCT REVIEWS

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

Response 200: Review summary object (average rating, count, rating distribution).

---

### POST /api/ProductReviews

Creates a new review.

Roles allowed: `customer`

Request body:
```json
{
  "frameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "orderItemId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "rating": 5,
  "title": "string",
  "comment": "string"
}
```

Validation:
- `rating` must be between 0 and 5
- `frameId` is required
- `orderItemId` is optional
- The service may throw an `InvalidOperationException` if the user has already reviewed this frame or if the purchase is not verified

Response 201: Created review object.

Response 400:
```json
{
  "errorCode": "REVIEW_ERROR",
  "message": "Error description from service"
}
```

---

### GET /api/ProductReviews/my-reviews

Gets reviews by the current user with pagination.

Roles allowed: `customer`

Query parameters: `page`, `pageSize`

Response 200: Paginated list of review objects.

---

### PUT /api/ProductReviews/{id}

Updates a review. Only the review's author can update it.

Roles allowed: `customer`

Path parameter `id`: GUID of the review.

Request body:
```json
{
  "rating": 4,
  "title": "string",
  "comment": "string"
}
```

All fields are optional.

Response 200: Updated review object.

Response 404:
```json
{
  "errorCode": "REVIEW_NOT_FOUND",
  "message": "Review not found or you don't have permission to update it"
}
```

---

### DELETE /api/ProductReviews/{id}

Deletes a review. Only the review's author can delete it.

Roles allowed: `customer`

Path parameter `id`: GUID of the review.

Response 204: No content.

Response 404:
```json
{
  "errorCode": "REVIEW_NOT_FOUND",
  "message": "Review not found or you don't have permission to delete it"
}
```

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

Hides a review (moderation action).

Roles allowed: `manager`, `admin`

Path parameter `id`: GUID of the review.

Response 200:
```json
{
  "message": "Review hidden successfully"
}
```

Response 404:
```json
{
  "errorCode": "REVIEW_NOT_FOUND",
  "message": "Review not found"
}
```

---

## 17. PREORDER CAMPAIGNS

### GET /api/PreorderCampaigns/active

Gets all active preorder campaigns.

Roles allowed: Public (no authentication required)

Response 200: List of active campaign objects with their associated frames.

---

### GET /api/PreorderCampaigns/{id}

Gets a specific campaign by ID.

Roles allowed: Public (no authentication required)

Path parameter `id`: GUID of the campaign.

Response 200: Campaign object with details.

Response 404:
```json
{
  "errorCode": "CAMPAIGN_NOT_FOUND",
  "message": "Campaign not found"
}
```

---

### POST /api/PreorderCampaigns

Creates a new preorder campaign.

Roles allowed: `manager`

Request body:
```json
{
  "campaignName": "string",
  "description": "string",
  "startDate": "2026-03-01T00:00:00",
  "endDate": "2026-04-01T00:00:00",
  "maxSlots": 100,
  "estimatedDeliveryDate": "2026-05-01T00:00:00",
  "frames": [
    {
      "frameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "campaignPrice": 99.0,
      "maxQuantityPerOrder": 2
    }
  ]
}
```

Validation:
- `campaignName` is required
- `startDate` must be before `endDate`
- `frames` must contain at least one frame
- `maxQuantityPerOrder` defaults to 2 if not provided

Response 201: Created campaign object.

---

### PUT /api/PreorderCampaigns/{id}

Updates an existing campaign.

Roles allowed: `manager`

Path parameter `id`: GUID of the campaign.

Request body:
```json
{
  "campaignName": "string",
  "description": "string",
  "maxSlots": 100,
  "estimatedDeliveryDate": "2026-05-01T00:00:00"
}
```

All fields are optional.

Response 200: Updated campaign object.

Response 404:
```json
{
  "errorCode": "CAMPAIGN_NOT_FOUND",
  "message": "Campaign not found"
}
```

---

### PATCH /api/PreorderCampaigns/{id}/end

Ends a campaign.

Roles allowed: `manager`

Path parameter `id`: GUID of the campaign.

Response 200:
```json
{
  "message": "Campaign ended successfully"
}
```

Response 404:
```json
{
  "errorCode": "CAMPAIGN_NOT_FOUND",
  "message": "Campaign not found"
}
```

---

## 18. DASHBOARD

### GET /api/Dashboard/statistics

Gets overall business statistics.

Roles allowed: `manager`, `admin`

Query parameters:
- `startDate` (optional): Start date filter
- `endDate` (optional): End date filter

Response 200:
```json
{
  "totalOrders": 41,
  "totalRevenue": 2093,
  "totalCustomers": 9,
  "pendingOrders": 34,
  "confirmedOrders": 5,
  "processingOrders": 2,
  "shippedOrders": 0,
  "deliveredOrders": 0,
  "cancelledOrders": 0,
  "averageOrderValue": 51.048780487804876,
  "totalPreorders": 13,
  "totalComplaints": 5,
  "pendingComplaints": 5
}
```

---

### GET /api/Dashboard/revenue/daily

Gets daily revenue report.

Roles allowed: `manager`, `admin`

Query parameters:
- `startDate` (optional): Start date. Defaults to 30 days before end date.
- `endDate` (optional): End date. Defaults to today.

Validation:
- `startDate` must be before `endDate`

Response 200:
```json
[
  {
    "date": "2026-01-01T00:00:00",
    "revenue": 0,
    "orderCount": 0
  },
  {
    "date": "2026-01-02T00:00:00",
    "revenue": 150.0,
    "orderCount": 2
  }
]
```

---

### GET /api/Dashboard/revenue/monthly

Gets monthly revenue report for a year.

Roles allowed: `manager`, `admin`

Query parameter `year` (optional): Defaults to the current year.

Response 200:
```json
[
  {
    "date": "2026-01-01T00:00:00",
    "revenue": 0,
    "orderCount": 0
  },
  {
    "date": "2026-02-01T00:00:00",
    "revenue": 2093,
    "orderCount": 41
  }
]
```

---

### GET /api/Dashboard/popular-frames

Gets the most popular frames by sales volume.

Roles allowed: `manager`, `admin`

Query parameters:
- `limit` (optional, default: 10, max: 50): Number of frames to return
- `startDate` (optional): Start date filter
- `endDate` (optional): End date filter

Response 200:
```json
[
  {
    "frameId": "6800c9c1-6fc3-44f8-ac69-0c62fc3af979",
    "frameName": "Classic Metal",
    "brand": null,
    "basePrice": 120,
    "totalSold": 34,
    "totalRevenue": 4755
  }
]
```

---

### GET /api/Dashboard/orders/summary

Gets order summary for today, this week, and this month.

Roles allowed: `staff`, `manager`, `admin`

Response 200:
```json
{
  "todayOrders": 0,
  "weekOrders": 30,
  "monthOrders": 30,
  "todayRevenue": 0,
  "weekRevenue": 2093,
  "monthRevenue": 2093
}
```

---

## 19. SHIPPING

### GET /api/Shipping/methods

Gets all available shipping methods and their fees.

Roles allowed: Public (no authentication required)

Response 200:
```json
[
  {
    "method": "standard",
    "fee": 5.0,
    "description": "Standard Shipping (5-7 business days)"
  },
  {
    "method": "express",
    "fee": 15.0,
    "description": "Express Shipping (2-3 business days)"
  },
  {
    "method": "free",
    "fee": 0.0,
    "description": "Free Shipping"
  }
]
```

Note: Orders with a subtotal at or above the free shipping threshold (default $89) automatically receive free shipping regardless of the method chosen.

---

### POST /api/Shipping/calculate

Calculates the shipping fee for a given method and order subtotal.

Roles allowed: Public (no authentication required)

Request body:
```json
{
  "shippingMethod": "string",
  "orderSubtotal": 0.0
}
```

`shippingMethod`: `standard` or `express` or `free`

Response 200:
```json
{
  "shippingMethod": "standard",
  "orderSubtotal": 50.0,
  "shippingFee": 5.0,
  "total": 55.0
}
```

---

### PATCH /api/Shipping/orders/{orderId}/tracking

Assigns a tracking number and carrier to an order. Automatically changes status to `shipped` if the order is in `processing` status.

Roles allowed: `manager`

Path parameter `orderId`: GUID of the order.

Request body:
```json
{
  "trackingNumber": "string",
  "carrier": "string"
}
```

Validation:
- `trackingNumber` is required
- `carrier` is required

Response 200:
```json
{
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "trackingNumber": "VN123456789",
  "shippingCarrier": "VNPost",
  "shippedAt": "2026-02-24T10:09:01.823",
  "status": "shipped"
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

## ROLE PERMISSIONS SUMMARY

Public (no authentication):
- POST /api/Auth/login
- POST /api/Auth/register
- POST /api/Auth/google
- GET /api/Frames, GET /api/Frames/{id}, GET /api/Frames/{id}/media
- GET /api/FrameMedia/frame/{frameId}, GET /api/FrameMedia/{id}
- GET /api/Brands, GET /api/Brands/{id}
- GET /api/Materials, GET /api/Materials/{id}
- GET /api/Colors, GET /api/Colors/{id}
- GET /api/LensTypes, GET /api/LensTypes/{id}, GET /api/LensTypes/prescription-required, GET /api/LensTypes/no-prescription
- GET /api/LensFeatures, GET /api/LensFeatures/{id}, POST /api/LensFeatures/calculate-price
- GET /api/LensIndices, GET /api/LensIndices/{id}, GET /api/LensIndices/compatible
- GET /api/ProductReviews/frame/{frameId}, GET /api/ProductReviews/frame/{frameId}/summary
- GET /api/PreorderCampaigns/active, GET /api/PreorderCampaigns/{id}
- GET /api/Shipping/methods, POST /api/Shipping/calculate
- GET /api/Payments/vnpay-return, POST /api/Payments/vnpay-ipn

Any authenticated user:
- GET /api/Users/me, PUT /api/Users/me
- GET /api/Orders/{id} (customers limited to own orders)
- GET /api/Preorders/{id} (customers limited to own preorders)
- GET /api/Prescriptions/{id} (customers limited to own prescriptions)
- GET /api/Prescriptions/{id}/validate
- GET /api/Complaints/{id} (customers limited to own complaints)
- GET /api/Payments/{id}

customer:
- POST /api/Orders, GET /api/Orders/my
- POST /api/Preorders, GET /api/Preorders/my, DELETE /api/Preorders/{id}
- POST /api/Payments, GET /api/Payments/my
- POST /api/Prescriptions, GET /api/Prescriptions/my, GET /api/Prescriptions/my/valid, PUT /api/Prescriptions/{id}, DELETE /api/Prescriptions/{id}
- POST /api/Complaints, GET /api/Complaints/my, PUT /api/Complaints/{id}
- POST /api/ProductReviews, GET /api/ProductReviews/my-reviews, PUT /api/ProductReviews/{id}, DELETE /api/ProductReviews/{id}, GET /api/ProductReviews/verified-purchase/{frameId}

staff:
- GET /api/Orders, PUT /api/Orders/{id}/status
- GET /api/Preorders, PUT /api/Preorders/{id}/status, POST /api/Preorders/{id}/convert
- PUT /api/Payments/{id}/status, GET /api/Payments/order/{orderId}, GET /api/Payments/preorder/{preorderId}
- GET /api/Complaints, GET /api/Complaints/status/{status}, PUT /api/Complaints/{id}/status
- GET /api/Prescriptions/user/{userId}
- GET /api/Dashboard/orders/summary

manager:
- Everything `staff` can do, plus:
- POST /api/Frames, PUT /api/Frames/{id}, DELETE /api/Frames/{id}, GET /api/Frames/inventory/low-stock, GET /api/Frames/inventory/out-of-stock, PATCH /api/Frames/{id}/inventory
- All /api/FrameMedia write operations (POST, PUT, DELETE, upload endpoints)
- POST /api/Brands, PUT /api/Brands/{id}, DELETE /api/Brands/{id}
- POST /api/Materials, PUT /api/Materials/{id}, DELETE /api/Materials/{id}
- POST /api/Colors, PUT /api/Colors/{id}, DELETE /api/Colors/{id}
- POST /api/LensTypes, PUT /api/LensTypes/{id}, DELETE /api/LensTypes/{id}
- POST /api/LensFeatures, PUT /api/LensFeatures/{id}, DELETE /api/LensFeatures/{id}
- POST /api/LensIndices, PUT /api/LensIndices/{id}, DELETE /api/LensIndices/{id}
- DELETE /api/Orders/{id}
- GET /api/Dashboard/statistics, GET /api/Dashboard/revenue/daily, GET /api/Dashboard/revenue/monthly, GET /api/Dashboard/popular-frames
- GET /api/Users, GET /api/Users/search, GET /api/Users/role/{role}, GET /api/Users/status/{status}, GET /api/Users/{id}
- PATCH /api/ProductReviews/{id}/hide
- POST /api/PreorderCampaigns, PUT /api/PreorderCampaigns/{id}, PATCH /api/PreorderCampaigns/{id}/end
- PATCH /api/Shipping/orders/{orderId}/tracking

admin:
- Everything `manager` can do, plus:
- POST /api/Users, PUT /api/Users/{id}, PUT /api/Users/{id}/status, PUT /api/Users/{id}/role

---

## ERROR RESPONSE FORMAT

All error responses follow this structure:
```json
{
  "errorCode": "ERROR_CODE_STRING",
  "message": "Human-readable error message"
}
```

## PAGINATION RESPONSE FORMAT

All paginated endpoints return:
```json
{
  "totalItems": 0,
  "totalPages": 0,
  "currentPage": 1,
  "pageSize": 10,
  "items": []
}
```
