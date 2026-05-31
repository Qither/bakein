using Bakein.Api.Security;
using Npgsql;

namespace Bakein.Api.Infrastructure;

public sealed class DatabaseInitializer(NpgsqlDataSource dataSource, ILogger<DatabaseInitializer> logger)
{
    private static readonly Guid DemoAccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Initializing Bakein Postgres schema");

        await dataSource.ExecuteAsync(SchemaSql, cancellationToken: cancellationToken);
        await SeedCatalogAsync(cancellationToken);
        await SeedDemoAccountAsync(cancellationToken);

        logger.LogInformation("Bakein Postgres schema is ready");
    }

    private async Task SeedCatalogAsync(CancellationToken cancellationToken)
    {
        await dataSource.ExecuteAsync(
            """
            insert into categories (id, name, sort_order) values
              ('bread', '面包', 10),
              ('cake', '蛋糕', 20),
              ('cookies', '饼干', 30),
              ('dessert', '甜点', 40),
              ('piping', '裱花', 50)
            on conflict (id) do update set
              name = excluded.name,
              sort_order = excluded.sort_order;

            insert into courses (
              id, title, category_id, cover_text, duration_minutes, level, price_cents, member_free,
              teacher, rating, student_count, intro, sort_order, beginner_rank, popular_rank
            ) values
              ('soft-bread', '零失败软欧包', 'bread', '软欧包封面', 40, '新手', 3900, false,
               '王老师', 4.9, 1200, '从称量、醒发到割包，用最少工具做出稳定蓬松的软欧包。', 10, 1, 1),
              ('toast', '基础吐司课', 'bread', '吐司封面', 35, '新手', 2900, false,
               '林老师', 4.8, 860, '掌握吐司面团状态、一次发酵和二次发酵的关键判断。', 20, null, 3),
              ('chiffon', '戚风蛋糕不塌陷', 'cake', '戚风封面', 45, '新手', 0, true,
               '陈老师', 4.9, 2100, '用可观察的状态点解决戚风塌腰、开裂和回缩问题。', 30, 2, 2),
              ('cookies', '黄油曲奇入门', 'cookies', '曲奇封面', 20, '新手', 1900, false,
               '周老师', 4.7, 720, '适合第一次使用裱花袋的酥松曲奇课程。', 40, 3, null),
              ('tart', '葡式蛋挞', 'dessert', '蛋挞封面', 25, '新手', 0, true,
               '宋老师', 4.8, 940, '用成品挞皮快速完成稳定焦斑和嫩滑内馅。', 50, null, 4),
              ('piping', '奶油裱花基础', 'piping', '裱花封面', 60, '进阶', 4500, false,
               '许老师', 4.8, 680, '从直线、贝壳边到玫瑰花，建立稳定手感。', 60, null, null)
            on conflict (id) do update set
              title = excluded.title,
              category_id = excluded.category_id,
              cover_text = excluded.cover_text,
              duration_minutes = excluded.duration_minutes,
              level = excluded.level,
              price_cents = excluded.price_cents,
              member_free = excluded.member_free,
              teacher = excluded.teacher,
              rating = excluded.rating,
              student_count = excluded.student_count,
              intro = excluded.intro,
              sort_order = excluded.sort_order,
              beginner_rank = excluded.beginner_rank,
              popular_rank = excluded.popular_rank;

            delete from course_tags;
            insert into course_tags (course_id, tag, sort_order) values
              ('soft-bread', '新手友好', 1), ('soft-bread', '家用烤箱', 2), ('soft-bread', '免揉技巧', 3),
              ('toast', '揉面判断', 1), ('toast', '发酵观察', 2), ('toast', '早餐', 3),
              ('chiffon', '蛋白打发', 1), ('chiffon', '翻拌', 2), ('chiffon', '脱模', 3),
              ('cookies', '黄油软化', 1), ('cookies', '裱花袋', 2), ('cookies', '下午茶', 3),
              ('tart', '快手', 1), ('tart', '材料少', 2), ('tart', '亲子', 3),
              ('piping', '花嘴认识', 1), ('piping', '手法练习', 2), ('piping', '生日蛋糕', 3);

            insert into course_steps (id, course_id, title, description, duration_seconds, sort_order)
            select c.id || '-prep', c.id, '准备材料并称量', '面粉、酵母、水和盐先分区摆放，避免漏加。', 180, 10
            from courses c
            on conflict (id) do update set title = excluded.title, description = excluded.description, duration_seconds = excluded.duration_seconds, sort_order = excluded.sort_order;

            insert into course_steps (id, course_id, title, description, duration_seconds, sort_order)
            select c.id || '-mix', c.id, '混合到无干粉', '用刮刀压拌，盆底没有干粉后静置 10 分钟。', 360, 20
            from courses c
            on conflict (id) do update set title = excluded.title, description = excluded.description, duration_seconds = excluded.duration_seconds, sort_order = excluded.sort_order;

            insert into course_steps (id, course_id, title, description, duration_seconds, sort_order)
            select c.id || '-fold', c.id, '折叠建立筋度', '从四边向中心折叠，动作轻但要拉出张力。', 540, 30
            from courses c
            on conflict (id) do update set title = excluded.title, description = excluded.description, duration_seconds = excluded.duration_seconds, sort_order = excluded.sort_order;

            insert into course_steps (id, course_id, title, description, duration_seconds, sort_order)
            select c.id || '-bake', c.id, '预热并入炉烘烤', '观察上色后加盖锡纸，出炉敲底部有空响。', 1080, 40
            from courses c
            on conflict (id) do update set title = excluded.title, description = excluded.description, duration_seconds = excluded.duration_seconds, sort_order = excluded.sort_order;

            insert into material_kits (id, course_id, name, description, price_cents)
            select 'kit-' || id, id, title || ' 配套材料包', '已按新手用量分装，减少采购成本。', 4600
            from courses
            on conflict (id) do update set
              name = excluded.name,
              description = excluded.description,
              price_cents = excluded.price_cents;

            insert into membership_plans (id, name, price_cents, billing_period, description, sort_order) values
              ('monthly', '月卡', 2900, 'month', '适合先体验，每周更新新手课。', 10),
              ('season', '季卡', 7900, 'quarter', '包含会员课程与一次材料包券。', 20),
              ('yearly', '年卡', 19900, 'year', '全年课程、直播答疑和作品点评。', 30)
            on conflict (id) do update set
              name = excluded.name,
              price_cents = excluded.price_cents,
              billing_period = excluded.billing_period,
              description = excluded.description,
              sort_order = excluded.sort_order;
            """,
            cancellationToken: cancellationToken);
    }

    private async Task SeedDemoAccountAsync(CancellationToken cancellationToken)
    {
        var passwordHash = PasswordHasher.Hash("bakein123");
        await dataSource.ExecuteAsync(
            """
            insert into accounts (id, email, password_hash, display_name, avatar_text, role)
            values (@account_id, 'demo@bakein.local', @password_hash, '烘焙新手', '新手头像', 'learner')
            on conflict (id) do update set
              password_hash = excluded.password_hash,
              display_name = excluded.display_name,
              avatar_text = excluded.avatar_text;

            insert into user_profiles (account_id, learning_days, streak_days)
            values (@account_id, 5, 2)
            on conflict (account_id) do update set
              learning_days = excluded.learning_days,
              streak_days = excluded.streak_days;

            insert into memberships (id, account_id, plan_id, status, starts_at, ends_at)
            values ('22222222-2222-2222-2222-222222222222', @account_id, 'season', 'trialing', now() - interval '3 days', now() + interval '11 days')
            on conflict (id) do update set status = excluded.status, ends_at = excluded.ends_at;

            insert into course_reviews (id, course_id, account_id, author_name, content, rating)
            values
              ('33333333-3333-3333-3333-333333333331', 'soft-bread', @account_id, '小白学员', '小白也能听懂，步骤很清楚。', 4.9),
              ('33333333-3333-3333-3333-333333333332', 'soft-bread', @account_id, '材料控', '材料替代提示很实用。', 4.8)
            on conflict (id) do update set content = excluded.content, rating = excluded.rating;

            insert into community_posts (id, account_id, author_name, course_id, text, image_text, likes_count, comments_count, created_at)
            values
              ('44444444-4444-4444-4444-444444444441', @account_id, '小鹿', 'soft-bread', '第一次割包成功，外壳很脆，里面很软。', '作品照片', 128, 18, now() - interval '2 days'),
              ('44444444-4444-4444-4444-444444444442', @account_id, '阿南', 'chiffon', '按步骤冷却倒扣，这次终于没有塌腰。', '作品照片', 96, 12, now() - interval '1 day'),
              ('44444444-4444-4444-4444-444444444443', @account_id, 'Momo', 'cookies', '花纹保持得不错，下次试试少糖版本。', '作品照片', 73, 9, now())
            on conflict (id) do update set
              text = excluded.text,
              likes_count = excluded.likes_count,
              comments_count = excluded.comments_count;

            insert into cart_items (id, account_id, item_type, sku_id, name, unit_price_cents, quantity, selected)
            values
              ('55555555-5555-5555-5555-555555555551', @account_id, 'course', 'soft-bread', '零失败软欧包课程', 3900, 1, true),
              ('55555555-5555-5555-5555-555555555552', @account_id, 'material_kit', 'kit-soft-bread', '软欧包材料包', 4600, 1, true)
            on conflict (account_id, item_type, sku_id) do update set
              name = excluded.name,
              unit_price_cents = excluded.unit_price_cents,
              quantity = excluded.quantity,
              selected = excluded.selected,
              updated_at = now();
            """,
            [Pg.Param("account_id", DemoAccountId), Pg.Param("password_hash", passwordHash)],
            cancellationToken);
    }

    private const string SchemaSql =
        """
        create extension if not exists pgcrypto;

        create table if not exists accounts (
          id uuid primary key default gen_random_uuid(),
          email text not null unique,
          password_hash text not null,
          display_name text not null,
          avatar_text text,
          role text not null default 'learner',
          created_at timestamptz not null default now(),
          updated_at timestamptz not null default now()
        );

        create table if not exists user_profiles (
          account_id uuid primary key references accounts(id) on delete cascade,
          learning_days integer not null default 0,
          streak_days integer not null default 0,
          updated_at timestamptz not null default now()
        );

        create table if not exists user_sessions (
          id uuid primary key default gen_random_uuid(),
          account_id uuid not null references accounts(id) on delete cascade,
          token_hash text not null unique,
          expires_at timestamptz not null,
          created_at timestamptz not null default now(),
          revoked_at timestamptz
        );

        create index if not exists idx_user_sessions_account on user_sessions(account_id);
        create index if not exists idx_user_sessions_token_hash on user_sessions(token_hash);

        create table if not exists categories (
          id text primary key,
          name text not null unique,
          sort_order integer not null default 0
        );

        create table if not exists courses (
          id text primary key,
          title text not null,
          category_id text not null references categories(id),
          cover_text text not null,
          duration_minutes integer not null,
          level text not null,
          price_cents integer not null default 0,
          member_free boolean not null default false,
          teacher text not null,
          rating numeric(3,1) not null default 0,
          student_count integer not null default 0,
          intro text not null,
          sort_order integer not null default 0,
          beginner_rank integer,
          popular_rank integer,
          created_at timestamptz not null default now(),
          updated_at timestamptz not null default now()
        );

        create index if not exists idx_courses_category on courses(category_id);
        create index if not exists idx_courses_beginner on courses(beginner_rank) where beginner_rank is not null;
        create index if not exists idx_courses_popular on courses(popular_rank) where popular_rank is not null;

        create table if not exists course_tags (
          course_id text not null references courses(id) on delete cascade,
          tag text not null,
          sort_order integer not null default 0,
          primary key (course_id, tag)
        );

        create table if not exists course_steps (
          id text primary key,
          course_id text not null references courses(id) on delete cascade,
          title text not null,
          description text not null,
          duration_seconds integer not null,
          sort_order integer not null default 0
        );

        create index if not exists idx_course_steps_course on course_steps(course_id, sort_order);

        create table if not exists course_reviews (
          id uuid primary key default gen_random_uuid(),
          course_id text not null references courses(id) on delete cascade,
          account_id uuid references accounts(id) on delete set null,
          author_name text not null,
          content text not null,
          rating numeric(3,1) not null,
          created_at timestamptz not null default now()
        );

        create table if not exists material_kits (
          id text primary key,
          course_id text not null references courses(id) on delete cascade,
          name text not null,
          description text not null,
          price_cents integer not null default 0
        );

        create table if not exists membership_plans (
          id text primary key,
          name text not null,
          price_cents integer not null,
          billing_period text not null,
          description text not null,
          sort_order integer not null default 0
        );

        create table if not exists memberships (
          id uuid primary key default gen_random_uuid(),
          account_id uuid not null references accounts(id) on delete cascade,
          plan_id text not null references membership_plans(id),
          status text not null,
          starts_at timestamptz not null default now(),
          ends_at timestamptz not null
        );

        create index if not exists idx_memberships_account on memberships(account_id, ends_at desc);

        create table if not exists community_posts (
          id uuid primary key default gen_random_uuid(),
          account_id uuid references accounts(id) on delete set null,
          author_name text not null,
          course_id text references courses(id) on delete set null,
          text text not null,
          image_text text not null default '作品照片',
          likes_count integer not null default 0,
          comments_count integer not null default 0,
          created_at timestamptz not null default now()
        );

        create index if not exists idx_community_posts_created on community_posts(created_at desc);

        create table if not exists cart_items (
          id uuid primary key default gen_random_uuid(),
          account_id uuid not null references accounts(id) on delete cascade,
          item_type text not null,
          sku_id text not null,
          name text not null,
          unit_price_cents integer not null,
          quantity integer not null default 1,
          selected boolean not null default true,
          created_at timestamptz not null default now(),
          updated_at timestamptz not null default now(),
          unique (account_id, item_type, sku_id)
        );

        create index if not exists idx_cart_items_account on cart_items(account_id);

        create table if not exists orders (
          id uuid primary key default gen_random_uuid(),
          account_id uuid not null references accounts(id) on delete cascade,
          order_no text not null unique,
          status text not null,
          total_cents integer not null,
          created_at timestamptz not null default now()
        );

        create table if not exists order_items (
          id uuid primary key default gen_random_uuid(),
          order_id uuid not null references orders(id) on delete cascade,
          item_type text not null,
          sku_id text not null,
          name text not null,
          unit_price_cents integer not null,
          quantity integer not null
        );

        create index if not exists idx_orders_account on orders(account_id, created_at desc);
        create index if not exists idx_order_items_order on order_items(order_id);

        create table if not exists learning_progress (
          account_id uuid not null references accounts(id) on delete cascade,
          course_id text not null references courses(id) on delete cascade,
          step_id text not null references course_steps(id) on delete cascade,
          completed_at timestamptz not null default now(),
          primary key (account_id, course_id, step_id)
        );

        create index if not exists idx_learning_progress_account_course on learning_progress(account_id, course_id);
        """;
}
