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
