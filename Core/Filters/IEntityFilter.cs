using FFV_ScreenReader.Field;

namespace FFV_ScreenReader.Core.Filters
{
    public enum FilterTiming
    {
        OnAdd,
        OnCycle,
        All
    }
    
    public interface IEntityFilter
    {
        bool IsEnabled { get; set; }
        
        string Name { get; }
        
        FilterTiming Timing { get; }
        
        bool PassesFilter(NavigableEntity entity, FilterContext context);
        
        void OnEnabled();
        
        void OnDisabled();
    }
}
