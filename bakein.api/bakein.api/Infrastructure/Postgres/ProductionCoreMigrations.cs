namespace Bakein.Api.Infrastructure.Postgres;

public static class ProductionCoreMigrations
{
    public static IReadOnlyList<PostgresMigration> All { get; } =
    [
        new("002_identity_roles_audit", IdentityRolesAuditSql),
        new("003_catalog_cms_versions", CatalogCmsVersionsSql),
        new("004_media_vod_moderation", MediaVodModerationSql),
        new("005_commerce_payment_idempotency", CommercePaymentIdempotencySql),
        new("006_community_comments_reports", CommunityCommentsReportsSql),
        new("007_learning_entitlements_stats", LearningEntitlementsStatsSql),
        new("008_operations_outbox", OperationsOutboxSql),
        new("009_learning_progress_version_steps", LearningProgressVersionStepsSql),
        new("010_account_external_identities", AccountExternalIdentitiesSql),
    ];

    private const string IdentityRolesAuditSql =
        """
        create table if not exists account_roles (
          account_id uuid not null references accounts(id) on delete cascade,
          role text not null,
          granted_at timestamptz not null default now(),
          primary key (account_id, role)
        );

        insert into account_roles (account_id, role)
        select id, role
        from accounts
        on conflict do nothing;

        create table if not exists profile_addresses (
          id uuid primary key default gen_random_uuid(),
          account_id uuid not null references accounts(id) on delete cascade,
          contact_name text not null,
          phone text not null,
          province text not null,
          city text not null,
          district text not null,
          detail text not null,
          is_default boolean not null default false,
          created_at timestamptz not null default now(),
          updated_at timestamptz not null default now()
        );

        create index if not exists idx_profile_addresses_account
          on profile_addresses(account_id, created_at desc);

        create table if not exists audit_logs (
          id uuid primary key default gen_random_uuid(),
          actor_account_id uuid references accounts(id) on delete set null,
          action text not null,
          target_type text,
          target_id text,
          metadata jsonb not null default '{}'::jsonb,
          created_at timestamptz not null default now()
        );

        create index if not exists idx_audit_logs_target
          on audit_logs(target_type, target_id, created_at desc);
        create index if not exists idx_audit_logs_actor
          on audit_logs(actor_account_id, created_at desc);
        """;

    private const string CatalogCmsVersionsSql =
        """
        create table if not exists course_versions (
          id uuid primary key default gen_random_uuid(),
          course_id text not null references courses(id) on delete cascade,
          version_no integer not null,
          status text not null default 'draft',
          title text not null,
          intro text not null,
          cover_text text not null,
          teacher text not null,
          submitted_at timestamptz,
          approved_at timestamptz,
          published_at timestamptz,
          archived_at timestamptz,
          created_by uuid references accounts(id) on delete set null,
          created_at timestamptz not null default now(),
          updated_at timestamptz not null default now(),
          unique (course_id, version_no)
        );

        create unique index if not exists uq_course_versions_one_published
          on course_versions(course_id)
          where status = 'published';

        alter table courses
          add column if not exists published_version_id uuid;

        do $$
        begin
          alter table courses
            add constraint fk_courses_published_version
            foreign key (published_version_id) references course_versions(id);
        exception
          when duplicate_object then null;
        end $$;

        create table if not exists course_version_sections (
          id uuid primary key default gen_random_uuid(),
          version_id uuid not null references course_versions(id) on delete cascade,
          title text not null,
          sort_order integer not null default 0
        );

        create table if not exists course_version_steps (
          id uuid primary key default gen_random_uuid(),
          version_id uuid not null references course_versions(id) on delete cascade,
          section_id uuid references course_version_sections(id) on delete set null,
          source_step_id text references course_steps(id) on delete set null,
          title text not null,
          description text not null,
          media_asset_id uuid,
          duration_seconds integer not null default 0,
          sort_order integer not null default 0
        );

        create index if not exists idx_course_version_steps_version
          on course_version_steps(version_id, sort_order);

        create table if not exists banner_slots (
          id text primary key,
          course_id text references courses(id) on delete set null,
          title text not null,
          image_text text not null,
          sort_order integer not null default 0,
          starts_at timestamptz,
          ends_at timestamptz,
          created_at timestamptz not null default now()
        );
        """;

    private const string MediaVodModerationSql =
        """
        create table if not exists media_assets (
          id uuid primary key default gen_random_uuid(),
          owner_account_id uuid references accounts(id) on delete set null,
          provider text not null default 'local',
          provider_file_id text,
          media_type text not null,
          file_name text not null,
          content_type text not null,
          status text not null default 'created',
          playback_url text,
          review_suggestion text,
          created_at timestamptz not null default now(),
          updated_at timestamptz not null default now()
        );

        create index if not exists idx_media_assets_owner
          on media_assets(owner_account_id, created_at desc);
        create index if not exists idx_media_assets_provider_file
          on media_assets(provider, provider_file_id);

        create table if not exists media_upload_intents (
          id uuid primary key default gen_random_uuid(),
          media_asset_id uuid not null references media_assets(id) on delete cascade,
          provider text not null,
          provider_file_id text not null,
          upload_url text not null,
          expires_at timestamptz not null,
          created_at timestamptz not null default now()
        );

        create table if not exists media_review_results (
          id uuid primary key default gen_random_uuid(),
          media_asset_id uuid not null references media_assets(id) on delete cascade,
          provider text not null,
          suggestion text not null,
          raw_payload jsonb not null default '{}'::jsonb,
          created_at timestamptz not null default now()
        );

        create table if not exists media_provider_events (
          id uuid primary key default gen_random_uuid(),
          provider text not null,
          event_type text not null,
          provider_file_id text,
          provider_event_id text,
          event_hash text not null,
          payload jsonb not null default '{}'::jsonb,
          received_at timestamptz not null default now(),
          processed_at timestamptz
        );

        create unique index if not exists uq_media_provider_events_provider_event
          on media_provider_events(provider, event_type, provider_file_id, provider_event_id)
          where provider_event_id is not null;
        create unique index if not exists uq_media_provider_events_hash
          on media_provider_events(provider, event_hash);

        create table if not exists moderation_tasks (
          id uuid primary key default gen_random_uuid(),
          target_type text not null,
          target_id text not null,
          status text not null default 'open',
          reason text not null,
          assigned_to uuid references accounts(id) on delete set null,
          resolved_by uuid references accounts(id) on delete set null,
          resolution text,
          created_at timestamptz not null default now(),
          resolved_at timestamptz
        );

        create index if not exists idx_moderation_tasks_status
          on moderation_tasks(status, created_at);
        """;

    private const string CommercePaymentIdempotencySql =
        """
        create table if not exists idempotency_keys (
          key text not null,
          scope text not null,
          request_hash text not null,
          response_json jsonb,
          created_at timestamptz not null default now(),
          primary key (key, scope)
        );

        create table if not exists payment_intents (
          id uuid primary key default gen_random_uuid(),
          order_id uuid not null references orders(id) on delete cascade,
          account_id uuid not null references accounts(id) on delete cascade,
          provider text not null,
          provider_intent_id text not null,
          amount_cents integer not null,
          currency text not null default 'CNY',
          status text not null default 'requires_action',
          client_secret text,
          expires_at timestamptz not null,
          created_at timestamptz not null default now(),
          updated_at timestamptz not null default now(),
          unique (provider, provider_intent_id)
        );

        create index if not exists idx_payment_intents_order
          on payment_intents(order_id);

        create table if not exists payment_events (
          id uuid primary key default gen_random_uuid(),
          payment_intent_id uuid references payment_intents(id) on delete set null,
          provider text not null,
          provider_event_id text,
          event_type text not null,
          payload jsonb not null default '{}'::jsonb,
          created_at timestamptz not null default now()
        );

        create unique index if not exists uq_payment_events_provider_event
          on payment_events(provider, provider_event_id)
          where provider_event_id is not null;

        create table if not exists refunds (
          id uuid primary key default gen_random_uuid(),
          order_id uuid not null references orders(id) on delete cascade,
          provider text not null,
          provider_refund_id text,
          amount_cents integer not null,
          status text not null default 'requested',
          reason text,
          created_at timestamptz not null default now()
        );

        create table if not exists provider_callback_logs (
          id uuid primary key default gen_random_uuid(),
          provider text not null,
          event_type text not null,
          provider_event_id text,
          event_hash text not null,
          signature_status text not null default 'not_required',
          payload jsonb not null default '{}'::jsonb,
          processing_outcome text not null default 'received',
          received_at timestamptz not null default now(),
          processed_at timestamptz
        );

        create unique index if not exists uq_provider_callback_logs_event
          on provider_callback_logs(provider, event_type, provider_event_id)
          where provider_event_id is not null;
        create unique index if not exists uq_provider_callback_logs_hash
          on provider_callback_logs(provider, event_hash);
        """;

    private const string CommunityCommentsReportsSql =
        """
        alter table community_posts
          add column if not exists post_type text not null default 'work_showcase',
          add column if not exists status text not null default 'published',
          add column if not exists step_id text,
          add column if not exists media_asset_id uuid;

        do $$
        begin
          alter table community_posts
            add constraint fk_community_posts_step
            foreign key (step_id) references course_steps(id) on delete set null;
        exception
          when duplicate_object then null;
        end $$;

        do $$
        begin
          alter table community_posts
            add constraint fk_community_posts_media_asset
            foreign key (media_asset_id) references media_assets(id) on delete set null;
        exception
          when duplicate_object then null;
        end $$;

        create index if not exists idx_community_posts_status_created
          on community_posts(status, created_at desc);
        create index if not exists idx_community_posts_type
          on community_posts(post_type, created_at desc);

        create table if not exists community_comments (
          id uuid primary key default gen_random_uuid(),
          post_id uuid not null references community_posts(id) on delete cascade,
          account_id uuid not null references accounts(id) on delete cascade,
          author_name text not null,
          text text not null,
          status text not null default 'published',
          created_at timestamptz not null default now()
        );

        create index if not exists idx_community_comments_post
          on community_comments(post_id, created_at);

        create table if not exists community_likes (
          account_id uuid not null references accounts(id) on delete cascade,
          target_type text not null,
          target_id uuid not null,
          created_at timestamptz not null default now(),
          primary key (account_id, target_type, target_id)
        );

        create table if not exists community_reports (
          id uuid primary key default gen_random_uuid(),
          target_type text not null,
          target_id uuid not null,
          account_id uuid not null references accounts(id) on delete cascade,
          reason text not null,
          status text not null default 'open',
          created_at timestamptz not null default now(),
          resolved_at timestamptz
        );

        create index if not exists idx_community_reports_status
          on community_reports(status, created_at);
        """;

    private const string LearningEntitlementsStatsSql =
        """
        create table if not exists course_entitlements (
          id uuid primary key default gen_random_uuid(),
          account_id uuid not null references accounts(id) on delete cascade,
          course_id text not null references courses(id) on delete cascade,
          source_type text not null,
          source_id text not null,
          starts_at timestamptz not null default now(),
          ends_at timestamptz,
          created_at timestamptz not null default now(),
          unique (account_id, course_id, source_type, source_id)
        );

        create index if not exists idx_course_entitlements_account
          on course_entitlements(account_id, course_id);

        create table if not exists learning_questions (
          id uuid primary key default gen_random_uuid(),
          account_id uuid not null references accounts(id) on delete cascade,
          course_id text not null references courses(id) on delete cascade,
          step_id text references course_steps(id) on delete set null,
          question text not null,
          answer text,
          status text not null default 'open',
          created_at timestamptz not null default now(),
          answered_at timestamptz
        );

        create table if not exists learning_stats (
          account_id uuid primary key references accounts(id) on delete cascade,
          completed_steps integer not null default 0,
          completed_courses integer not null default 0,
          check_in_count integer not null default 0,
          updated_at timestamptz not null default now()
        );
        """;

    private const string OperationsOutboxSql =
        """
        create table if not exists outbox_events (
          id uuid primary key default gen_random_uuid(),
          aggregate_type text not null,
          aggregate_id text not null,
          event_type text not null,
          payload jsonb not null default '{}'::jsonb,
          created_at timestamptz not null default now(),
          processed_at timestamptz,
          failed_at timestamptz,
          failure_reason text
        );

        create index if not exists idx_outbox_events_pending
          on outbox_events(created_at)
          where processed_at is null and failed_at is null;
        """;

    private const string LearningProgressVersionStepsSql =
        """
        alter table learning_progress
          drop constraint if exists learning_progress_step_id_fkey;
        """;

    private const string AccountExternalIdentitiesSql =
        """
        create table if not exists account_external_identities (
          provider text not null,
          provider_subject text not null,
          account_id uuid not null references accounts(id) on delete cascade,
          union_subject text,
          display_name text not null,
          avatar_url text,
          raw_profile jsonb not null default '{}'::jsonb,
          created_at timestamptz not null default now(),
          updated_at timestamptz not null default now(),
          primary key (provider, provider_subject)
        );

        create index if not exists idx_account_external_identities_account
          on account_external_identities(account_id, provider);

        create unique index if not exists uq_account_external_identities_provider_union
          on account_external_identities(provider, union_subject)
          where union_subject is not null;
        """;
}
