namespace Bakein.Api.Api;

public sealed record ApiError(string Code, string Message);

public sealed record RegisterRequest(string Email, string Password, string DisplayName);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(string Token, DateTimeOffset ExpiresAt, AccountDto Account);

public sealed record AccountDto(Guid Id, string Email, string DisplayName, string Role, DateTimeOffset CreatedAt);

public sealed record CategoryDto(string Id, string Name, int SortOrder);

public sealed record CourseCardDto(
    string Id,
    string Title,
    string Category,
    string Cover,
    int DurationMinutes,
    string Duration,
    string Level,
    int PriceCents,
    string Price,
    bool MemberFree,
    string Teacher,
    decimal Rating,
    string RatingLabel,
    int StudentCount,
    string Students,
    IReadOnlyList<string> Tags,
    string Intro);

public sealed record CourseStepDto(string Id, string Title, string Description, int DurationSeconds, string Time, int SortOrder);

public sealed record CourseReviewDto(Guid Id, string Author, string Content, decimal Rating, DateTimeOffset CreatedAt);

public sealed record MaterialKitDto(string Id, string CourseId, string Name, string Description, int PriceCents, string Price);

public sealed record CourseDetailDto(
    CourseCardDto Course,
    IReadOnlyList<CourseStepDto> Steps,
    IReadOnlyList<CourseReviewDto> Reviews,
    MaterialKitDto? MaterialKit);

public sealed record HomeFeedDto(
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<CourseCardDto> BeginnerCourses,
    IReadOnlyList<CourseCardDto> PopularCourses);

public sealed record MembershipPlanDto(string Id, string Name, int PriceCents, string Price, string BillingPeriod, string Description, int SortOrder);

public sealed record CommunityPostDto(
    Guid Id,
    string Author,
    string? CourseId,
    string? CourseTitle,
    string Text,
    string ImageText,
    int Likes,
    int Comments,
    DateTimeOffset CreatedAt);

public sealed record CreateCommunityPostRequest(string? CourseId, string Text, string? ImageText);

public sealed record ProfileDto(
    AccountDto Account,
    string MembershipStatus,
    int LearningDays,
    int StreakDays,
    int PurchasedCourses,
    int CompletedSteps,
    int CheckInCount);

public sealed record CartItemDto(
    Guid Id,
    string ItemType,
    string SkuId,
    string Name,
    int UnitPriceCents,
    string UnitPrice,
    int Quantity,
    bool Selected,
    int LineTotalCents,
    string LineTotal);

public sealed record CartDto(IReadOnlyList<CartItemDto> Items, int TotalCents, string Total);

public sealed record UpsertCartItemRequest(string ItemType, string SkuId, int Quantity = 1, bool Selected = true);

public sealed record UpdateCartItemRequest(int? Quantity, bool? Selected);

public sealed record OrderItemDto(Guid Id, string ItemType, string SkuId, string Name, int UnitPriceCents, int Quantity, int LineTotalCents);

public sealed record OrderDto(Guid Id, string OrderNo, string Status, int TotalCents, string Total, DateTimeOffset CreatedAt, IReadOnlyList<OrderItemDto> Items);

public sealed record LearningProgressDto(string CourseId, IReadOnlyList<string> CompletedStepIds, int CompletedSteps, int TotalSteps);

public sealed record ProgressUpdateRequest(string CourseId, string StepId, bool Completed = true);

public sealed record MediaUploadIntentRequest(string FileName, string ContentType, string MediaType = "image");

public sealed record MediaUploadIntentDto(
    Guid MediaAssetId,
    Guid UploadIntentId,
    string Provider,
    string ProviderFileId,
    string UploadUrl,
    string? PlaybackUrl,
    DateTimeOffset ExpiresAt,
    string Status);

public sealed record LocalMediaCallbackRequest(
    Guid MediaAssetId,
    string EventType = "upload_completed",
    string Suggestion = "pass",
    string? ProviderEventId = null,
    string? ProviderFileId = null);

public sealed record MediaCallbackResultDto(Guid MediaAssetId, string Status, string Suggestion, bool Duplicate);

public sealed record PaymentIntentRequest(Guid OrderId);

public sealed record PaymentIntentDto(
    Guid Id,
    Guid OrderId,
    string Provider,
    string ProviderIntentId,
    int AmountCents,
    string Currency,
    string Status,
    string ClientSecret,
    DateTimeOffset ExpiresAt);

public sealed record LocalPaymentCallbackRequest(
    Guid PaymentIntentId,
    string Status = "succeeded",
    string? ProviderEventId = null);

public sealed record PaymentCallbackResultDto(Guid PaymentIntentId, Guid OrderId, string PaymentStatus, string OrderStatus, bool Duplicate);

public sealed record CommunityCheckInRequest(string? CourseId, string? StepId, Guid? MediaAssetId, string Text);

public sealed record CommunityCommentRequest(string Text);

public sealed record CommunityCommentDto(Guid Id, Guid PostId, string Author, string Text, DateTimeOffset CreatedAt);

public sealed record CommunityReportRequest(string Reason);

public sealed record ProfileAddressRequest(
    string ContactName,
    string Phone,
    string Province,
    string City,
    string District,
    string Detail,
    bool IsDefault = false);

public sealed record ProfileAddressDto(
    Guid Id,
    string ContactName,
    string Phone,
    string Province,
    string City,
    string District,
    string Detail,
    bool IsDefault);

public sealed record AdminCourseVersionRequest(string CourseId, string Title, string Intro, string CoverText, string Teacher);

public sealed record AdminCourseVersionDto(Guid Id, string CourseId, int VersionNo, string Status, string Title, DateTimeOffset CreatedAt);

public sealed record AdminCourseVersionStepRequest(
    string Title,
    string Description,
    int DurationSeconds = 0,
    int SortOrder = 0,
    string? SourceStepId = null,
    Guid? SectionId = null,
    Guid? MediaAssetId = null);

public sealed record AdminCourseVersionStepDto(
    Guid Id,
    Guid VersionId,
    string? SourceStepId,
    string Title,
    string Description,
    int DurationSeconds,
    int SortOrder);

public sealed record AdminModerationDecisionRequest(string Resolution);
