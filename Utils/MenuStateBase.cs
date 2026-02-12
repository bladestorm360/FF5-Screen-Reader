namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Base class for menu state tracking singletons.
    /// Provides the common IsActive/ShouldSuppress/ShouldAnnounce/ClearState pattern
    /// shared by most menu state classes.
    ///
    /// Subclasses provide RegistryKey and DeduplicationContext, then expose
    /// static facades that delegate to the singleton instance.
    /// </summary>
    public abstract class MenuStateBase
    {
        /// <summary>
        /// The MenuStateRegistry key for this state (e.g., MenuStateRegistry.BATTLE_TARGET).
        /// </summary>
        protected abstract string RegistryKey { get; }

        /// <summary>
        /// The AnnouncementDeduplicator context for this state (e.g., AnnouncementContexts.BATTLE_TARGET_PLAYER_INDEX).
        /// </summary>
        protected abstract string DeduplicationContext { get; }

        /// <summary>
        /// Whether this menu state is currently active.
        /// </summary>
        public bool IsActive
        {
            get => MenuStateRegistry.IsActive(RegistryKey);
            set => MenuStateRegistry.SetActive(RegistryKey, value);
        }

        /// <summary>
        /// Returns true if generic cursor reading should be suppressed.
        /// Default: suppress when active. Override for state-machine-based validation.
        /// </summary>
        public virtual bool ShouldSuppress() => IsActive;

        /// <summary>
        /// Check if text is new for deduplication purposes.
        /// </summary>
        public bool ShouldAnnounce(string text) =>
            AnnouncementDeduplicator.ShouldAnnounce(DeduplicationContext, text);

        /// <summary>
        /// Clears the active state.
        /// </summary>
        public virtual void ClearState() => IsActive = false;

        /// <summary>
        /// Resets the active state. Override to clear additional fields.
        /// </summary>
        public virtual void ResetState() => ClearState();

        /// <summary>
        /// Called by MenuStateRegistry reset handler.
        /// Resets the deduplication context. Override to add custom cleanup.
        /// </summary>
        protected virtual void OnReset()
        {
            AnnouncementDeduplicator.Reset(DeduplicationContext);
        }

        /// <summary>
        /// Registers this state's reset handler with MenuStateRegistry.
        /// Call from the static constructor of the facade class.
        /// </summary>
        public void RegisterResetHandler()
        {
            MenuStateRegistry.RegisterResetHandler(RegistryKey, OnReset);
        }
    }
}
