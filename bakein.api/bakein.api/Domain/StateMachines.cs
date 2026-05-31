namespace Bakein.Api.Domain;

public static class CourseVersionState
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string Approved = "approved";
    public const string Published = "published";
    public const string Archived = "archived";

    public static bool CanTransition(string current, string next) =>
        (current, next) switch
        {
            (Draft, Submitted) => true,
            (Submitted, Approved) => true,
            (Approved, Published) => true,
            (Published, Archived) => true,
            (Approved, Archived) => true,
            _ => current == next,
        };
}

public static class MediaAssetState
{
    public const string Created = "created";
    public const string UploadPending = "upload_pending";
    public const string Uploaded = "uploaded";
    public const string Processing = "processing";
    public const string ReviewPending = "review_pending";
    public const string Approved = "approved";
    public const string Published = "published";
    public const string Blocked = "blocked";

    public static bool CanTransition(string current, string next) =>
        (current, next) switch
        {
            (Created, UploadPending) => true,
            (UploadPending, Uploaded) => true,
            (Uploaded, Processing) => true,
            (Processing, ReviewPending) => true,
            (ReviewPending, Approved) => true,
            (ReviewPending, Blocked) => true,
            (Approved, Published) => true,
            _ => current == next,
        };

    public static bool CanAdvanceTo(string current, string next)
    {
        if (current == next)
        {
            return true;
        }

        if (current is Published or Blocked)
        {
            return false;
        }

        if (next is Blocked)
        {
            return current is not (Approved or Published or Blocked);
        }

        return Rank(next) > Rank(current);
    }

    private static int Rank(string status) =>
        status switch
        {
            Created => 0,
            UploadPending => 1,
            Uploaded => 2,
            Processing => 3,
            ReviewPending => 4,
            Approved => 5,
            Published => 6,
            Blocked => 6,
            _ => -1,
        };
}

public static class PaymentIntentState
{
    public const string RequiresAction = "requires_action";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";

    public static bool CanApplyCallback(string current, string next) =>
        (current, next) switch
        {
            (RequiresAction, Succeeded) => true,
            (RequiresAction, Failed) => true,
            (RequiresAction, Cancelled) => true,
            _ => current == next,
        };
}

public static class OrderPaymentState
{
    public const string PendingPayment = "pending_payment";
    public const string Paid = "paid";
    public const string Fulfilled = "fulfilled";
    public const string Cancelled = "cancelled";

    public static bool CanTransition(string current, string next) =>
        (current, next) switch
        {
            (PendingPayment, Paid) => true,
            (Paid, Fulfilled) => true,
            (PendingPayment, Cancelled) => true,
            _ => current == next,
        };
}

public static class CommunityPostState
{
    public const string Draft = "draft";
    public const string MediaReviewPending = "media_review_pending";
    public const string ManualReviewPending = "manual_review_pending";
    public const string Published = "published";
    public const string Hidden = "hidden";
    public const string Deleted = "deleted";

    public static bool CanTransition(string current, string next) =>
        (current, next) switch
        {
            (Draft, MediaReviewPending) => true,
            (Draft, ManualReviewPending) => true,
            (Draft, Published) => true,
            (MediaReviewPending, ManualReviewPending) => true,
            (MediaReviewPending, Published) => true,
            (ManualReviewPending, Published) => true,
            (Published, Hidden) => true,
            (Hidden, Published) => true,
            (Published, Deleted) => true,
            (Hidden, Deleted) => true,
            _ => current == next,
        };
}
