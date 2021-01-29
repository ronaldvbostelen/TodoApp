namespace TodoApp.DB.Models
{
    public class ItemData : BaseDbModel
    {
        public string Title { get; set; }
        public string Details { get; set; }
        public bool Completed { get; set; }
    }
}