using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.AspNetCore.Identity;
using MCBusiness.Models;

namespace MCBusiness.Services
{
    public interface ITwoFactorSendThrottle
    {
        Task<TwoFactorSendStatus> GetStatusAsync(ApplicationUser user);
        Task<TwoFactorSendStatus> TryAcquireAsync(ApplicationUser user);
        Task ClearAsync(ApplicationUser user);
    }

    public sealed record TwoFactorSendStatus(
        bool IsAllowed,
        int RetryAfterSeconds,
        int RemainingSends,
        bool WasAcquired = false);

    public class TwoFactorSendThrottle : ITwoFactorSendThrottle
    {
        private const string Provider = "MCBusiness";
        private const string WindowStartToken = "TWO_FACTOR_SEND_WINDOW_START";
        private const string SendCountToken = "TWO_FACTOR_SEND_COUNT";
        private const string NextSendToken = "TWO_FACTOR_NEXT_SEND";
        private const int MaxSendsPerWindow = 5;
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30);
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> UserLocks = new();

        private readonly UserManager<ApplicationUser> _userManager;

        public TwoFactorSendThrottle(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<TwoFactorSendStatus> GetStatusAsync(ApplicationUser user)
        {
            var state = await ReadStateAsync(user);
            return BuildStatus(state, DateTimeOffset.UtcNow);
        }

        public async Task<TwoFactorSendStatus> TryAcquireAsync(ApplicationUser user)
        {
            var userLock = UserLocks.GetOrAdd(user.Id, _ => new SemaphoreSlim(1, 1));
            await userLock.WaitAsync();

            try
            {
                var now = DateTimeOffset.UtcNow;
                var state = await ReadStateAsync(user);
                var status = BuildStatus(state, now);
                if (!status.IsAllowed)
                {
                    return status;
                }

                if (state.WindowStart == null || now - state.WindowStart.Value >= Window)
                {
                    state = new SendState(now, 0, null);
                }

                var sendCount = state.SendCount + 1;
                var nextSend = now.Add(Cooldown);

                await SetTokenAsync(user, WindowStartToken, state.WindowStart.Value.ToString("O"));
                await SetTokenAsync(user, SendCountToken, sendCount.ToString(CultureInfo.InvariantCulture));
                await SetTokenAsync(user, NextSendToken, nextSend.ToString("O"));

                return new TwoFactorSendStatus(
                    false,
                    (int)Math.Ceiling(Cooldown.TotalSeconds),
                    Math.Max(0, MaxSendsPerWindow - sendCount),
                    true);
            }
            finally
            {
                userLock.Release();
            }
        }

        public async Task ClearAsync(ApplicationUser user)
        {
            await _userManager.RemoveAuthenticationTokenAsync(user, Provider, WindowStartToken);
            await _userManager.RemoveAuthenticationTokenAsync(user, Provider, SendCountToken);
            await _userManager.RemoveAuthenticationTokenAsync(user, Provider, NextSendToken);
        }

        private async Task<SendState> ReadStateAsync(ApplicationUser user)
        {
            var windowStartValue = await _userManager.GetAuthenticationTokenAsync(user, Provider, WindowStartToken);
            var sendCountValue = await _userManager.GetAuthenticationTokenAsync(user, Provider, SendCountToken);
            var nextSendValue = await _userManager.GetAuthenticationTokenAsync(user, Provider, NextSendToken);

            DateTimeOffset? windowStart = DateTimeOffset.TryParse(
                windowStartValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedWindowStart)
                ? parsedWindowStart
                : null;

            DateTimeOffset? nextSend = DateTimeOffset.TryParse(
                nextSendValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedNextSend)
                ? parsedNextSend
                : null;

            var sendCount = int.TryParse(
                sendCountValue,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsedSendCount)
                ? parsedSendCount
                : 0;

            return new SendState(windowStart, sendCount, nextSend);
        }

        private static TwoFactorSendStatus BuildStatus(SendState state, DateTimeOffset now)
        {
            if (state.WindowStart == null || now - state.WindowStart.Value >= Window)
            {
                return new TwoFactorSendStatus(true, 0, MaxSendsPerWindow);
            }

            if (state.SendCount >= MaxSendsPerWindow)
            {
                var retryAt = state.WindowStart.Value.Add(Window);
                return new TwoFactorSendStatus(
                    false,
                    Math.Max(1, (int)Math.Ceiling((retryAt - now).TotalSeconds)),
                    0);
            }

            if (state.NextSend.HasValue && state.NextSend.Value > now)
            {
                return new TwoFactorSendStatus(
                    false,
                    Math.Max(1, (int)Math.Ceiling((state.NextSend.Value - now).TotalSeconds)),
                    MaxSendsPerWindow - state.SendCount);
            }

            return new TwoFactorSendStatus(
                true,
                0,
                MaxSendsPerWindow - state.SendCount);
        }

        private async Task SetTokenAsync(ApplicationUser user, string name, string value)
        {
            var result = await _userManager.SetAuthenticationTokenAsync(user, Provider, name, value);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Impossible d'enregistrer la limite 2FA: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        private sealed record SendState(
            DateTimeOffset? WindowStart,
            int SendCount,
            DateTimeOffset? NextSend);
    }
}
