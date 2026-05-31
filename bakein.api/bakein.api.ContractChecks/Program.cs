using Bakein.Api.Domain;
using Bakein.Api.Infrastructure.Postgres;

var repositoryRoot = FindRepositoryRoot();
var apiRoot = Path.Combine(repositoryRoot, "bakein.api", "bakein.api");

var checks = new List<ContractCheck>
{
    SourceContains("Program.cs", "\"/health\"", "health endpoint remains mapped"),
    SourceContains("Program.cs", "MapAuthEndpoints", "auth endpoint group remains composed"),
    SourceContains("Program.cs", "MapCatalogEndpoints", "catalog endpoint group remains composed"),
    SourceContains("Program.cs", "MapUserEndpoints", "user endpoint group remains composed"),

    SourceContains("Api/AuthEndpoints.cs", "MapPost(\"/register\"", "auth register route remains mapped"),
    SourceContains("Api/AuthEndpoints.cs", "MapPost(\"/login\"", "auth login route remains mapped"),
    SourceContains("Api/AuthEndpoints.cs", "MapGet(\"/me\"", "auth me route remains mapped"),
    SourceContains("Api/AuthEndpoints.cs", "MapPost(\"/logout\"", "auth logout route remains mapped"),

    SourceContains("Api/CatalogEndpoints.cs", "MapGet(\"/catalog/categories\"", "catalog categories route remains mapped"),
    SourceContains("Api/CatalogEndpoints.cs", "MapGet(\"/catalog/home\"", "catalog home route remains mapped"),
    SourceContains("Api/CatalogEndpoints.cs", "MapGet(\"/courses\"", "course list route remains mapped"),
    SourceContains("Api/CatalogEndpoints.cs", "MapGet(\"/courses/{id}\"", "course detail route remains mapped"),
    SourceContains("Api/CatalogEndpoints.cs", "MapGet(\"/courses/{id}/steps\"", "course steps route remains mapped"),
    SourceContains("Api/CatalogEndpoints.cs", "MapGet(\"/membership/plans\"", "membership plans route remains mapped"),
    SourceContains("Api/CatalogEndpoints.cs", "MapGet(\"/community/posts\"", "community post feed route remains mapped"),
    SourceContains("Api/CatalogEndpoints.cs", "MapPost(\"/community/posts\"", "community post create route remains mapped"),

    SourceContains("Api/UserEndpoints.cs", "MapGet(\"/profile\"", "profile route remains mapped"),
    SourceContains("Api/UserEndpoints.cs", "MapGet(\"/cart\"", "cart read route remains mapped"),
    SourceContains("Api/UserEndpoints.cs", "MapPut(\"/cart/items\"", "cart upsert route remains mapped"),
    SourceContains("Api/UserEndpoints.cs", "MapPatch(\"/cart/items/{id:guid}\"", "cart update route remains mapped"),
    SourceContains("Api/UserEndpoints.cs", "MapDelete(\"/cart/items/{id:guid}\"", "cart delete route remains mapped"),
    SourceContains("Api/UserEndpoints.cs", "MapPost(\"/cart/checkout\"", "checkout route remains mapped"),
    SourceContains("Api/UserEndpoints.cs", "MapGet(\"/orders\"", "orders route remains mapped"),
    SourceContains("Api/UserEndpoints.cs", "MapGet(\"/progress\"", "progress read route remains mapped"),
    SourceContains("Api/UserEndpoints.cs", "MapPut(\"/progress\"", "progress update route remains mapped"),

    SourceContains("Api/Dtos.cs", "record CourseCardDto", "course card DTO remains declared"),
    SourceContains("Api/Dtos.cs", "record CourseDetailDto", "course detail DTO remains declared"),
    SourceContains("Api/Dtos.cs", "record HomeFeedDto", "home feed DTO remains declared"),
    SourceContains("Api/Dtos.cs", "record MembershipPlanDto", "membership DTO remains declared"),
    SourceContains("Api/Dtos.cs", "record CommunityPostDto", "community post DTO remains declared"),
    SourceContains("Api/Dtos.cs", "record CartDto", "cart DTO remains declared"),
    SourceContains("Api/Dtos.cs", "record OrderDto", "order DTO remains declared"),
    SourceContains("Api/Dtos.cs", "record LearningProgressDto", "learning progress DTO remains declared"),

    SourceContains("Infrastructure/Postgres/PostgresMigrationRunner.cs", "schema_migrations", "migration ledger table remains defined"),
    SourceContains("Infrastructure/Postgres/PostgresMigrationRunner.cs", "checksum drift", "migration checksum drift remains a startup failure"),
    SourceContains("Infrastructure/Postgres/PostgresMigrationRunner.cs", "BeginTransactionAsync", "migration application remains transactional"),
    SourceContains("Infrastructure/DatabaseInitializer.cs", "001_baseline_mvp", "baseline MVP migration remains registered"),
    SourceContains("Infrastructure/Providers/ProviderRegistration.cs", "Unsupported Payment:Provider", "unsupported payment provider fails startup"),
    SourceContains("Infrastructure/Providers/ProviderRegistration.cs", "Tencent VOD provider selected without required configuration", "Tencent VOD mode requires explicit configuration"),
    SourceContains("Api/OperationsEndpoints.cs", "ProviderRuntimeMode", "provider diagnostics use registered runtime mode"),

    SourceContains("Infrastructure/Postgres/ProductionCoreMigrations.cs", "002_identity_roles_audit", "identity migration remains registered"),
    SourceContains("Infrastructure/Postgres/ProductionCoreMigrations.cs", "008_operations_outbox", "operations outbox migration remains registered"),
    SourceContains("Infrastructure/Postgres/ProductionCoreMigrations.cs", "uq_provider_callback_logs_event", "provider callback uniqueness remains enforced"),
    SourceContains("Infrastructure/Postgres/ProductionCoreMigrations.cs", "uq_payment_events_provider_event", "payment event uniqueness remains enforced"),
    SourceContains("Infrastructure/Postgres/ProductionCoreMigrations.cs", "uq_course_versions_one_published", "published course version uniqueness remains enforced"),
    SourceContains("Infrastructure/Postgres/ProductionCoreMigrations.cs", "primary key (account_id, target_type, target_id)", "community like uniqueness remains enforced"),
    SourceContains("Infrastructure/Postgres/ProductionCoreMigrations.cs", "unique (account_id, course_id, source_type, source_id)", "course entitlement uniqueness remains enforced"),

    SourceContains("Api/MediaEndpoints.cs", "MapPost(\"/upload-intents\"", "media upload intent route remains mapped"),
    SourceContains("Api/MediaEndpoints.cs", "MapPost(\"/callbacks/local\"", "local media callback route remains mapped"),
    SourceContains("Api/CommerceEndpoints.cs", "MapPost(\"/intents\"", "payment intent route remains mapped"),
    SourceContains("Api/CommerceEndpoints.cs", "MapPost(\"/callbacks/local\"", "local payment callback route remains mapped"),
    SourceContains("Api/CommunityInteractionEndpoints.cs", "MapPost(\"/check-ins\"", "community check-in route remains mapped"),
    SourceContains("Api/CommunityInteractionEndpoints.cs", "MapPost(\"/posts/{postId:guid}/comments\"", "community comment route remains mapped"),
    SourceContains("Api/CommunityInteractionEndpoints.cs", "MapPost(\"/posts/{postId:guid}/likes\"", "community like route remains mapped"),
    SourceContains("Api/CommunityInteractionEndpoints.cs", "MapPost(\"/posts/{postId:guid}/reports\"", "community report route remains mapped"),
    SourceContains("Api/CatalogEndpoints.cs", "where p.status = 'published'", "community feed filters unpublished posts"),
    SourceContains("Api/CommunityInteractionEndpoints.cs", "PublishedPostExistsAsync", "community interactions require published posts"),
    SourceContains("Api/ProfileEndpoints.cs", "MapGet(\"/addresses\"", "profile address read route remains mapped"),
    SourceContains("Api/ProfileEndpoints.cs", "MapPost(\"/addresses\"", "profile address create route remains mapped"),
    SourceContains("Api/AdminEndpoints.cs", "MapPost(\"/courses/versions\"", "admin course version create route remains mapped"),
    SourceContains("Api/AdminEndpoints.cs", "MapPost(\"/courses/versions/{id:guid}/steps\"", "admin course version step route remains mapped"),
    SourceContains("Api/AdminEndpoints.cs", "MapPost(\"/courses/versions/{id:guid}/publish\"", "admin course publish route remains mapped"),
    SourceContains("Api/AdminEndpoints.cs", "title = cv.title", "course publish syncs app-facing course title"),
    SourceContains("Api/CatalogEndpoints.cs", "coalesce(cv.title, c.title)", "catalog cards read published version metadata"),
    SourceContains("Api/CatalogEndpoints.cs", "course_version_steps", "course steps read published version steps"),
    SourceContains("Api/UserEndpoints.cs", "visible_steps", "learning progress uses app-visible steps"),
    SourceContains("Infrastructure/Postgres/ProductionCoreMigrations.cs", "009_learning_progress_version_steps", "learning progress migration supports version-only steps"),
    SourceContains("Api/AdminEndpoints.cs", "MapGet(\"/moderation/tasks\"", "admin moderation route remains mapped"),
    SourceContains("Api/AdminEndpoints.cs", "MapGet(\"/audit-logs\"", "admin audit route remains mapped"),
    SourceContains("Api/OperationsEndpoints.cs", "MapGet(\"/readiness\"", "operations readiness route remains mapped"),
    SourceContains("Application/Providers/MediaProviders.cs", "interface IMediaProvider", "media provider port remains declared"),
    SourceContains("Application/Providers/PaymentProviders.cs", "interface IPaymentProvider", "payment provider port remains declared"),
    SourceContains("Infrastructure/Providers/Local/LocalMediaProvider.cs", "class LocalMediaProvider", "local media adapter remains declared"),
    SourceContains("Infrastructure/Providers/Local/LocalPaymentProvider.cs", "class LocalPaymentProvider", "local payment adapter remains declared"),
    SourceContains("Infrastructure/Providers/TencentVod/TencentVodOptions.cs", "TencentVod", "Tencent VOD options remain configuration-bound"),

    Check(CourseVersionState.CanTransition(CourseVersionState.Draft, CourseVersionState.Submitted), "course version draft can submit"),
    Check(!CourseVersionState.CanTransition(CourseVersionState.Draft, CourseVersionState.Published), "course version draft cannot publish directly"),
    Check(MediaAssetState.CanTransition(MediaAssetState.ReviewPending, MediaAssetState.Approved), "media review can approve asset"),
    Check(!MediaAssetState.CanTransition(MediaAssetState.UploadPending, MediaAssetState.Published), "media cannot publish before review"),
    Check(MediaAssetState.CanAdvanceTo(MediaAssetState.UploadPending, MediaAssetState.Approved), "media callback can advance local upload to approved"),
    Check(!MediaAssetState.CanAdvanceTo(MediaAssetState.Approved, MediaAssetState.ReviewPending), "media callback cannot move approved asset backward"),
    Check(OrderPaymentState.CanTransition(OrderPaymentState.PendingPayment, OrderPaymentState.Paid), "order can transition from pending payment to paid"),
    Check(!OrderPaymentState.CanTransition(OrderPaymentState.Cancelled, OrderPaymentState.Paid), "cancelled order cannot become paid"),
    Check(PaymentIntentState.CanApplyCallback(PaymentIntentState.RequiresAction, PaymentIntentState.Succeeded), "payment callback can mark intent succeeded"),
    Check(!PaymentIntentState.CanApplyCallback(PaymentIntentState.Succeeded, PaymentIntentState.Failed), "payment callback cannot move succeeded intent backward"),
    Check(CommunityPostState.CanTransition(CommunityPostState.MediaReviewPending, CommunityPostState.Published), "community media-reviewed post can publish"),
    Check(!CommunityPostState.CanTransition(CommunityPostState.Deleted, CommunityPostState.Published), "deleted community post cannot republish"),
    Check(ProductionCoreMigrations.All.Select(migration => migration.Id).SequenceEqual([
        "002_identity_roles_audit",
        "003_catalog_cms_versions",
        "004_media_vod_moderation",
        "005_commerce_payment_idempotency",
        "006_community_comments_reports",
        "007_learning_entitlements_stats",
        "008_operations_outbox",
        "009_learning_progress_version_steps",
    ]), "production core migrations remain ordered"),
};

var failures = checks.Where(check => !check.Passed).ToList();
foreach (var check in checks)
{
    Console.WriteLine($"{(check.Passed ? "PASS" : "FAIL")} {check.Description}");
}

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Contract checks failed: {failures.Count}");
    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"Contract checks passed: {checks.Count}");

ContractCheck SourceContains(string relativePath, string expected, string description)
{
    var path = Path.Combine(apiRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    var source = File.Exists(path) ? File.ReadAllText(path) : "";
    return new ContractCheck(description, source.Contains(expected, StringComparison.Ordinal));
}

ContractCheck Check(bool passed, string description) => new(description, passed);

string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, "bakein.api")) &&
            Directory.Exists(Path.Combine(directory.FullName, "bakein.app")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return Directory.GetCurrentDirectory();
}

internal sealed record ContractCheck(string Description, bool Passed);
