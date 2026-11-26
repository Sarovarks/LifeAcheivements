using FluentValidation;
using LifeAchievements.Application.Abstractions;
using LifeAchievements.Application.Achievements.Commands;
using LifeAchievements.Application.Achievements.Queries;
using LifeAchievements.Application.Achievements;
using LifeAchievements.Application.Common;
using LifeAchievements.Application.Friends.Commands;
using LifeAchievements.Application.Friends.Queries;
using LifeAchievements.Application.Events;
using LifeAchievements.Application.Users.Queries;
using LifeAchievements.Domain.Events;
using LifeAchievements.Shared.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace LifeAchievements.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<IClock, SystemClock>();

        services.AddValidatorsFromAssemblyContaining<ClaimAchievementCommandValidator>();

        // Commands
        services.AddScoped<ICommandHandler<ClaimAchievementCommand, UnlockResultDto>, ClaimAchievementCommandHandler>();
        services.AddScoped<ICommandHandler<LogProgressCommand, LogProgressResultDto>, LogProgressCommandHandler>();
        services.AddScoped<ICommandHandler<SetUnlockNoteCommand, bool>, SetUnlockNoteCommandHandler>();

        // Phase 2 - social
        services.AddScoped<IUnlockService, UnlockService>();
        services.AddScoped<ICommandHandler<SendFriendRequestCommand, Guid>, SendFriendRequestCommandHandler>();
        services.AddScoped<ICommandHandler<RespondToFriendRequestCommand, bool>, RespondToFriendRequestCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveFriendCommand, bool>, RemoveFriendCommandHandler>();
        services.AddScoped<ICommandHandler<VerifyClaimCommand, VerifyClaimResultDto>, VerifyClaimCommandHandler>();
        services.AddScoped<ICommandHandler<ToggleReactionCommand, ReactionResultDto>, ToggleReactionCommandHandler>();

        // Queries
        services.AddScoped<IQueryHandler<GetMeQuery, MeDto>, GetMeQueryHandler>();
        services.AddScoped<IQueryHandler<GetAchievementsQuery, IReadOnlyList<AchievementDto>>, GetAchievementsQueryHandler>();
        services.AddScoped<IQueryHandler<GetRecentUnlocksQuery, IReadOnlyList<RecentUnlockDto>>, GetRecentUnlocksQueryHandler>();
        services.AddScoped<IQueryHandler<GetFriendsQuery, FriendsOverviewDto>, GetFriendsQueryHandler>();
        services.AddScoped<IQueryHandler<GetFeedQuery, IReadOnlyList<FeedItemDto>>, GetFeedQueryHandler>();
        services.AddScoped<IQueryHandler<GetPendingVerificationsQuery, IReadOnlyList<PendingVerificationDto>>, GetPendingVerificationsQueryHandler>();
        services.AddScoped<IQueryHandler<GetWeeklyLeaderboardQuery, WeeklyLeaderboardDto>, GetWeeklyLeaderboardQueryHandler>();

        // Domain event handlers
        services.AddScoped<IDomainEventHandler<AchievementUnlocked>, HiddenAchievementRulesHandler>();

        return services;
    }
}
