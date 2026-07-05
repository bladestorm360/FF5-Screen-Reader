namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Tracks whether the user most recently selected an entity or a waypoint.
    /// Read by the audio beacon and controller left-trigger handler to route to
    /// the correct target.
    /// </summary>
    public static class NavigationTargetTracker
    {
        public enum Kind { None, Entity, Waypoint }

        public static Kind LastKind { get; private set; } = Kind.None;

        public static void MarkEntity() => LastKind = Kind.Entity;
        public static void MarkWaypoint() => LastKind = Kind.Waypoint;
        public static void Clear() => LastKind = Kind.None;
    }
}
