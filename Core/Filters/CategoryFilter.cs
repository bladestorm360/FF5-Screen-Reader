using FFV_ScreenReader.Field;

namespace FFV_ScreenReader.Core.Filters
{
    public class CategoryFilter : IEntityFilter
    {
        private bool isEnabled = true;
        
        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (value != isEnabled)
                {
                    isEnabled = value;
                    if (value)
                        OnEnabled();
                    else
                        OnDisabled();
                }
            }
        }
        
        public string Name => "Category Filter";
        
        public FilterTiming Timing => FilterTiming.OnAdd;
        
        public EntityCategory TargetCategory { get; set; } = EntityCategory.All;
        
        public bool PassesFilter(NavigableEntity entity, FilterContext context)
        {
            if (TargetCategory == EntityCategory.All)
                return true;
            
            return entity.Category == TargetCategory;
        }
        
        public void OnEnabled()
        {
        }
        
        public void OnDisabled()
        {
        }
    }
}
