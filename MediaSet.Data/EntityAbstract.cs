namespace MediaSet.Data
{
    public abstract class EntityAbstract : IEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
