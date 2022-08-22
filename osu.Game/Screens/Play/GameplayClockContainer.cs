// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Encapsulates gameplay timing logic and provides a <see cref="IGameplayClock"/> via DI for gameplay components to use.
    /// </summary>
    [Cached(typeof(IGameplayClock))]
    public class GameplayClockContainer : Container, IAdjustableClock, IGameplayClock
    {
        /// <summary>
        /// Whether gameplay is paused.
        /// </summary>
        public IBindable<bool> IsPaused => isPaused;

        /// <summary>
        /// The source clock. Should generally not be used for any timekeeping purposes.
        /// </summary>
        public IClock SourceClock { get; private set; }

        /// <summary>
        /// Invoked when a seek has been performed via <see cref="Seek"/>
        /// </summary>
        public event Action? OnSeek;

        /// <summary>
        /// The time from which the clock should start. Will be seeked to on calling <see cref="Reset"/>.
        /// Can be adjusted by calling <see cref="Reset"/> with a time value.
        /// </summary>
        /// <remarks>
        /// By default, a value of zero will be used.
        /// Importantly, the value will be inferred from the current beatmap in <see cref="MasterGameplayClockContainer"/> by default.
        /// </remarks>
        public double StartTime { get; private set; }

        public virtual IEnumerable<double> NonGameplayAdjustments => Enumerable.Empty<double>();

        private readonly BindableBool isPaused = new BindableBool(true);

        /// <summary>
        /// The adjustable source clock used for gameplay. Should be used for seeks and clock control.
        /// This is the final clock exposed to gameplay components as an <see cref="IGameplayClock"/>.
        /// </summary>
        protected readonly FramedBeatmapClock GameplayClock;

        protected override Container<Drawable> Content { get; } = new Container { RelativeSizeAxes = Axes.Both };

        /// <summary>
        /// Creates a new <see cref="GameplayClockContainer"/>.
        /// </summary>
        /// <param name="sourceClock">The source <see cref="IClock"/> used for timing.</param>
        /// <param name="applyOffsets">Whether to apply platform, user and beatmap offsets to the mix.</param>
        public GameplayClockContainer(IClock sourceClock, bool applyOffsets = false)
        {
            SourceClock = sourceClock;

            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                GameplayClock = new FramedBeatmapClock(sourceClock, applyOffsets) { IsCoupled = false },
                Content
            };

            IsPaused.BindValueChanged(OnIsPausedChanged);
        }

        /// <summary>
        /// Starts gameplay and marks un-paused state.
        /// </summary>
        public virtual void Start()
        {
            ensureSourceClockSet();

            isPaused.Value = false;

            // the clock may be stopped via internal means (ie. not via `IsPaused`).
            if (!GameplayClock.IsRunning)
            {
                // Seeking the decoupled clock to its current time ensures that its source clock will be seeked to the same time
                // This accounts for the clock source potentially taking time to enter a completely stopped state
                Seek(GameplayClock.CurrentTime);

                // The case which cause this to be added is FrameStabilityContainer, which manages its own current and elapsed time.
                // Because we generally update our own current time quicker than children can query it (via Start/Seek/Update),
                // this means that the first frame ever exposed to children may have a non-zero current time.
                //
                // If the child component is not aware of the parent ElapsedFrameTime (which is the case for FrameStabilityContainer)
                // they will take on the new CurrentTime with a zero elapsed time. This can in turn cause components to behave incorrectly
                // if they are intending to trigger events at the precise StartTime (ie. DrawableStoryboardSample).
                //
                // By scheduling the start call, children are guaranteed to receive one frame at the original start time, allowing
                // then to progress with a correct locally calculated elapsed time.
                SchedulerAfterChildren.Add(GameplayClock.Start);
            }
        }

        /// <summary>
        /// Seek to a specific time in gameplay.
        /// </summary>
        /// <param name="time">The destination time to seek to.</param>
        public void Seek(double time)
        {
            Logger.Log($"{nameof(GameplayClockContainer)} seeking to {time}");

            GameplayClock.Seek(time);

            OnSeek?.Invoke();
        }

        /// <summary>
        /// Stops gameplay and marks paused state.
        /// </summary>
        public void Stop() => isPaused.Value = true;

        /// <summary>
        /// Resets this <see cref="GameplayClockContainer"/> and the source to an initial state ready for gameplay.
        /// </summary>
        /// <param name="time">The time to seek to on resetting. If <c>null</c>, the existing <see cref="StartTime"/> will be used.</param>
        /// <param name="startClock">Whether to start the clock immediately, if not already started.</param>
        public void Reset(double? time = null, bool startClock = false)
        {
            // Manually stop the source in order to not affect the IsPaused state.
            GameplayClock.Stop();

            ensureSourceClockSet();

            if (time != null)
                StartTime = time.Value;

            Seek(StartTime);

            if (!IsPaused.Value || startClock)
                Start();
        }

        /// <summary>
        /// Changes the source clock.
        /// </summary>
        /// <param name="sourceClock">The new source.</param>
        protected void ChangeSource(IClock sourceClock) => GameplayClock.ChangeSource(SourceClock = sourceClock);

        /// <summary>
        /// Ensures that the <see cref="GameplayClock"/> is set to <see cref="SourceClock"/>, if it hasn't been given a source yet.
        /// This is usually done before a seek to avoid accidentally seeking only the adjustable source in decoupled mode,
        /// but not the actual source clock.
        /// That will pretty much only happen on the very first call of this method, as the source clock is passed in the constructor,
        /// but it is not yet set on the adjustable source there.
        /// </summary>
        private void ensureSourceClockSet()
        {
            if (GameplayClock.Source == null)
                ChangeSource(SourceClock);
        }

        /// <summary>
        /// Invoked when the value of <see cref="IsPaused"/> is changed to start or stop the <see cref="GameplayClock"/> clock.
        /// </summary>
        /// <param name="isPaused">Whether the clock should now be paused.</param>
        protected virtual void OnIsPausedChanged(ValueChangedEvent<bool> isPaused)
        {
            if (isPaused.NewValue)
                GameplayClock.Stop();
            else
                GameplayClock.Start();
        }

        #region IAdjustableClock

        bool IAdjustableClock.Seek(double position)
        {
            Seek(position);
            return true;
        }

        void IAdjustableClock.Reset() => Reset();

        public void ResetSpeedAdjustments() => throw new NotImplementedException();

        double IAdjustableClock.Rate
        {
            get => GameplayClock.Rate;
            set => throw new NotSupportedException();
        }

        public double Rate => GameplayClock.Rate;

        public double CurrentTime => GameplayClock.CurrentTime;

        public bool IsRunning => GameplayClock.IsRunning;

        #endregion

        public void ProcessFrame()
        {
            // Handled via update. Don't process here to safeguard from external usages potentially processing frames additional times.
        }

        public double ElapsedFrameTime => GameplayClock.ElapsedFrameTime;

        public double FramesPerSecond => GameplayClock.FramesPerSecond;

        public FrameTimeInfo TimeInfo => GameplayClock.TimeInfo;

        public double TrueGameplayRate
        {
            get
            {
                double baseRate = Rate;

                foreach (double adjustment in NonGameplayAdjustments)
                {
                    if (Precision.AlmostEquals(adjustment, 0))
                        return 0;

                    baseRate /= adjustment;
                }

                return baseRate;
            }
        }
    }
}
