namespace SpectraGlasses.WebAPI.Models
{
    public class ErrorResponse
    {
        public string ErrorCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Role { get; set; }
    }

    public class CreateFrameRequest
    {
        public string FrameName { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string? Color { get; set; }
        public string? Material { get; set; }
        public int? LensWidth { get; set; }
        public int? BridgeWidth { get; set; }
        public int? FrameWidth { get; set; }
        public int? TempleLength { get; set; }
        public string? Shape { get; set; }
        public string? Size { get; set; }
        public double? BasePrice { get; set; }
    }

    public class UpdateFrameRequest
    {
        public string? FrameName { get; set; }
        public string? Brand { get; set; }
        public string? Color { get; set; }
        public string? Material { get; set; }
        public int? LensWidth { get; set; }
        public int? BridgeWidth { get; set; }
        public int? FrameWidth { get; set; }
        public int? TempleLength { get; set; }
        public string? Shape { get; set; }
        public string? Size { get; set; }
        public double? BasePrice { get; set; }
        public string? Status { get; set; }
    }

    public class FrameValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
